namespace JPSoftworks.ScreenManExtension.Pages;

internal sealed partial class CaptureListItem : ListItem, IDisposable
{
    private const int MaximumConcurrentThumbnailLoads = 4;
    private static readonly SemaphoreSlim ThumbnailLoadSemaphore = new(MaximumConcurrentThumbnailLoads);

    private readonly Details _details;
    private readonly OpenCaptureCommand _externalOpenCommand;
    private readonly CapturePreviewPage _previewPage;
    private readonly IContextItem[] _captureCommands;
    private readonly CancellationTokenSource _thumbnailCancellationTokenSource;
    private int _isDisposed;

    internal CaptureListItem(
        CaptureFile capture,
        CaptureMetadata metadata,
        CaptureMetadataStore metadataStore,
        Action<string>? onDeleted = null,
        bool openInPreview = false,
        CancellationToken thumbnailCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(metadataStore);
        this._thumbnailCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(thumbnailCancellationToken);
        this._externalOpenCommand = new OpenCaptureCommand(capture) { Name = "Open in default app" };

        var fallbackIcon = capture.Kind == CaptureMediaKind.Image ? Icons.Picture : Icons.Video;
        var localTimestamp = capture.ModifiedAtUtc.ToLocalTime();
        this.Title = string.IsNullOrWhiteSpace(metadata.Label) ? capture.FileName : metadata.Label;
        this.Subtitle = $"{localTimestamp:g} · {FormatSize(capture.SizeInBytes)}";
        this.Icon = fallbackIcon;
        this.TextToSuggest = this.Title;
        this.Tags = CreateTags(capture, metadata);
        this.DataPackage = CaptureDataPackageFactory.Create(capture);

        this._captureCommands =
        [
            new CommandContextItem(new CopyCaptureCommand(capture))
            {
                RequestedShortcut = Chords.CopyCapture,
            },
            new CommandContextItem(new ToggleFavoriteCommand(capture, metadataStore, metadata.IsFavorite))
            {
                RequestedShortcut = Chords.ToggleFavorite,
            },
            new CommandContextItem(new EditCaptureMetadataPage(capture, metadataStore))
            {
                RequestedShortcut = Chords.EditLabelAndTags,
            },
            new CommandContextItem(new ShowFileInFolderCommand(capture.FullPath))
            {
                RequestedShortcut = Chords.ShowInFolder,
            },
            new CommandContextItem(new CopyPathCommand(capture.FullPath))
            {
                RequestedShortcut = Chords.CopyPath,
            },
            new CommandContextItem(new DeleteCaptureCommand(capture, onDeleted: onDeleted))
            {
                RequestedShortcut = Chords.DeleteCapture,
                IsCritical = true,
            },
        ];

        var detailTags = CreateMetadataTags(metadata);
        this._details = new Details
        {
            Title = this.Title,
            HeroImage = fallbackIcon,
            Body = CreateDetailsBody(capture, metadata, localTimestamp),
            Metadata = detailTags.Length == 0
                ? []
                :
                [
                    new DetailsElement
                    {
                        Key = "Tags",
                        Data = new DetailsTags
                        {
                            Tags = detailTags,
                        },
                    },
                ],
            Size = ContentSize.Medium,
        };
        this.Details = this._details;
        this._previewPage = new CapturePreviewPage(capture, this._details);
        this.SetOpenInPreview(openInPreview);

        var cancellationToken = this._thumbnailCancellationTokenSource.Token;
        _ = Task.Run(
            () => this.LoadThumbnailAsync(capture, cancellationToken),
            CancellationToken.None);
    }

    internal void SetOpenInPreview(bool openInPreview)
    {
        ICommand primaryCommand = openInPreview ? this._previewPage : this._externalOpenCommand;
        if (ReferenceEquals(this.Command, primaryCommand))
        {
            return;
        }

        this.Command = primaryCommand;
        this.MoreCommands =
        [
            .. this._captureCommands,
            new CommandContextItem(openInPreview ? this._externalOpenCommand : this._previewPage),
        ];
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref this._isDisposed, 1) != 0)
        {
            return;
        }

        this._thumbnailCancellationTokenSource.Cancel();
        this._thumbnailCancellationTokenSource.Dispose();
    }

    private static ITag[] CreateTags(CaptureFile capture, CaptureMetadata metadata)
    {
        var tags = new List<ITag>
        {
            new Tag(capture.Kind == CaptureMediaKind.Image ? "Screenshot" : "Recording"),
        };
        if (metadata.IsFavorite)
        {
            tags.Add(new Tag("Favorite"));
        }

        tags.AddRange(metadata.Tags.Select(static tag => (ITag)new Tag(tag)));
        return [.. tags];
    }

    private static ITag[] CreateMetadataTags(CaptureMetadata metadata)
    {
        var tags = new List<ITag>();
        if (metadata.IsFavorite)
        {
            tags.Add(new Tag("Favorite"));
        }

        tags.AddRange(metadata.Tags.Select(static tag => (ITag)new Tag(tag)));
        return [.. tags];
    }

    private static string CreateDetailsBody(
        CaptureFile capture,
        CaptureMetadata metadata,
        DateTimeOffset localTimestamp)
    {
        var kind = capture.Kind == CaptureMediaKind.Image ? "Screenshot" : "Screen recording";
        var escapedPath = capture.FullPath.Replace("`", "\\`", StringComparison.Ordinal);
        var label = string.IsNullOrWhiteSpace(metadata.Label)
            ? string.Empty
            : $"**Label:** {metadata.Label}  \n";
        return $"{label}**Captured:** {localTimestamp:F}  \n**Type:** {kind}  \n**Size:** {FormatSize(capture.SizeInBytes)}  \n\n`{escapedPath}`";
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)Math.Max(bytes, 0);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.#} {units[unit]}";
    }

    private async Task LoadThumbnailAsync(CaptureFile capture, CancellationToken cancellationToken)
    {
        var enteredSemaphore = false;
        try
        {
            await ThumbnailLoadSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            enteredSemaphore = true;
            var streamReference = await CaptureThumbnailLoader.CreateAsync(capture, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (streamReference is null)
            {
                return;
            }

            var data = new IconData(streamReference);
            var icon = new IconInfo(data, data);
            this.Icon = icon;
            this._details.HeroImage = icon;
            this._previewPage.SetImage(icon);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ScreenManLog.Error($"Unable to load thumbnail for '{capture.FullPath}'.", ex);
        }
        finally
        {
            if (enteredSemaphore)
            {
                ThumbnailLoadSemaphore.Release();
            }
        }
    }
}

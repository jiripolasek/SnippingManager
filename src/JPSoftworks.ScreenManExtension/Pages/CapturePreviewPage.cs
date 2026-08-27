namespace JPSoftworks.ScreenManExtension.Pages;

internal sealed partial class CapturePreviewPage : ContentPage
{
    private readonly ImageContent _image;
    private readonly IContent[] _content;

    internal CapturePreviewPage(CaptureFile capture, Details captureDetails)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(captureDetails);

        this.Name = "Preview in Command Palette";
        this.Title = captureDetails.Title;
        this.Icon = capture.Kind == CaptureMediaKind.Image ? Icons.Picture : Icons.Video;
        this._image = new ImageContent(capture.Kind == CaptureMediaKind.Image
            ? new IconInfo(capture.FullPath)
            : Icons.Video);
        this._content = capture.Kind == CaptureMediaKind.Image
            ? [this._image]
            :
            [
                this._image,
                new PlainTextContent
                {
                    Text = "Still preview of this recording. Open in the default app to play it.",
                    WrapWords = true,
                },
            ];
        this.Details = new Details
        {
            Title = captureDetails.Title,
            Body = captureDetails.Body,
            Metadata = captureDetails.Metadata,
            Size = ContentSize.Small,
        };
        this.Commands =
        [
            new CommandContextItem(new OpenCaptureCommand(capture) { Name = "Open in default app" }),
            new CommandContextItem(new CopyCaptureCommand(capture))
            {
                RequestedShortcut = Chords.CopyCapture,
            },
            new CommandContextItem(new ShowFileInFolderCommand(capture.FullPath))
            {
                RequestedShortcut = Chords.ShowInFolder,
            },
            new CommandContextItem(new CopyPathCommand(capture.FullPath))
            {
                RequestedShortcut = Chords.CopyPath,
            },
        ];
    }

    public override IContent[] GetContent() => this._content;

    internal void SetImage(IconInfo image)
    {
        ArgumentNullException.ThrowIfNull(image);
        this._image.Image = image;
    }
}

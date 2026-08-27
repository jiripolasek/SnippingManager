namespace JPSoftworks.ScreenManExtension.Model;

internal sealed record CaptureFile(
    string FullPath,
    DateTimeOffset ModifiedAtUtc,
    long SizeInBytes,
    CaptureMediaKind Kind)
{
    internal string? FileIdentity { get; init; }

    internal string FileName => Path.GetFileName(this.FullPath);

    internal string DirectoryPath => Path.GetDirectoryName(this.FullPath) ?? string.Empty;
}

using System.Text.Json.Serialization;

namespace JPSoftworks.ScreenManExtension.Model;

internal sealed class CaptureMetadataDocument
{
    public Dictionary<string, CaptureMetadataEntry> Items { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class CaptureMetadataEntry
{
    public string? Label { get; set; }

    public string[] Tags { get; set; } = [];

    public bool IsFavorite { get; set; }
}

internal sealed class CaptureMetadataFormPayload
{
    public string? Label { get; set; }

    public string? Tags { get; set; }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CaptureMetadataDocument))]
[JsonSerializable(typeof(CaptureMetadataFormPayload))]
internal sealed partial class ScreenManJsonContext : JsonSerializerContext
{
}

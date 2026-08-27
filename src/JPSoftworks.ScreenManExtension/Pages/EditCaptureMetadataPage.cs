using System.Text.Json;

namespace JPSoftworks.ScreenManExtension.Pages;

internal sealed partial class EditCaptureMetadataPage : ContentPage
{
    private readonly IContent[] _content;

    internal EditCaptureMetadataPage(CaptureFile capture, CaptureMetadataStore store)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(store);

        this.Name = "Edit label and tags";
        this.Title = $"Organize {capture.FileName}";
        this.Icon = Icons.Edit;
        this._content = [new EditCaptureMetadataForm(capture, store)];
    }

    public override IContent[] GetContent()
    {
        return this._content;
    }

    private sealed partial class EditCaptureMetadataForm : FormContent
    {
        private readonly CaptureFile _capture;
        private readonly CaptureMetadataStore _store;

        internal EditCaptureMetadataForm(CaptureFile capture, CaptureMetadataStore store)
        {
            this._capture = capture;
            this._store = store;
            this.TemplateJson = CreateTemplate(store.Get(capture.FullPath, capture.FileIdentity));
            this.DataJson = string.Empty;
            this.StateJson = string.Empty;
        }

        public override ICommandResult SubmitForm(string payload, string data)
        {
            try
            {
                var values = JsonSerializer.Deserialize(
                    payload,
                    ScreenManJsonContext.Default.CaptureMetadataFormPayload);
                var label = values?.Label;
                var tagsText = values?.Tags;
                var tags = (tagsText ?? string.Empty).Split(
                    [',', ';', '\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                this._store.Update(this._capture.FullPath, label, tags, this._capture.FileIdentity);
                return CommandResult.GoBack();
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
            {
                ScreenManLog.Error("Unable to update capture metadata.", ex);
                return CommandResult.ShowToast("Snipping Manager couldn't save that label and tags.");
            }
        }

        private static string CreateTemplate(CaptureMetadata metadata)
        {
            var label = Quote(metadata.Label ?? string.Empty);
            var tags = Quote(string.Join(", ", metadata.Tags));
            return $$"""
                {
                  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
                  "type": "AdaptiveCard",
                  "version": "1.5",
                  "body": [
                    {
                      "type": "Input.Text",
                      "id": "label",
                      "label": "Label",
                      "placeholder": "Short descriptive name",
                      "value": {{label}},
                      "maxLength": 120
                    },
                    {
                      "type": "Input.Text",
                      "id": "tags",
                      "label": "Tags",
                      "placeholder": "work, bug, release",
                      "value": {{tags}},
                      "isMultiline": true
                    }
                  ],
                  "actions": [
                    {
                      "type": "Action.Submit",
                      "title": "Save",
                      "data": {
                        "label": "label",
                        "tags": "tags"
                      }
                    }
                  ]
                }
                """;
        }

        private static string Quote(string value)
        {
            return $"\"{JsonEncodedText.Encode(value)}\"";
        }
    }
}

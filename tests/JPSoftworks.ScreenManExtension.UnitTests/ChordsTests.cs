using JPSoftworks.ScreenManExtension.Commands;
using JPSoftworks.ScreenManExtension.Model;
using JPSoftworks.ScreenManExtension.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace JPSoftworks.ScreenManExtension.UnitTests;

[TestClass]
public sealed class ChordsTests
{
    [TestMethod]
    public void ContextCommandChordsMatchExpectedShortcuts()
    {
        AssertChord(Chords.CopyCapture, VirtualKeyModifiers.Control, VirtualKey.C);
        AssertChord(Chords.ToggleFavorite, VirtualKeyModifiers.Control, VirtualKey.D);
        AssertChord(Chords.EditLabelAndTags, VirtualKeyModifiers.Control, VirtualKey.E);
        AssertChord(
            Chords.ShowInFolder,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            VirtualKey.E);
        AssertChord(
            Chords.CopyPath,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            VirtualKey.C);
        AssertChord(Chords.DeleteCapture, VirtualKeyModifiers.Control, VirtualKey.Delete);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CaptureListItemAssignsCentralizedChordsInCommandOrder(bool isRecording)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var kind = isRecording ? CaptureMediaKind.Video : CaptureMediaKind.Image;
        var extension = kind == CaptureMediaKind.Image ? ".png" : ".mp4";
        var capturePath = Path.Combine(Path.GetTempPath(), $"screenman-shortcuts-{Guid.NewGuid():N}{extension}");
        var metadataPath = Path.Combine(Path.GetTempPath(), $"screenman-metadata-{Guid.NewGuid():N}.json");
        var capture = new CaptureFile(capturePath, DateTimeOffset.UtcNow, 0, kind);
        var metadataStore = new CaptureMetadataStore(metadataPath);

        using var item = new CaptureListItem(
            capture,
            CaptureMetadata.Empty,
            metadataStore,
            thumbnailCancellationToken: cancellation.Token);

        var commands = item.MoreCommands.Cast<CommandContextItem>().ToArray();
        Assert.HasCount(7, commands);
        Assert.AreEqual(Chords.CopyCapture, commands[0].RequestedShortcut);
        Assert.AreEqual(Chords.ToggleFavorite, commands[1].RequestedShortcut);
        Assert.AreEqual(Chords.EditLabelAndTags, commands[2].RequestedShortcut);
        Assert.AreEqual(Chords.ShowInFolder, commands[3].RequestedShortcut);
        Assert.AreEqual(Chords.CopyPath, commands[4].RequestedShortcut);
        Assert.AreEqual(Chords.DeleteCapture, commands[5].RequestedShortcut);
        var deleteCommand = Assert.IsInstanceOfType<DeleteCaptureCommand>(commands[5].Command);
        Assert.AreEqual("Delete", deleteCommand.Name);
        Assert.IsTrue(commands[5].IsCritical);
        Assert.IsInstanceOfType<CapturePreviewPage>(commands[6].Command);
    }

    private static void AssertChord(
        KeyChord chord,
        VirtualKeyModifiers expectedModifiers,
        VirtualKey expectedKey)
    {
        Assert.AreEqual(expectedModifiers, chord.Modifiers);
        Assert.AreEqual((int)expectedKey, chord.Vkey);
        Assert.AreEqual(0, chord.ScanCode);
    }
}

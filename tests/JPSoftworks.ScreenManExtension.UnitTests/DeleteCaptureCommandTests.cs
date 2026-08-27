using JPSoftworks.ScreenManExtension.Commands;
using JPSoftworks.ScreenManExtension.Model;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace JPSoftworks.ScreenManExtension.UnitTests;

[TestClass]
public sealed class DeleteCaptureCommandTests
{
    [TestMethod]
    public void InvokeRequiresConfirmationBeforeDeletingTheSelectedCapture()
    {
        var capture = CreateCapture();
        string? deletedPath = null;
        string? removedPath = null;
        var command = new DeleteCaptureCommand(capture, path =>
        {
            deletedPath = path;
            return Task.CompletedTask;
        }, onDeleted: path =>
        {
            Assert.AreEqual(deletedPath, path);
            removedPath = path;
        });

        var result = command.Invoke();

        Assert.AreEqual(CommandResultKind.Confirm, result.Kind);
        Assert.IsNull(deletedPath);
        Assert.IsNull(removedPath);
        var confirmation = Assert.IsInstanceOfType<ConfirmationArgs>(result.Args);
        Assert.IsTrue(confirmation.IsPrimaryCommandCritical);
        StringAssert.Contains(confirmation.Title, capture.FileName);
        StringAssert.Contains(confirmation.Description, capture.FullPath);
        StringAssert.Contains(confirmation.Description, "permanently deleted");

        var confirmedCommand = Assert.IsInstanceOfType<InvokableCommand>(confirmation.PrimaryCommand);
        var deleteResult = confirmedCommand.Invoke();

        Assert.AreEqual(capture.FullPath, deletedPath);
        Assert.AreEqual(capture.FullPath, removedPath);
        var toast = AssertToastKeepsOpen(deleteResult);
        StringAssert.Contains(toast.Message, capture.FileName);
    }

    [TestMethod]
    public void FailedDeleteReportsAnErrorAndKeepsTheGalleryOpen()
    {
        var removed = false;
        var command = new DeleteCaptureCommand(
            CreateCapture(),
            _ => Task.FromException(new UnauthorizedAccessException("Access denied.")),
            onDeleted: _ => removed = true);

        var result = ConfirmAndInvoke(command);

        var toast = AssertToastKeepsOpen(result);
        Assert.AreEqual("Snipping Manager couldn't delete that capture.", toast.Message);
        Assert.IsFalse(removed);
    }

    [TestMethod]
    public void CanceledDeleteKeepsTheGalleryOpenWithoutASuccessToast()
    {
        var removed = false;
        var command = new DeleteCaptureCommand(
            CreateCapture(),
            _ => Task.FromCanceled(new CancellationToken(canceled: true)),
            onDeleted: _ => removed = true);

        var result = ConfirmAndInvoke(command);

        Assert.AreEqual(CommandResultKind.KeepOpen, result.Kind);
        Assert.IsFalse(removed);
    }

    [TestMethod]
    public void MissingFileIsReportedWithoutThrowing()
    {
        var capture = CreateCapture();
        string? removedPath = null;
        var command = new DeleteCaptureCommand(capture, onDeleted: path => removedPath = path);

        var result = ConfirmAndInvoke(command);

        var toast = AssertToastKeepsOpen(result);
        Assert.AreEqual("That capture no longer exists.", toast.Message);
        Assert.AreEqual(capture.FullPath, removedPath);
    }

    [TestMethod]
    public void NotificationFailureDoesNotReportASuccessfulDeleteAsFailed()
    {
        var capture = CreateCapture();
        var command = new DeleteCaptureCommand(
            capture,
            _ => Task.CompletedTask,
            onDeleted: _ => throw new InvalidOperationException("List update failed."));

        var result = ConfirmAndInvoke(command);

        var toast = AssertToastKeepsOpen(result);
        Assert.AreEqual($"Deleted {capture.FileName}", toast.Message);
    }

    private static ICommandResult ConfirmAndInvoke(DeleteCaptureCommand command)
    {
        var confirmation = Assert.IsInstanceOfType<ConfirmationArgs>(command.Invoke().Args);
        return Assert.IsInstanceOfType<InvokableCommand>(confirmation.PrimaryCommand).Invoke();
    }

    private static ToastArgs AssertToastKeepsOpen(ICommandResult result)
    {
        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        var toast = Assert.IsInstanceOfType<ToastArgs>(result.Args);
        var followUp = toast.Result;
        Assert.IsNotNull(followUp);
        Assert.AreEqual(CommandResultKind.KeepOpen, followUp.Kind);
        return toast;
    }

    private static CaptureFile CreateCapture()
    {
        return new CaptureFile(
            Path.Combine(Path.GetTempPath(), $"screenman-delete-{Guid.NewGuid():N}.png"),
            DateTimeOffset.UtcNow,
            0,
            CaptureMediaKind.Image);
    }
}

using JPSoftworks.ScreenManExtension.Helpers;
using System.Runtime.InteropServices;

namespace JPSoftworks.ScreenManExtension.UnitTests;

[TestClass]
public sealed class ClipboardThreadTests
{
    [TestMethod]
    public void InvokeRunsWorkOnInitializedStaThread()
    {
        var apartmentState = ApartmentState.Unknown;
        var contextResult = unchecked((int)0x800401F0);

        ClipboardThread.Invoke(() =>
        {
            apartmentState = Thread.CurrentThread.GetApartmentState();
            contextResult = CoGetContextToken(out _);
        });

        Assert.AreEqual(ApartmentState.STA, apartmentState);
        Assert.AreEqual(0, contextResult);
    }

    [TestMethod]
    public void InvokePropagatesWorkException()
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => ClipboardThread.Invoke(() => throw new InvalidOperationException("Expected failure")));

        Assert.AreEqual("Expected failure", exception.Message);
    }

#pragma warning disable SYSLIB1054 // DllImport avoids enabling unsafe code in the test project for one assertion.
    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoGetContextToken(out IntPtr token);
#pragma warning restore SYSLIB1054
}

using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace JPSoftworks.ScreenManExtension.Helpers;

internal sealed partial class ClipboardThread
{
    private static readonly ClipboardThread Instance = new();

    private readonly BlockingCollection<WorkItem> _workItems = [];

    private ClipboardThread()
    {
        var thread = new Thread(this.ProcessWorkItems)
        {
            IsBackground = true,
            Name = "Snipping Manager clipboard thread",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    internal static void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var workItem = new WorkItem(action);
        Instance._workItems.Add(workItem);
        workItem.Wait();
    }

    private void ProcessWorkItems()
    {
        var initializationResult = CoInitialize(IntPtr.Zero);
        var initializationException = initializationResult < 0
            ? Marshal.GetExceptionForHR(initializationResult)
            : null;

        try
        {
            foreach (var workItem in this._workItems.GetConsumingEnumerable())
            {
                workItem.Execute(initializationException);
            }
        }
        finally
        {
            if (initializationResult >= 0)
            {
                CoUninitialize();
            }
        }
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoInitialize(IntPtr reserved);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    private sealed class WorkItem(Action action)
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Execute(Exception? initializationException)
        {
            if (initializationException is not null)
            {
                this._completion.SetException(initializationException);
                return;
            }

            try
            {
                action();
                this._completion.SetResult();
            }
            catch (Exception ex)
            {
                this._completion.SetException(ex);
            }
        }

        internal void Wait() => this._completion.Task.GetAwaiter().GetResult();
    }
}

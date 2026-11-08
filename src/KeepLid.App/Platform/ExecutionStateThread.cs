using System;
using System.Collections.Concurrent;
using System.Threading;
using KeepLid.App.Native;
using KeepLid.Core.Abstractions;

namespace KeepLid.App.Platform;

/// <summary>
/// Holds the "keep awake" request on a dedicated thread. SetThreadExecutionState(ES_CONTINUOUS | …) is a
/// per-thread request that Windows also clears automatically when the thread ends, so a dedicated, long-lived
/// thread makes the state explicit and is released cleanly (ES_CONTINUOUS alone) on Release or Dispose.
/// No keystroke or mouse movement is ever simulated.
/// </summary>
internal sealed class ExecutionStateThread : IExecutionState, IDisposable
{
    private readonly BlockingCollection<uint> _requests = new();
    private readonly Thread _thread;
    private readonly ILog _log;

    public ExecutionStateThread(ILog log)
    {
        _log = log;
        _thread = new Thread(Run) { IsBackground = true, Name = "KeepLid execution state" };
        _thread.Start();
    }

    public void KeepAwake(bool keepDisplayOn)
    {
        uint flags = NativeMethods.ES_CONTINUOUS | NativeMethods.ES_SYSTEM_REQUIRED;
        if (keepDisplayOn) flags |= NativeMethods.ES_DISPLAY_REQUIRED;
        _requests.Add(flags);
    }

    public void Release() => _requests.Add(NativeMethods.ES_CONTINUOUS);

    public void Dispose()
    {
        if (_requests.IsAddingCompleted) return;
        Release();
        _requests.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(2));
    }

    private void Run()
    {
        foreach (uint flags in _requests.GetConsumingEnumerable())
        {
            uint previous = NativeMethods.SetThreadExecutionState(flags);
            if (previous == 0) _log.Warn($"SetThreadExecutionState(0x{flags:X8}) failed");
        }
        NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
    }
}

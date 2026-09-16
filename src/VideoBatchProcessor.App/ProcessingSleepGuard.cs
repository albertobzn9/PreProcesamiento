using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VideoBatchProcessor.App;

internal sealed class ProcessingSleepGuard : IDisposable
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;
    private readonly Process? _caffeinate;
    private readonly bool _windowsStateSet;

    private ProcessingSleepGuard(Process? caffeinate, bool windowsStateSet)
    {
        _caffeinate = caffeinate;
        _windowsStateSet = windowsStateSet;
    }

    public static ProcessingSleepGuard PreventIdleSleep()
    {
        if (OperatingSystem.IsMacOS())
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo("/usr/bin/caffeinate")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    ArgumentList = { "-i" },
                });
                return new ProcessingSleepGuard(process, false);
            }
            catch
            {
                return new ProcessingSleepGuard(null, false);
            }
        }

        if (OperatingSystem.IsWindows())
        {
            var succeeded = SetThreadExecutionState(EsContinuous | EsSystemRequired) != 0;
            return new ProcessingSleepGuard(null, succeeded);
        }

        return new ProcessingSleepGuard(null, false);
    }

    public void Dispose()
    {
        if (_caffeinate is { HasExited: false })
        {
            _caffeinate.Kill();
            _caffeinate.WaitForExit();
        }
        _caffeinate?.Dispose();

        if (_windowsStateSet)
            SetThreadExecutionState(EsContinuous);
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint executionState);
}

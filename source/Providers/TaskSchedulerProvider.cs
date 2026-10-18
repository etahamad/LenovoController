using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace LenovoController.Providers
{
    public static class TaskSchedulerProvider
    {
        private const string TaskName = "LenovoController";

        public static bool IsTaskScheduled()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/query /tn \"{TaskName}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        p.WaitForExit(3000);
                        return p.ExitCode == 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Failed to query Task Scheduler: " + ex);
            }

            return false;
        }

        public static bool SetTaskScheduled(bool enable, bool startMinimized = true)
        {
            var exePath = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return false;

            try
            {
                if (enable)
                {
                    var args = startMinimized ? "-minimized" : "";
                    var taskRun = $"\\\"{exePath}\\\" {args}".Trim();

                    var psi = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/create /tn \"{TaskName}\" /tr \"{taskRun}\" /sc onlogon /rl highest /f",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (var p = Process.Start(psi))
                    {
                        if (p != null)
                        {
                            p.WaitForExit(5000);
                            return p.ExitCode == 0;
                        }
                    }
                }
                else
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/delete /tn \"{TaskName}\" /f",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (var p = Process.Start(psi))
                    {
                        if (p != null)
                        {
                            p.WaitForExit(5000);
                            return p.ExitCode == 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to update Task Scheduler: " + ex);
            }

            return false;
        }
    }
}

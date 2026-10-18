using LenovoController.Providers;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LenovoController.Features
{
    public enum PowerModeState
    {
        Quiet,
        Balance,
        Performance
    }

    public class PowerModeFeature : IFeature<PowerModeState>
    {
        private enum Backend
        {
            GameZoneWmi,
            WindowsPowerScheme
        }

        private Backend _backend = Backend.GameZoneWmi;
        private readonly WmiPowerMode _wmiFeature = new WmiPowerMode();

        private static readonly Guid GuidBalanced = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
        private static readonly Guid GuidHighPerformance = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        private static readonly Guid GuidPowerSaver = new Guid("a1841308-3541-4fab-bc81-f71556f20b4a");
        private static readonly Guid GuidUltimatePerformance = new Guid("b59a6058-f8fa-45aa-8a1b-9b0aa521e465");

        [DllImport("powrprof.dll")]
        private static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

        [DllImport("powrprof.dll")]
        private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr pSchemeGuid);

        [DllImport("Kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);

        private class WmiPowerMode : AbstractWmiFeature<PowerModeState>
        {
            public WmiPowerMode() : base("SmartFanMode", 1)
            {
            }
        }

        public PowerModeState GetState()
        {
            if (_backend == Backend.GameZoneWmi)
            {
                try
                {
                    return _wmiFeature.GetState();
                }
                catch
                {
                    _backend = Backend.WindowsPowerScheme;
                }
            }

            return GetWindowsPowerSchemeState();
        }

        public void SetState(PowerModeState state)
        {
            if (_backend == Backend.GameZoneWmi)
            {
                try
                {
                    _wmiFeature.SetState(state);
                    return;
                }
                catch
                {
                    _backend = Backend.WindowsPowerScheme;
                }
            }

            SetWindowsPowerSchemeState(state);
        }

        private static PowerModeState GetWindowsPowerSchemeState()
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out var pGuid) == 0 && pGuid != IntPtr.Zero)
            {
                try
                {
                    var activeGuid = (Guid)Marshal.PtrToStructure(pGuid, typeof(Guid));
                    if (activeGuid == GuidPowerSaver)
                        return PowerModeState.Quiet;
                    if (activeGuid == GuidHighPerformance || activeGuid == GuidUltimatePerformance)
                        return PowerModeState.Performance;
                    return PowerModeState.Balance;
                }
                finally
                {
                    LocalFree(pGuid);
                }
            }

            return PowerModeState.Balance;
        }

        private static void SetWindowsPowerSchemeState(PowerModeState state)
        {
            var targetGuid = GuidBalanced;
            switch (state)
            {
                case PowerModeState.Quiet:
                    targetGuid = GuidPowerSaver;
                    break;
                case PowerModeState.Balance:
                    targetGuid = GuidBalanced;
                    break;
                case PowerModeState.Performance:
                    targetGuid = GuidHighPerformance;
                    break;
            }

            var res = PowerSetActiveScheme(IntPtr.Zero, ref targetGuid);
            if (res != 0)
            {
                Trace.TraceWarning("PowerSetActiveScheme returned: " + res);
            }
        }
    }
}
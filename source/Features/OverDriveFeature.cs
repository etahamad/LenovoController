using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace LenovoController.Features
{
    public enum OverDriveState
    {
        Off,
        On
    }

    public class OverDriveFeature : IFeature<OverDriveState>
    {
        private readonly WmiOverDrive _wmiFeature = new WmiOverDrive();
        private bool _useWmi = true;
        private OverDriveState _fallbackState = OverDriveState.Off;
        private const string GameBarKey = @"Software\Microsoft\GameBar";

        private class WmiOverDrive : AbstractWmiFeature<OverDriveState>
        {
            public WmiOverDrive() : base("ODStatus", 0)
            {
            }
        }

        public OverDriveState GetState()
        {
            if (_useWmi)
            {
                try
                {
                    return _wmiFeature.GetState();
                }
                catch
                {
                    _useWmi = false;
                }
            }

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(GameBarKey, false))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("AllowAutoGameMode");
                        if (val is int intVal)
                        {
                            return intVal == 1 ? OverDriveState.On : OverDriveState.Off;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Failed to read OverDrive / Game Mode state: " + ex);
            }

            return _fallbackState;
        }

        public void SetState(OverDriveState state)
        {
            if (_useWmi)
            {
                try
                {
                    _wmiFeature.SetState(state);
                    return;
                }
                catch
                {
                    _useWmi = false;
                }
            }

            _fallbackState = state;
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(GameBarKey, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key != null)
                    {
                        key.SetValue("AllowAutoGameMode", state == OverDriveState.On ? 1 : 0, RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to write OverDrive / Game Mode state: " + ex);
            }
        }
    }
}
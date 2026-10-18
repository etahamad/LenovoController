using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace LenovoController.Features
{
    public enum TouchpadLockState
    {
        Off,
        On
    }

    public class TouchpadLockFeature : IFeature<TouchpadLockState>
    {
        private readonly WmiTouchpadLock _wmiFeature = new WmiTouchpadLock();
        private bool _useWmi = true;
        private const string TouchpadRegKey = @"Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad\Status";

        private class WmiTouchpadLock : AbstractWmiFeature<TouchpadLockState>
        {
            public WmiTouchpadLock() : base("TPStatus", 0)
            {
            }
        }

        public TouchpadLockState GetState()
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
                using (var key = Registry.CurrentUser.OpenSubKey(TouchpadRegKey, false))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("Enabled");
                        if (val is int intVal)
                        {
                            return intVal == 0 ? TouchpadLockState.On : TouchpadLockState.Off;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Failed to read Touchpad state from registry: " + ex);
            }

            return TouchpadLockState.Off;
        }

        public void SetState(TouchpadLockState state)
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

            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(TouchpadRegKey, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key != null)
                    {
                        key.SetValue("Enabled", state == TouchpadLockState.On ? 0 : 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to write Touchpad state to registry: " + ex);
            }
        }
    }
}
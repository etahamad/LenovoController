using Microsoft.Win32;
using System;

namespace LenovoController.Providers
{
    public static class AppConfig
    {
        private const string RegSubKey = @"Software\LenovoController";

        public static bool StartMinimized
        {
            get => GetBool("StartMinimized", true);
            set => SetBool("StartMinimized", value);
        }

        public static bool MinimizeToTrayOnClose
        {
            get => GetBool("MinimizeToTrayOnClose", true);
            set => SetBool("MinimizeToTrayOnClose", value);
        }

        private static bool GetBool(string name, bool defaultValue)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegSubKey, false))
                {
                    if (key != null)
                    {
                        var val = key.GetValue(name);
                        if (val is int intVal)
                            return intVal != 0;
                    }
                }
            }
            catch { }
            return defaultValue;
        }

        private static void SetBool(string name, bool value)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegSubKey, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    key?.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
                }
            }
            catch { }
        }
    }
}

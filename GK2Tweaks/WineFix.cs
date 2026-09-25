using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace GK2Tweaks
{
    // Controller-Fix fuer Mac (CrossOver/Wine) und Linux: Wine liest Controller wie den DualSense sonst ueber den
    // "hidraw"-Weg ein. Das kostete auf einem M1 Pro ~14 ms pro Bild (Ruckler). Der Schalter setzt den Wine-Registry-Wert
    // HKLM\System\CurrentControlSet\Services\WineBus\DisableHidraw. Wirkt erst nach Neustart der Wine-Umgebung.
    // Unter echtem Windows ist der Schalter unsichtbar und tut nichts.
    internal static class WineFix
    {
        private const string KeyPath = @"System\CurrentControlSet\Services\WineBus";
        private const string ValueName = "DisableHidraw";

        [DllImport("kernel32", CharSet = CharSet.Ansi)] private static extern IntPtr GetModuleHandleA(string name);
        [DllImport("kernel32", CharSet = CharSet.Ansi)] private static extern IntPtr GetProcAddress(IntPtr module, string name);

        private static bool? wine;
        private static bool? startState;
        internal static bool Pending;

        internal static bool IsWine
        {
            get
            {
                if (wine != null) return wine.Value;
                bool w = false;
                try
                {
                    IntPtr h = GetModuleHandleA("ntdll.dll");
                    w = h != IntPtr.Zero && GetProcAddress(h, "wine_get_version") != IntPtr.Zero;
                }
                catch { }
                if (!w)
                {
                    try { using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"Software\Wine")) w = k != null; } catch { }
                }
                wine = w;
                return w;
            }
        }

        private static bool? cached;

        internal static bool Enabled
        {
            get
            {
                if (cached != null) return cached.Value;
                cached = ReadRegistry();
                return cached.Value;
            }
            set
            {
                Write(value);
                cached = ReadRegistry();
                Pending = cached != startState;
            }
        }

        private static bool ReadRegistry()
        {
            {
                try
                {
                    using (RegistryKey k = Registry.LocalMachine.OpenSubKey(KeyPath))
                    {
                        object v = k?.GetValue(ValueName);
                        bool on = v is int i && i != 0;
                        if (startState == null) startState = on;
                        return on;
                    }
                }
                catch { return false; }
            }
        }

        private static void Write(bool value)
        {
            {
                try
                {
                    using (RegistryKey k = Registry.LocalMachine.CreateSubKey(KeyPath))
                    {
                        if (value) k.SetValue(ValueName, 1, RegistryValueKind.DWord);
                        else k.DeleteValue(ValueName, false);
                    }
                    Plugin.Log.LogInfo("Controller fix (DisableHidraw) " + (value ? "on" : "off") + " – restart the Wine/CrossOver bottle to apply");
                }
                catch (Exception e) { Plugin.Log.LogWarning("Controller fix: " + e.Message); }
            }
        }
    }
}

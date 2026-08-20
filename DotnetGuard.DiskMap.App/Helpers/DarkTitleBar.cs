using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DotnetGuard.DiskMap.App.Helpers
{
    internal static class DarkTitleBar
    {
        private const int DwmwaUseImmersiveDarkMode = 20;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public static void Apply(Window window)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            int useDarkMode = 1;
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDarkMode, sizeof(int));
        }
    }
}

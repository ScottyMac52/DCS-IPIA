using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DcsIpia.Presentation
{
    /// <summary>Colors native Windows 11 window chrome while retaining system window behavior.</summary>
    internal static class WindowThemeChrome
    {
        #region BEGIN Public
        /// <summary>Applies the live palette after handle creation; unsupported desktops retain their system chrome.</summary>
        internal static void Apply(Window window)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            var background = ((SolidColorBrush)window.FindResource("WindowBackgroundBrush")).Color;
            var dark = !SystemParameters.HighContrast && background.R * 299 + background.G * 587 + background.B * 114 < 128000 ? 1 : 0;
            var caption = SystemParameters.HighContrast ? -1 : ColorRef(window, "WindowBackgroundBrush");
            var text = SystemParameters.HighContrast ? -1 : ColorRef(window, "PrimaryTextBrush");
            var border = SystemParameters.HighContrast ? -1 : ColorRef(window, "ControlBorderBrush");
            // DWM returns HRESULT for unsupported attributes; no custom chrome or startup failure is needed.
            _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
            _ = DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
            _ = DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
            _ = DwmSetWindowAttribute(handle, 34, ref border, sizeof(int));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static int ColorRef(Window window, string key)
        {
            var color = ((SolidColorBrush)window.FindResource(key)).Color;
            return color.R | color.G << 8 | color.B << 16;
        }
        [DllImport("dwmapi.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        #endregion END private helpers
    }
}

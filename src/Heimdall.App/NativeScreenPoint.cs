using System.Runtime.InteropServices;

namespace Heimdall.App
{
    /// <summary>Sequential Win32 POINT coordinates returned by GetCursorPos.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeScreenPoint
    {
        #region BEGIN Public
        /// <summary>Horizontal screen coordinate in pixels.</summary>
        public int X;
        /// <summary>Vertical screen coordinate in pixels.</summary>
        public int Y;
        #endregion END Public
    }
}

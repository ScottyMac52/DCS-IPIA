using System.IO;
using System.Runtime.InteropServices;
using Heimdall.Application;

namespace Heimdall.App
{
    /// <summary>Reads object evidence without setting a data format, acquiring controls or force feedback.</summary>
    public static class WindowsControllerObjectEnumeration
    {
        #region BEGIN Public
        /// <summary>Enumerates all native objects, including inputs unsupported by the DCS mapper.</summary>
        /// <param name="device">Borrowed DirectInput device interface.</param>
        /// <param name="ct">Cancellation checked inside the native callback.</param>
        /// <returns>Complete native object evidence.</returns>
        public static unsafe IReadOnlyList<ControllerObject> Read(nint device, CancellationToken ct = default)
        {
            var objects = new List<ControllerObject>();
            Exception? error = null;
            ObjectCallback callback = (instance, _) =>
            {
                if (ct.IsCancellationRequested) return 0;
                try
                {
                    var size = Marshal.ReadInt32(instance);
                    if (size is < 552 or > 4096) throw new InvalidDataException("Invalid DirectInput object information.");
                    var bytes = new byte[size];
                    Marshal.Copy(instance, bytes, 0, bytes.Length);
                    objects.Add(ControllerObjectReader.Read(bytes));
                    return 1;
                }
                catch (Exception ex) { error = ex; return 0; }
            };
            var enumerate = (delegate* unmanaged[Stdcall]<nint, nint, nint, uint, int>)
                Marshal.ReadIntPtr(Marshal.ReadIntPtr(device), 4 * nint.Size);
            var result = enumerate(device, Marshal.GetFunctionPointerForDelegate(callback), 0, 0); // DIDFT_ALL
            GC.KeepAlive(callback);
            ct.ThrowIfCancellationRequested();
            Marshal.ThrowExceptionForHR(result);
            if (error is not null) throw new InvalidDataException("Cannot enumerate controller inputs.", error);
            return objects;
        }
        #endregion END Public
        #region BEGIN private helpers
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ObjectCallback(nint instance, nint context);
        #endregion END private helpers
    }
}

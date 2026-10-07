using System.IO;
using System.Runtime.InteropServices;
using Dcs.Ipia.Application;

namespace DcsIpia.Presentation
{
    /// <summary>Owns a DirectInput8 connection shared by discovery and foreground testing.</summary>
    public sealed class WindowsDirectInputConnection : IDisposable
    {
        private nint _library;
        private nint _input;
        #region BEGIN Public
        /// <summary>Creates a Unicode DirectInput8 connection.</summary>
        public unsafe WindowsDirectInputConnection()
        {
            nint kernel = 0;
            try
            {
                kernel = NativeLibrary.Load(Path.Combine(Environment.SystemDirectory, "kernel32.dll"));
                _library = NativeLibrary.Load(Path.Combine(Environment.SystemDirectory, "dinput8.dll"));
                var module = (delegate* unmanaged[Stdcall]<nint, nint>)NativeLibrary.GetExport(kernel, "GetModuleHandleW");
                var create = (delegate* unmanaged[Stdcall]<nint, uint, Guid*, nint*, nint, int>)NativeLibrary.GetExport(_library, "DirectInput8Create");
                var iid = new Guid("BF798031-483A-4DA2-AA99-5D64ED369700");
                nint input = 0;
                Marshal.ThrowExceptionForHR(create(module(0), 0x0800, &iid, &input, 0));
                _input = input;
            }
            catch { Dispose(); throw; }
            finally { if (kernel != 0) NativeLibrary.Free(kernel); }
        }
        /// <summary>Enumerates attached controller instances without acquiring them.</summary>
        /// <returns>Exact native instance identities.</returns>
        /// <param name="ct">Enumeration cancellation.</param>
        public unsafe IReadOnlyList<DetectedDcsDevice> Discover(CancellationToken ct = default)
        {
            ObjectDisposedException.ThrowIf(_input == 0, this);
            var devices = new List<DetectedDcsDevice>();
            Exception? callbackError = null;
            NativeDeviceEnumerationCallback callback = (instance, _) =>
            {
                if (ct.IsCancellationRequested) return 0;
                try
                {
                    if (Marshal.ReadInt32(instance) < 1100) throw new InvalidDataException("Unexpected DirectInput device information size.");
                    var bytes = new byte[16];
                    Marshal.Copy(instance + 4, bytes, 0, bytes.Length);
                    var guid = new Guid(bytes);
                    var name = Marshal.PtrToStringUni(instance + 40, 260)?.TrimEnd('\0') ?? "";
                    var terminator = name.IndexOf('\0');
                    if (terminator >= 0) name = name[..terminator];
                    if (guid != Guid.Empty && !string.IsNullOrWhiteSpace(name))
                        devices.Add(new(guid, name, true, ProductGuid: Marshal.PtrToStructure<Guid>(instance + 20)));
                    return 1;
                }
                catch (Exception ex) { callbackError = ex; return 0; }
            };
            var table = Marshal.ReadIntPtr(_input);
            var enumerate = (delegate* unmanaged[Stdcall]<nint, uint, nint, nint, uint, int>)Marshal.ReadIntPtr(table, 4 * nint.Size);
            var result = enumerate(_input, 4, Marshal.GetFunctionPointerForDelegate(callback), 0, 1);
            GC.KeepAlive(callback);
            ct.ThrowIfCancellationRequested();
            Marshal.ThrowExceptionForHR(result);
            if (callbackError is not null) throw new InvalidDataException("Controller information could not be read.", callbackError);
            return DcsPhysicalIdentity.Merge(devices, []);
        }
        /// <summary>Creates one controller interface by exact instance GUID.</summary>
        /// <param name="physicalIdentity">Physical DirectInput instance.</param>
        /// <returns>Owned controller COM pointer.</returns>
        public unsafe nint CreateDevice(Guid physicalIdentity)
        {
            ObjectDisposedException.ThrowIf(_input == 0, this);
            var create = (delegate* unmanaged[Stdcall]<nint, Guid*, nint*, nint, int>)Marshal.ReadIntPtr(Marshal.ReadIntPtr(_input), 3 * nint.Size);
            nint device = 0;
            Marshal.ThrowExceptionForHR(create(_input, &physicalIdentity, &device, 0));
            return device;
        }
        /// <summary>Releases the COM connection and its native library.</summary>
        public unsafe void Dispose()
        {
            if (_input != 0)
            {
                var release = (delegate* unmanaged[Stdcall]<nint, uint>)Marshal.ReadIntPtr(Marshal.ReadIntPtr(_input), 2 * nint.Size);
                _ = release(_input); _input = 0;
            }
            if (_library != 0) { NativeLibrary.Free(_library); _library = 0; }
        }
        #endregion END Public
        #region BEGIN private helpers
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int NativeDeviceEnumerationCallback(nint instance, nint context);
        #endregion END private helpers
    }
}

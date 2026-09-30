using System.IO;
using System.Runtime.InteropServices;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace DcsIpia.Presentation
{
    /// <summary>Enumerates attached DirectInput game controllers using their native instance GUIDs.</summary>
    public sealed class WindowsConnectedDeviceDiscovery : IConnectedDeviceDiscovery
    {
        /// <summary>DirectInput's synchronous Unicode enumeration callback ABI.</summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int NativeDeviceEnumerationCallback(nint instance, nint context);
        #region BEGIN Public
        /// <summary>Reads devices without acquiring them or changing DCS files.</summary>
        /// <returns>Native attached controller instances.</returns>
        public unsafe IReadOnlyList<DetectedDcsDevice> Discover()
        {
            var kernel = NativeLibrary.Load(Path.Combine(Environment.SystemDirectory, "kernel32.dll"));
            nint library = 0;
            nint input = 0;
            try
            {
                library = NativeLibrary.Load(Path.Combine(Environment.SystemDirectory, "dinput8.dll"));
                var module = (delegate* unmanaged[Stdcall]<nint, nint>)NativeLibrary.GetExport(kernel, "GetModuleHandleW");
                var create = (delegate* unmanaged[Stdcall]<nint, uint, Guid*, nint*, nint, int>)NativeLibrary.GetExport(library, "DirectInput8Create");
                var iid = new Guid("BF798031-483A-4DA2-AA99-5D64ED369700");
                Marshal.ThrowExceptionForHR(create(module(0), 0x0800, &iid, &input, 0));
                var devices = new List<DetectedDcsDevice>();
                Exception? callbackError = null;
                NativeDeviceEnumerationCallback callback = (instance, _) => {
                    try
                    {
                        if (Marshal.ReadInt32(instance) < 1100) throw new InvalidDataException("Unexpected DirectInput device information size.");
                        var bytes = new byte[16];
                        Marshal.Copy(instance + 4, bytes, 0, bytes.Length);
                        var guid = new Guid(bytes);
                        var name = Marshal.PtrToStringUni(instance + 40, 260)?.TrimEnd('\0') ?? "";
                        var terminator = name.IndexOf('\0');
                        if (terminator >= 0) name = name[..terminator];
                        if (guid != Guid.Empty && !string.IsNullOrWhiteSpace(name)) devices.Add(new(guid, name, true));
                        return 1;
                    }
                    catch (Exception ex) { callbackError = ex; return 0; }
                };
                var table = Marshal.ReadIntPtr(input);
                var enumerate = (delegate* unmanaged[Stdcall]<nint, uint, nint, nint, uint, int>)Marshal.ReadIntPtr(table, 4 * nint.Size);
                var result = enumerate(input, 4, Marshal.GetFunctionPointerForDelegate(callback), 0, 1);
                GC.KeepAlive(callback);
                Marshal.ThrowExceptionForHR(result);
                if (callbackError is not null) throw new InvalidDataException("Controller information could not be read.", callbackError);
                return DcsPhysicalIdentity.Merge(devices, []);
            }
            finally
            {
                if (input != 0)
                {
                    var release = (delegate* unmanaged[Stdcall]<nint, uint>)Marshal.ReadIntPtr(Marshal.ReadIntPtr(input), 2 * nint.Size);
                    _ = release(input);
                }
                if (library != 0) NativeLibrary.Free(library);
                NativeLibrary.Free(kernel);
            }
        }
        #endregion END Public
    }
}

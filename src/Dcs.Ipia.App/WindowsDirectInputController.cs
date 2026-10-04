using System.IO;
using System.Runtime.InteropServices;
using Dcs.Ipia.Application;

namespace DcsIpia.Presentation
{
    /// <summary>Acquires one controller with a format containing only enumerated input objects.</summary>
    public sealed class WindowsDirectInputController : IDisposable
    {
        private nint _device;
        private readonly Dictionary<string, int> _offsets = new(StringComparer.Ordinal);
        private readonly byte[] _state = new byte[176];
        #region BEGIN Public
        /// <summary>Configures foreground nonexclusive input without force feedback.</summary>
        /// <param name="device">Owned native device interface.</param>
        /// <param name="window">Foreground top-level window handle.</param>
        public unsafe WindowsDirectInputController(nint device, nint window)
        {
            _device = device;
            try
            {
                ConfigureFormat();
                var cooperate = (delegate* unmanaged[Stdcall]<nint, nint, uint, int>)Method(13);
                Marshal.ThrowExceptionForHR(cooperate(_device, window, 6)); // NONEXCLUSIVE | FOREGROUND
                Marshal.ThrowExceptionForHR(Call(7));
            }
            catch { Dispose(); throw; }
        }
        /// <summary>Gets only the supported physical DCS inputs.</summary>
        public IReadOnlyCollection<string> SupportedInputs => DirectInputStateDecoder.Decode(_state, _offsets).Keys.ToArray();
        /// <summary>Reads a complete controller state; acquisition loss stops the owning session.</summary>
        /// <returns>Normalized supported input values.</returns>
        public unsafe IReadOnlyDictionary<string, double> Read()
        {
            ObjectDisposedException.ThrowIf(_device == 0, this);
            Marshal.ThrowExceptionForHR(Call(25)); // Poll
            var read = (delegate* unmanaged[Stdcall]<nint, uint, byte*, int>)Method(9);
            fixed (byte* state = _state) Marshal.ThrowExceptionForHR(read(_device, (uint)_state.Length, state));
            return DirectInputStateDecoder.Decode(_state, _offsets);
        }
        /// <summary>Unacquires and releases this controller exactly once.</summary>
        public unsafe void Dispose()
        {
            if (_device == 0) return;
            _ = Call(8);
            var release = (delegate* unmanaged[Stdcall]<nint, uint>)Method(2);
            _ = release(_device); _device = 0;
        }
        #endregion END Public
        #region BEGIN private helpers
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ObjectCallback(nint instance, nint context);
        private nint Method(int index) => Marshal.ReadIntPtr(Marshal.ReadIntPtr(_device), index * nint.Size);
        private unsafe int Call(int index) => ((delegate* unmanaged[Stdcall]<nint, int>)Method(index))(_device);
        private unsafe void ConfigureFormat()
        {
            var objects = new List<(Guid Guid, uint Type)>();
            Exception? error = null;
            ObjectCallback callback = (instance, _) =>
            {
                try
                {
                    if (Marshal.ReadInt32(instance) < 32) throw new InvalidDataException("Invalid DirectInput object information.");
                    objects.Add((Marshal.PtrToStructure<Guid>(instance + 4), unchecked((uint)Marshal.ReadInt32(instance + 24))));
                    return 1;
                }
                catch (Exception ex) { error = ex; return 0; }
            };
            var enumerate = (delegate* unmanaged[Stdcall]<nint, nint, nint, uint, int>)Method(4);
            Marshal.ThrowExceptionForHR(enumerate(_device, Marshal.GetFunctionPointerForDelegate(callback), 0, 31));
            GC.KeepAlive(callback);
            if (error is not null) throw new InvalidDataException("Cannot enumerate controller inputs.", error);
            var rows = new List<(Guid Guid, uint Type, int Offset, string Key)>();
            var slider = 0;
            foreach (var item in objects.OrderBy(x => (x.Type >> 8) & 65535))
            {
                var instance = (int)((item.Type >> 8) & 65535);
                if ((item.Type & 12) != 0 && instance < 128)
                    rows.Add((item.Guid, item.Type, 48 + instance, "JOY_BTN" + (instance + 1)));
                else if ((item.Type & 16) != 0 && instance < 4)
                    rows.Add((item.Guid, item.Type, 32 + instance * 4, "JOY_BTN_POV" + (instance + 1)));
                else if ((item.Type & 2) != 0)
                {
                    var axis = item.Guid.ToString("D")[..8] switch
                    {
                        "a36d02e0" => (0, "JOY_X"),
                        "a36d02e1" => (4, "JOY_Y"),
                        "a36d02e2" => (8, "JOY_Z"),
                        "a36d02f4" => (12, "JOY_RX"),
                        "a36d02f5" => (16, "JOY_RY"),
                        "a36d02e3" => (20, "JOY_RZ"),
                        "a36d02e4" when slider < 2 => (24 + slider * 4, "JOY_SLIDER" + (++slider)),
                        _ => (-1, "")
                    };
                    if (axis.Item1 >= 0) rows.Add((item.Guid, item.Type, axis.Item1, axis.Item2));
                }
            }
            if (rows.Count == 0) throw new InvalidDataException("Controller exposes no supported absolute axes, buttons or hats.");
            var objectSize = nint.Size == 8 ? 24 : 16;
            var pointerOffset = nint.Size == 8 ? 24 : 20;
            var format = Marshal.AllocHGlobal(pointerOffset + nint.Size);
            var nativeRows = Marshal.AllocHGlobal(rows.Count * objectSize);
            var guids = Marshal.AllocHGlobal(rows.Count * 16);
            try
            {
                new Span<byte>((void*)nativeRows, rows.Count * objectSize).Clear();
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    Marshal.StructureToPtr(row.Guid, guids + i * 16, false);
                    var entry = nativeRows + i * objectSize;
                    Marshal.WriteIntPtr(entry, guids + i * 16);
                    Marshal.WriteInt32(entry, nint.Size, row.Offset);
                    Marshal.WriteInt32(entry, nint.Size + 4, unchecked((int)(row.Type & 0x00ffffff)));
                    _offsets.Add(row.Key, row.Offset);
                }
                new Span<byte>((void*)format, pointerOffset + nint.Size).Clear();
                Marshal.WriteInt32(format, pointerOffset + nint.Size);
                Marshal.WriteInt32(format, 4, objectSize);
                Marshal.WriteInt32(format, 8, 1); // DIDF_ABSAXIS
                Marshal.WriteInt32(format, 12, _state.Length);
                Marshal.WriteInt32(format, 16, rows.Count);
                Marshal.WriteIntPtr(format, pointerOffset, nativeRows);
                var set = (delegate* unmanaged[Stdcall]<nint, nint, int>)Method(11);
                Marshal.ThrowExceptionForHR(set(_device, format));
                var range = stackalloc int[6] { 24, 16, 0, 1, 0, 65535 };
                foreach (var row in rows.Where(x => (x.Type & 2) != 0))
                {
                    range[2] = row.Offset;
                    var property = (delegate* unmanaged[Stdcall]<nint, nint, int*, int>)Method(6);
                    Marshal.ThrowExceptionForHR(property(_device, 4, range)); // DIPROP_RANGE, BYOFFSET
                }
            }
            finally { Marshal.FreeHGlobal(guids); Marshal.FreeHGlobal(nativeRows); Marshal.FreeHGlobal(format); }
        }
        #endregion END private helpers
    }
}

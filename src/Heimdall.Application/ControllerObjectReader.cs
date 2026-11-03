using System.Buffers.Binary;
using System.Text;

namespace Heimdall.Application
{
    /// <summary>Decodes documented Unicode DIDEVICEOBJECTINSTANCE bytes without applying any simulator mapping.</summary>
    public static class ControllerObjectReader
    {
        #region BEGIN Public
        /// <summary>Preserves every supplied native field, respecting the structure's declared size.</summary>
        public static ControllerObject Read(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 4) throw new InvalidDataException("Missing DirectInput object size.");
            var size = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
            if (size < 552 || size > bytes.Length) throw new InvalidDataException("Invalid DirectInput object information size.");
            bytes = bytes[..(int)size];
            var name = Encoding.Unicode.GetString(bytes.Slice(32, 520));
            var terminator = name.IndexOf('\0');
            if (terminator >= 0) name = name[..terminator];
            return new(new Guid(bytes.Slice(4, 16)), BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(24, 4)), name)
            {
                Details = new(BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(20, 4)), BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(28, 4)),
                    UInt32(bytes, 552), UInt32(bytes, 556), UInt16(bytes, 560), UInt16(bytes, 562), UInt16(bytes, 564),
                    UInt16(bytes, 566), UInt32(bytes, 568), UInt16(bytes, 572), UInt16(bytes, 574))
            };
        }
        #endregion END Public
        #region BEGIN private helpers
        private static uint? UInt32(ReadOnlySpan<byte> bytes, int offset) => bytes.Length >= offset + 4
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4)) : null;
        private static ushort? UInt16(ReadOnlySpan<byte> bytes, int offset) => bytes.Length >= offset + 2
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2)) : null;
        #endregion END private helpers
    }
}

using System.Buffers.Binary;

namespace Heimdall.Application
{
    /// <summary>Bounds embedded raster size and dimensions before a native renderer sees image bytes.</summary>
    public static class RasterImageContract
    {
        #region BEGIN Public
        /// <summary>Accepts complete PNG/JPEG containers with bounded pixel dimensions.</summary>
        /// <param name="bytes">Untrusted raster file.</param>
        /// <param name="mime">PNG or JPEG media subtype.</param>
        /// <returns>Validated decoded pixel count.</returns>
        public static long Validate(byte[] bytes, string mime)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            if (bytes.Length < 24 || bytes.Length > 4_000_000) throw new InvalidDataException("Raster images must be complete and at most 4 MB.");
            if (mime == "png")
            {
                if (!bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
                    !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) ||
                    !bytes.AsSpan(bytes.Length - 12).SequenceEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 }))
                    throw new InvalidDataException("Incomplete PNG image.");
                PngChunks(bytes);
                return Dimensions(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)),
                    BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)));
            }
            if (mime != "jpeg" || bytes[0] != 255 || bytes[1] != 216 || bytes[^2] != 255 || bytes[^1] != 217)
                throw new InvalidDataException("Incomplete JPEG image.");
            var offset = 2;
            while (offset + 4 < bytes.Length)
            {
                if (bytes[offset++] != 255) throw new InvalidDataException("Invalid JPEG marker.");
                while (offset < bytes.Length && bytes[offset] == 255) offset++;
                if (offset + 3 >= bytes.Length) break;
                var marker = bytes[offset++];
                if (marker == 218) break;
                var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
                if (length < 2 || offset + length > bytes.Length) throw new InvalidDataException("Truncated JPEG image.");
                if (marker is 192 or 193 or 194)
                {
                    if (length < 8) throw new InvalidDataException("Invalid JPEG frame.");
                    return Dimensions(BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 5, 2)),
                        BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 3, 2)));
                }
                offset += length;
            }
            throw new InvalidDataException("JPEG image has no supported frame.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static long Dimensions(uint width, uint height)
        {
            if (width == 0 || height == 0 || width > 4096 || height > 4096 || (long)width * height > 8_000_000)
                throw new InvalidDataException("Raster dimensions must be positive, at most 4096 per side and at most 8 million pixels.");
            return (long)width * height;
        }
        private static void PngChunks(byte[] bytes)
        {
            var offset = 8;
            var imageData = false;
            while (offset < bytes.Length)
            {
                if (bytes.Length - offset < 12) throw new InvalidDataException("Truncated PNG chunk.");
                var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
                if (length > bytes.Length - offset - 12) throw new InvalidDataException("Truncated PNG payload.");
                var count = checked((int)length);
                var chunk = bytes.AsSpan(offset + 4, count + 4);
                uint crc = uint.MaxValue;
                foreach (var value in chunk)
                {
                    crc ^= value;
                    for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
                }
                if (~crc != BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + count + 8, 4)))
                    throw new InvalidDataException("PNG chunk checksum differs.");
                var type = chunk[..4];
                if (offset == 8 && (!type.SequenceEqual("IHDR"u8) || count != 13))
                    throw new InvalidDataException("Invalid PNG header.");
                if (type.SequenceEqual("IDAT"u8)) imageData = true;
                if (type.SequenceEqual("IEND"u8) && (!imageData || count != 0 || offset + 12 != bytes.Length))
                    throw new InvalidDataException("Invalid PNG image terminator.");
                offset += count + 12;
            }
        }
        #endregion END private helpers
    }
}

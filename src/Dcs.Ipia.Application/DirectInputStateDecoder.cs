using System.Buffers.Binary;

namespace Dcs.Ipia.Application
{
    /// <summary>Decodes the custom native controller format without inventing absent controls.</summary>
    public static class DirectInputStateDecoder
    {
        private static readonly string[] Directions = ["U", "UR", "R", "DR", "D", "DL", "L", "UL"];
        #region BEGIN Public
        /// <summary>Reads only the offsets enumerated for this physical controller.</summary>
        /// <param name="state">Native state buffer.</param>
        /// <param name="offsets">Supported DCS inputs and custom-format offsets.</param>
        /// <returns>Normalized axes and digital button/hat states.</returns>
        public static IReadOnlyDictionary<string, double> Decode(byte[] state, IReadOnlyDictionary<string, int> offsets)
        {
            var result = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var (key, offset) in offsets)
            {
                var hat = key.StartsWith("JOY_BTN_POV", StringComparison.Ordinal);
                var button = !hat && key.StartsWith("JOY_BTN", StringComparison.Ordinal);
                if (offset < 0 || offset > state.Length - (button ? 1 : 4))
                    throw new InvalidDataException("DirectInput state is shorter than its supported input format.");
                if (button) result.Add(key, (state[offset] & 128) != 0 ? 1 : 0);
                else
                {
                    var raw = BinaryPrimitives.ReadInt32LittleEndian(state.AsSpan(offset, 4));
                    if (hat)
                    {
                        var direction = (raw & 65535) == 65535 ? -1 : ((raw + 2250) / 4500) % 8;
                        if (raw < 0) direction = -1;
                        for (var i = 0; i < Directions.Length; i++) result.Add(key + "_" + Directions[i], i == direction ? 1 : 0);
                    }
                    else result.Add(key, Math.Clamp(raw / 65535.0, 0, 1));
                }
            }
            return result;
        }
        #endregion END Public
    }
}

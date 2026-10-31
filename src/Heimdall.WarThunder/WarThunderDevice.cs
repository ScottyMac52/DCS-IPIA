using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>A saved mapping occurrence; offsets address native inputs and do not establish a Windows instance.</summary>
    public sealed record WarThunderDevice(string Id, string RecordId, string Name, string Product,
        int AxesOffset, int AxesCount, int ButtonsOffset, int ButtonsCount, bool Valid)
    {
        #region BEGIN Public
        /// <summary>Checks membership in the captured half-open native range, using widened arithmetic.</summary>
        public bool Contains(CommandType type, int index) => Valid && index >= 0 && (type switch
        {
            CommandType.Axis => index >= AxesOffset && (long)index < (long)AxesOffset + AxesCount,
            CommandType.Button => index >= ButtonsOffset && (long)index < (long)ButtonsOffset + ButtonsCount,
            _ => false
        });
        /// <summary>Encodes a zero-based native local index after validating the captured range.</summary>
        public int Encode(CommandType type, int index)
        {
            if (!Valid || index < 0 || type is not (CommandType.Axis or CommandType.Button))
                throw new InvalidOperationException("Choose a valid native axis or button index.");
            var count = type == CommandType.Axis ? AxesCount : ButtonsCount;
            var offset = type == CommandType.Axis ? AxesOffset : ButtonsOffset;
            if (index >= count) throw new InvalidOperationException("Native input index is outside this saved device range.");
            return checked(offset + index);
        }
        #endregion END Public
    }
}

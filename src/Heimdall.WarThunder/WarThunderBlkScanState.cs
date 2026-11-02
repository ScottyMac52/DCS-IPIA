using System.Globalization;

namespace Heimdall.WarThunder
{
    /// <summary>Keeps nested block and occurrence identity state local to one native text scan.</summary>
    internal sealed class WarThunderBlkScanState
    {
        private readonly Dictionary<(string Parent, string Name, bool Block), int> _occurrences = new();
        #region BEGIN Public
        internal Stack<int> OpenBlocks { get; } = new();
        internal List<(string Text, int Start)> Pending { get; } = [];
        internal string NextAddress(string parent, string name, bool block)
        {
            var key = (parent, name, block);
            var index = _occurrences.GetValueOrDefault(key);
            _occurrences[key] = index + 1;
            return (block ? "b:" : "s:") + (parent.Length == 0 ? "" : parent[2..] + "/")
                + Uri.EscapeDataString(name) + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
        }
        #endregion END Public
    }
}

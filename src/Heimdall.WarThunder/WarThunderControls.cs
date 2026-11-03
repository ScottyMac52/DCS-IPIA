using System.Globalization;

namespace Heimdall.WarThunder
{
    /// <summary>Inventories captured controls without inferring command semantics or physical device ownership.</summary>
    public sealed class WarThunderControls
    {
        #region BEGIN Public
        /// <summary>Gets the immutable native capture; inventory discovery never rewrites it.</summary>
        public WarThunderBlkDocument Document { get; }
        /// <summary>Gets captured hotkey scalars, including unknown activation values.</summary>
        public IReadOnlyList<WarThunderBlkScalar> Hotkeys { get; }
        /// <summary>Gets captured axis scalars without interpreting native global offsets.</summary>
        public IReadOnlyList<WarThunderBlkScalar> Axes { get; }
        /// <summary>Gets captured joystick scalars without matching device names to hardware instances.</summary>
        public IReadOnlyList<WarThunderBlkScalar> Joysticks { get; }
        /// <summary>Captures native text and discovers supported controls subtrees independently of WPF.</summary>
        public WarThunderControls(string machineBlk)
        {
            Document = WarThunderBlkDocument.Parse(machineBlk);
            Hotkeys = Document.Scalars.Where(x => InSection(x.Path, "hotkeys")).ToList().AsReadOnly();
            Axes = Document.Scalars.Where(x => InSection(x.Path, "axes")).ToList().AsReadOnly();
            Joysticks = Document.Scalars.Where(x => InSection(x.Path, "joysticks") || InSection(x.Path, "deviceMapping/joystick")).ToList().AsReadOnly();
        }
        /// <summary>Gets uniquely addressed values eligible for reviewed scalar edits; this does not authorize hardware bindings.</summary>
        public IReadOnlyList<WarThunderBlkScalar> EditableSettings => Document.HasOpaqueSyntax ? [] :
            Document.Scalars.GroupBy(x => x.Path, StringComparer.Ordinal).Where(group => group.Count() == 1)
                .Select(group => group.Single()).ToList().AsReadOnly();
        /// <summary>Returns captured numeric activation codes without tap/hold interpretations or failures on opaque values.</summary>
        public IReadOnlyList<int> ActivationCodes()
        {
            var codes = new SortedSet<int>();
            foreach (var scalar in Hotkeys.Where(x => x.Path.EndsWith("/activationType", StringComparison.Ordinal) && x.Type == 'i'))
                if (int.TryParse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)) codes.Add(code);
            return codes.ToArray();
        }
        /// <summary>Changes one reviewed native scalar; physical association and command creation are outside this capability.</summary>
        public string ReplaceReviewedSetting(string path, string nativeValue) => Document.ReplaceScalar(path, nativeValue);
        #endregion END Public
        #region BEGIN private helpers
        private static bool InSection(string path, string section) =>
            path.StartsWith("controls/" + section + "/", StringComparison.Ordinal)
                || path.Contains("/controls/" + section + "/", StringComparison.Ordinal);
        #endregion END private helpers
    }
}

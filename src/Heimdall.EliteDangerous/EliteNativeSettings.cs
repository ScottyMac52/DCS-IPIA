using System.Xml.Linq;

namespace Heimdall.EliteDangerous
{
    /// <summary>Validates repeated scalar settings, normalizing only exact duplicates during game export.</summary>
    internal static class EliteNativeSettings
    {
        #region BEGIN Public
        /// <summary>Rejects conflicting duplicate scalar settings without choosing which value should win.</summary>
        internal static void Validate(XElement root)
        {
            foreach (var group in root.Elements().Where(x => !x.HasElements).GroupBy(x => x.Name).Where(x => x.Count() > 1))
                if (group.Skip(1).Any(x => !XNode.DeepEquals(group.First(), x)))
                    throw new InvalidOperationException("Conflicting duplicate ED setting: " + group.Key + ". Resolve the source values before importing or exporting.");
        }
        /// <summary>Removes only identical scalar duplicates and returns their count.</summary>
        internal static int Normalize(XElement root)
        {
            Validate(root);
            var duplicates = root.Elements().Where(x => !x.HasElements).GroupBy(x => x.Name).SelectMany(x => x.Skip(1)).ToArray();
            foreach (var duplicate in duplicates) duplicate.Remove();
            return duplicates.Length;
        }
        #endregion END Public
    }
}

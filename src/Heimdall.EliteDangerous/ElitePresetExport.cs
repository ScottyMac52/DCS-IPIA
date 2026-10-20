using System.Text.RegularExpressions;
using System.Xml.Linq;
using Heimdall.Application;

namespace Heimdall.EliteDangerous
{
    /// <summary>Aligns user-visible native preset identity without changing captured source evidence.</summary>
    internal static class ElitePresetExport
    {
        #region BEGIN Public
        /// <summary>Renames one validated preset and matching selector lines, preserving other control-section choices.</summary>
        internal static SimProfileExportPlan Prepare(SimProfileExportPlan plan, string name)
        {
            if (!plan.Succeeded) return plan;
            try
            {
                ValidateName(name);
                var native = EliteNativeDocument.Read(plan.Files);
                var oldName = EliteNativeDocument.Attribute(native.Root, "PresetName");
                native.Root.SetAttributeValue("PresetName", name);
                var removed = EliteNativeSettings.Normalize(native.Root);
                var files = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (path, text) in plan.Files.Where(x => x.Key != native.Path))
                    files.Add(path, Regex.Replace(text, @"(?m)^[^\r\n]+", match => match.Value.Trim() == oldName ? name : match.Value));
                var pathName = name + "." + EliteNativeDocument.Attribute(native.Root, "MajorVersion") + "." + EliteNativeDocument.Attribute(native.Root, "MinorVersion") + ".binds";
                files.Add(pathName, (native.Xml.Declaration is null ? "" : native.Xml.Declaration + "\n") + native.Xml.ToString(SaveOptions.DisableFormatting));
                var diagnostics = plan.Diagnostics.ToList();
                if (removed > 0) diagnostics.Add(new("ed.settings.normalized", "Removed " + removed + " identical duplicate native setting(s). Conflicting values are never chosen automatically.", false));
                return new(files, diagnostics);
            }
            catch (InvalidOperationException error)
            {
                return new(new Dictionary<string, string>(), [new("ed.export.name", error.Message, true)]);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || name != name.Trim() || name.EndsWith('.')
                || name.Any(char.IsControl) || name.IndexOfAny(['/', '\\', ':', '<', '>', '"', '|', '*', '?']) >= 0 || name.Contains("..", StringComparison.Ordinal))
                throw new InvalidOperationException("Choose an ED preset name of 1–100 characters without path characters, trailing dots or surrounding whitespace.");
            var stem = name.Split('.')[0].ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" || Regex.IsMatch(stem, @"^(COM|LPT)[1-9¹²³]$"))
                throw new InvalidOperationException("Choose an ED preset name that is not a reserved Windows filename.");
        }
        #endregion END private helpers
    }
}

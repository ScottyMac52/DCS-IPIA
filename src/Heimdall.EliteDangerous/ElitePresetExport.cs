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
                Heimdall.Application.Games.GameConfigurationName.Validate(name);
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
    }
}

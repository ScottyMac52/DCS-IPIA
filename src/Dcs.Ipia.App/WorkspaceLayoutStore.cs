using System.IO;
using System.Text.Json;

namespace DcsIpia.Presentation
{

    /// <summary>User-local presentation preferences, independent of module data and appearance entitlements.</summary>
    internal static class WorkspaceLayoutStore
    {
        internal static string FilePath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DCS-IPIA", "workspace-layout.json");

        internal static double Read(string key, double fallback, double minimum, double maximum)
        {
            var values = Load();
            return values.TryGetValue(key, out var value) && double.IsFinite(value)
                ? Math.Clamp(value, minimum, maximum) : fallback;
        }

        internal static void Write(string key, double value)
        {
            if (!double.IsFinite(value)) return;
            try
            {
                var values = Load();
                values[key] = value;
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var temporary = FilePath + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(values));
                File.Move(temporary, FilePath, overwrite: true);
            }
            catch (IOException) { /* An unavailable preferences file must not block editing. */ }
            catch (UnauthorizedAccessException) { }
        }

        private static Dictionary<string, double> Load()
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(FilePath)) ?? [];
            }
            catch (IOException) { return []; }
            catch (UnauthorizedAccessException) { return []; }
            catch (JsonException) { return []; }
        }
    }
}

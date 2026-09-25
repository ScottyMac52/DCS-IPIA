using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DcsConsumerScaffold.Models;

namespace DcsConsumerScaffold.Services;

public sealed class ModuleUiLayerOverlay
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; } = 2;
    [JsonPropertyName("assignments")] public List<DcsCommandAssignment> Assignments { get; set; } = [];
    [JsonPropertyName("labels")] public Dictionary<string, string> Labels { get; set; } = [];
    [JsonPropertyName("modifierOverrides")] public List<PreviewModifier> ModifierOverrides { get; set; } = [];
    [JsonPropertyName("removedModifiers")] public List<string> RemovedModifiers { get; set; } = [];
    [JsonPropertyName("removedProfiles")] public List<string> RemovedProfiles { get; set; } = [];
}

public static class ModuleUiLayerOverlayService
{
    public static string PathFor(string repository) => Path.Combine(repository, "config", "ui-layer-overlay.json");

    public static ModuleUiLayerOverlay Load(string repository)
    {
        var path = PathFor(repository);
        if (!File.Exists(path)) return new();
        var result = JsonSerializer.Deserialize<ModuleUiLayerOverlay>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("Invalid module UI Layer overlay.");
        if (result.SchemaVersion is not (1 or 2)) throw new InvalidOperationException("Unsupported module UI Layer overlay version.");
        return result;
    }

    public static List<PreviewModifier> CopyModifiers(IEnumerable<PreviewModifier> modifiers) =>
        modifiers.Select(item => new PreviewModifier { Name = item.Name, Device = item.Device, Key = item.Key,
            Mode = item.Mode, DeviceId = item.DeviceId, SemanticModifier = item.SemanticModifier }).ToList();

    public static ModuleUiLayerOverlay Capture(ModuleUiLayerOverlay loaded,
        IEnumerable<PreviewModifier> baseline, IEnumerable<PreviewModifier> current,
        IEnumerable<PreviewDevice> devices, IEnumerable<DcsCommandAssignment> pending,
        IReadOnlyDictionary<string, string> labels)
    {
        // Keep only explicit differences. Unedited defaults continue to inherit global changes.
        var result = JsonSerializer.Deserialize<ModuleUiLayerOverlay>(JsonSerializer.Serialize(loaded))!;
        result.SchemaVersion = 2;
        var originals = baseline.ToDictionary(item => item.Name!, StringComparer.Ordinal);
        var effective = current.ToDictionary(item => item.Name!, StringComparer.Ordinal);
        foreach (var name in originals.Keys.Where(name => !effective.ContainsKey(name)))
        {
            if (!result.RemovedModifiers.Contains(name)) result.RemovedModifiers.Add(name);
            result.ModifierOverrides.RemoveAll(item => item.Name == name);
        }
        foreach (var item in effective.Values)
        {
            result.RemovedModifiers.Remove(item.Name!);
            if (originals.TryGetValue(item.Name!, out var original) &&
                original.Device == item.Device && original.Key == item.Key && original.Mode == item.Mode &&
                original.SemanticModifier == item.SemanticModifier) continue;
            result.ModifierOverrides.RemoveAll(value => value.Name == item.Name);
            result.ModifierOverrides.AddRange(CopyModifiers([item]));
        }
        foreach (var device in devices.Where(item => item.RemoveRequested && !string.IsNullOrWhiteSpace(item.ProfileFile)))
            if (!result.RemovedProfiles.Contains(device.ProfileFile!, StringComparer.OrdinalIgnoreCase))
                result.RemovedProfiles.Add(device.ProfileFile!);
        foreach (var assignment in pending)
        {
            result.Assignments.RemoveAll(item => item.ProfileFile == assignment.ProfileFile && item.Section == assignment.Section &&
                item.Key == assignment.Key && item.Reformers.OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(assignment.Reformers.OrderBy(value => value, StringComparer.Ordinal)));
            result.Assignments.Add(assignment);
        }
        foreach (var label in labels) result.Labels[label.Key] = label.Value;
        return result;
    }

    public static void Save(string repository, ModuleUiLayerOverlay overlay)
    {
        var path = PathFor(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(overlay, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

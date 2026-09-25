using System.Diagnostics;
using System.IO;
using System.Text.Json;
using DcsConsumerScaffold.Models;

namespace DcsConsumerScaffold.Services;

/// <summary>Loads DCS-Common hardware, functions, and authoring operations.</summary>
public sealed class IpiAuthoringService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    #region BEGIN Public
    /// <summary>Reads or writes DCS-Common authoring data.</summary>
    public static IReadOnlyList<IpiHardwareChoice> Hardware(string commonRoot)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(commonRoot, "assets", "shared", "hardware", "manifest.json")));
        return document.RootElement.GetProperty("devices").EnumerateArray().SelectMany(device =>
        {
            var id = device.GetProperty("id").GetString()!;
            var label = device.GetProperty("label").GetString()!;
            var ids = new List<string> { id };
            if (device.TryGetProperty("aliases", out var aliases)) ids.AddRange(aliases.EnumerateArray().Select(item => item.GetString()!));
            return ids.Select(value => new IpiHardwareChoice { DeviceId = value, Label = label });
        }).OrderBy(item => item.Label).ThenBy(item => item.DeviceId).ToList();
    }

    /// <summary>Reads or writes DCS-Common authoring data.</summary>
    public static IReadOnlyList<IpiUiFunctionChoice> UiFunctions(string commonRoot)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(commonRoot, "assets", "shared", "ui-layer", "functions.json")));
        return document.RootElement.GetProperty("functions").EnumerateArray().Select(item => new IpiUiFunctionChoice
        {
            Id = item.GetProperty("id").GetString()!, Command = item.GetProperty("command").GetString()!,
            Label = item.GetProperty("label").GetString()!, Category = item.GetProperty("category").GetString()!,
        }).OrderBy(item => item.Category).ThenBy(item => item.Label).ToList();
    }

    /// <summary>Reads or writes DCS-Common authoring data.</summary>
    public static async Task<IReadOnlyList<IpiSharedControlChoice>> SharedControlsAsync(string commonRoot, string deviceId,
        CancellationToken cancellationToken = default)
    {
        var script = Path.Combine(commonRoot, "scripts", "render-interactive-device.mjs");
        var png = Path.Combine(Path.GetTempPath(), $"dcs-device-{Guid.NewGuid():N}.png");
        try
        {
            var start = new ProcessStartInfo("node") { WorkingDirectory = commonRoot, RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(script); start.ArgumentList.Add(deviceId); start.ArgumentList.Add(png);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Node.");
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0) throw new InvalidOperationException(await stderr);
            using var document = JsonDocument.Parse(await stdout);
            var controls = document.RootElement.EnumerateObject().First(x=>x.Name.Equals("controls",StringComparison.OrdinalIgnoreCase)).Value;
            string Field(JsonElement item,string name) => item.EnumerateObject()
                .First(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase)).Value.GetString() ?? "";
            return controls.EnumerateArray().Where(item=>!item.EnumerateObject().Any(x=>x.Name.Equals("shifted",StringComparison.OrdinalIgnoreCase)&&x.Value.ValueKind==JsonValueKind.True))
                .Select(item=>new IpiSharedControlChoice {Id=Field(item,"id"),Key=Field(item,"key"),
                    Type=Field(item,"type"),HardwareLabel=Field(item,"hardwareLabel")})
                .OrderBy(item=>item.HardwareLabel).ThenBy(item=>item.Id).ToList();
        }
        finally { try { File.Delete(png); } catch { /* best effort */ } }
    }

    /// <summary>Reads or writes DCS-Common authoring data.</summary>
    public static async Task<IReadOnlyList<IpiUiLayerBindingChoice>> UiLayerBindingsAsync(string commonRoot,
        CancellationToken cancellationToken = default)
    {
        var document = await UiLayerCatalogService.InspectAsync(commonRoot, cancellationToken);
        var functions = UiFunctions(commonRoot).ToDictionary(item => item.Id, StringComparer.Ordinal);
        return document.Bindings.Where(item => item.DeviceId is not null && item.FunctionId is not null && item.ControlId is not null)
            .Select(item => new IpiUiLayerBindingChoice
            {
                BindingId = item.BindingId, DeviceId = item.DeviceId!, DeviceInstance = item.DeviceInstance,
                FunctionId = item.FunctionId!, FunctionLabel = functions.GetValueOrDefault(item.FunctionId!)?.Label ?? item.Name,
                ControlId = item.ControlId!, HardwareLabel = item.HardwareLabel ?? item.ControlId!, Modifiers = [.. item.Modifiers],
            }).OrderBy(item => item.DeviceId).ThenBy(item => item.DeviceInstance).ThenBy(item => item.FunctionLabel).ToList();
    }

    /// <summary>Reads or writes DCS-Common authoring data.</summary>
    public static Task<IpiAuthoringResult> InitializeAsync(string commonRoot, object request, CancellationToken cancellationToken = default) =>
        RunAsync<IpiAuthoringResult>(commonRoot, "initialize", request, cancellationToken);

    /// <summary>Reads or writes DCS-Common authoring data.</summary>
    public static Task<IpiModuleAuthoringState> InspectModuleAsync(string commonRoot, string repositoryRoot, string inputModuleId,
        CancellationToken cancellationToken = default) =>
        RunAsync<IpiModuleAuthoringState>(commonRoot, "inspect-module", new { repositoryRoot, inputModuleId }, cancellationToken);

    /// <summary>Reads or writes DCS-Common authoring data.</summary>
    public static Task<IpiAuthoringResult> SaveModuleAsync(string commonRoot, object request, CancellationToken cancellationToken = default) =>
        RunAsync<IpiAuthoringResult>(commonRoot, "save-module", request, cancellationToken);

    #endregion END Public
    #region BEGIN private helpers
    private static async Task<T> RunAsync<T>(string commonRoot, string command, object request, CancellationToken cancellationToken) where T : class
    {
        var requestPath = Path.Combine(Path.GetTempPath(), $"ipi-authoring-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request), cancellationToken);
            var start = new ProcessStartInfo("node") { WorkingDirectory = commonRoot, RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(Path.Combine(commonRoot, "scripts", "ipi-authoring.mjs"));
            start.ArgumentList.Add(command);
            start.ArgumentList.Add(requestPath);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Node.");
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await stdout;
            var error = await stderr;
            if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);
            return JsonSerializer.Deserialize<T>(output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Last(), JsonOptions)
                ?? throw new InvalidOperationException("IPI authoring returned no result.");
        }
        finally { try { File.Delete(requestPath); } catch { /* best effort */ } }
    }
    #endregion END private helpers
}

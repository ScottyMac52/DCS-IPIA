using System.Text.Json;
using Heimdall.Application;
using Heimdall.Domain;
using Heimdall.Domain.MultiSim;
using PortableModifier = Heimdall.Domain.MultiSim.Modifier;

namespace Heimdall.Dcs
{
    /// <summary>Maps DCS assignment semantics without flattening native filters or confusing catalog and instance identities.</summary>
    internal static class DcsSimProfileProjection
    {
        private static readonly JsonSerializerOptions NativeOptions = new() { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
        #region BEGIN Public
        /// <summary>Builds an explicitly scoped command identity, including key/axis section.</summary>
        public static string CommandId(string context, BindingSection section, string native) =>
            "dcs:" + Uri.EscapeDataString(context) + ":" + section + ":" + Uri.EscapeDataString(native);
        /// <summary>Projects one native command into its owning context.</summary>
        public static SimCommand Command(string context, string id, string name, bool axis, string category = "DCS") => new()
        {
            Id = CommandId(context, axis ? BindingSection.Axis : BindingSection.Key, id),
            DisplayName = name,
            Type = axis ? CommandType.Axis : CommandType.Button,
            Category = category,
            IsModuleSpecific = context != "UiLayer",
            Context = context,
            Metadata = { ["nativeId"] = id }
        };
        /// <summary>Creates a physical instance projection from its exact captured filename.</summary>
        public static LogicalDevice Device(string path, string context) => new()
        {
            LogicalId = path,
            ContextId = context,
            Identity = new()
            {
                WindowsName = DcsPhysicalIdentity.FromFileName(Path.GetFileName(path)) is null
                    ? Path.GetFileName(path)[..^9] : DcsPhysicalIdentity.NameFromFileName(Path.GetFileName(path)),
                InstanceId = DcsPhysicalIdentity.FromFileName(Path.GetFileName(path))?.ToString("D"),
                SimSpecificIds = { ["dcs.path"] = path }
            }
        };
        /// <summary>Projects an active binding, retaining its exact sparse native filter as JSON.</summary>
        public static void Add(LogicalDevice device, DcsBinding binding)
        {
            var command = CommandId(device.ContextId, binding.Section, binding.CommandId);
            var modifiers = binding.Reformers.Select(x => new PortableModifier { Source = x }).ToList();
            if (binding.Section == BindingSection.Key)
                device.Buttons.Add(new() { SourceButton = binding.InputKey, CommandId = command, Modifiers = modifiers });
            else
                device.Axes.Add(new()
                {
                    SourceAxis = binding.InputKey,
                    CommandId = command,
                    Modifiers = modifiers,
                    Deadzone = (float)(binding.Filter?.Deadzone ?? 0),
                    Invert = binding.Filter?.Invert ?? false,
                    Curve = binding.Filter?.Curvature?.Count > 0 ? CurveType.Custom : CurveType.Linear,
                    SimSpecific = { ["dcs.filter"] = JsonSerializer.SerializeToElement(binding.Filter) }
                });
        }
        /// <summary>Restores validated native assignments using only pinned scoped commands and modifiers.</summary>
        public static IReadOnlyList<DcsBinding> Bindings(LogicalDevice device, IReadOnlyList<SimCommand> catalog,
            IReadOnlyList<DcsModifier> modifiers, Guid profileId)
        {
            if (device.Buttons is null || device.Axes is null)
                throw new InvalidDataException(device.LogicalId + ": binding collections must not be null.");
            var result = new List<DcsBinding>();
            foreach (var button in device.Buttons)
                result.Add(ReadButton(button, device, catalog, modifiers, profileId));
            foreach (var axis in device.Axes)
            {
                var filter = ReadAxisFilter(axis, device.LogicalId);
                result.Add(Create(device, catalog, modifiers, profileId, axis.SourceAxis, axis.CommandId, axis.Modifiers, BindingSection.Axis, filter));
            }
            if (result.GroupBy(x => x.Section + "\0" + x.InputKey + "\0" + string.Join("+", x.Reformers)).Any(x => x.Count() > 1))
                throw new InvalidDataException(device.LogicalId + ": multiple commands occupy the same physical input/chord.");
            return result;

        }
        /// <summary>Rejects non-finite or out-of-range native percentage tuning without normalizing sparse values.</summary>
        public static void ValidateFilter(AxisFilter? filter)
        {
            if (filter is null) return;
            var scalars = new[] { filter.Deadzone, filter.SaturationX, filter.SaturationY, filter.HardwareDetentAB, filter.HardwareDetentMax };
            if (scalars.Any(x => x is { } value && !double.IsFinite(value)) || filter.Curvature?.Any(x => !double.IsFinite(x) || x is < -100 or > 100) == true)
                throw new InvalidDataException("DCS filter values must be finite; curvature must be -100..100.");
            if (new[] { filter.Deadzone, filter.SaturationX, filter.SaturationY }.Any(x => x is < 0 or > 100))
                throw new InvalidDataException("DCS deadzone and saturation use percentage points, 0..100.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DcsBinding ReadButton(ButtonBinding button, LogicalDevice device, IReadOnlyList<SimCommand> catalog,
            IReadOnlyList<DcsModifier> modifiers, Guid profileId)
        {
            if (button is null || button.Mode != BindingMode.Press || button.SimSpecific is null || button.SimSpecific.Count != 0)
                throw new InvalidDataException(device.LogicalId + ": only native Press bindings without unknown extensions are supported.");
            return Create(device, catalog, modifiers, profileId, button.SourceButton, button.CommandId, button.Modifiers, BindingSection.Key, null);
        }
        private static AxisFilter? ReadAxisFilter(AxisBinding axis, string path)
        {
            if (axis is null || !float.IsFinite(axis.Deadzone) || axis.Deadzone is < 0 or > 100 || axis.Sensitivity != 1 ||
                axis.SimSpecific is null || axis.SimSpecific.Keys.Any(x => x != "dcs.filter"))
                throw new InvalidDataException(path + ": axis deadzone must be finite, 0..100; sensitivity must be 1; unknown extensions are unsupported.");
            AxisFilter? filter = axis.SimSpecific.TryGetValue("dcs.filter", out var extension)
                ? JsonSerializer.SerializeToElement(extension).Deserialize<AxisFilter>(NativeOptions) : null;
            if (axis.Curve != (filter?.Curvature?.Count > 0 ? CurveType.Custom : CurveType.Linear))
                throw new InvalidDataException(path + ": portable curve conversion is unsupported; edit the exact DCS native filter instead.");
            if (axis.Deadzone != (float)(filter?.Deadzone ?? 0)) filter = (filter ?? new()) with { Deadzone = axis.Deadzone };
            if (axis.Invert != (filter?.Invert ?? false)) filter = (filter ?? new()) with { Invert = axis.Invert };
            ValidateFilter(filter);
            return filter;
        }
        private static DcsBinding Create(LogicalDevice device, IReadOnlyList<SimCommand> catalog,
            IReadOnlyList<DcsModifier> modifiers, Guid profileId, string input, string commandId,
            List<PortableModifier> chord, BindingSection section, AxisFilter? filter)
        {
            var command = catalog.SingleOrDefault(x => x.Id == commandId && x.Context == device.ContextId &&
                (x.Type == CommandType.Axis) == (section == BindingSection.Axis))
                ?? throw new InvalidDataException(device.LogicalId + ": command is absent from the pinned context/section catalog: " + commandId);
            if (chord is null || chord.Any(x => x is null || x.Value is not null || !modifiers.Any(m => m.Name == x.Source)))
                throw new InvalidDataException(device.LogicalId + ": modifier chord has an unknown name or unsupported threshold.");
            return new(profileId, section, command.Metadata["nativeId"], command.DisplayName,
                BindingDisposition.Added, input, chord.Select(x => x.Source), filter);
        }
        #endregion END private helpers
    }
}

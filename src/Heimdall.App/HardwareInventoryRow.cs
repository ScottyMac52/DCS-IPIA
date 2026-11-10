using Heimdall.Application;
using Heimdall.Dcs;
using System.Text.Json;

namespace Heimdall.App
{
    /// <summary>Editable review state for one physical unit, independent of module memberships.</summary>
    public sealed class HardwareInventoryRow
    {
        private readonly IReadOnlyList<string> _templates;
        private readonly bool _reviewMapping;
        private const string GenericMapping = "Generic — actual controls";
        /// <summary>Creates a row preserving saved labels and selections.</summary>
        /// <param name="candidate">Saved and observed evidence.</param>
        /// <param name="sources">Optional bundled mapping metadata.</param>
        /// <param name="reviewMapping">Whether setup requires an explicit preferred mapping choice.</param>
        /// <param name="definition">Pinned controls for an offline unit.</param>
        public HardwareInventoryRow(HardwareInventoryCandidate candidate, IReadOnlyDictionary<string, string> sources, bool reviewMapping = false,
            HardwareDefinition? definition = null)
        {
            Candidate = candidate;
            _reviewMapping = reviewMapping;
            Selected = candidate.Saved?.Selected ?? false;
            Label = candidate.Saved?.Label ?? candidate.Observed!.Device.Name;
            Role = candidate.Saved?.Role ?? "";
            _templates = candidate.Observed is null ? candidate.Saved?.TemplateCandidates ?? [] :
                BundledHardwareCapabilityMatcher.Candidates(sources, candidate.Observed);
            var hardware = sources.TryGetValue("config/ipia-hardware.json", out var catalog)
                ? JsonSerializer.Deserialize<WorkspaceHardware[]>(catalog) ?? [] : [];
            if (candidate.Observed?.Capabilities is { } caps)
                definition = new(ControllerCapabilityMapper.Fingerprint(caps), caps);
            if (reviewMapping && definition is not null)
            {
                var unit = candidate.Saved ?? new HardwareInventoryInstance(candidate.PhysicalGuid,
                    candidate.Observed!.Device.Name, definition.Id, Label, Role, Selected, []);
                _templates = DcsInventoryHardwareAuthoring.Drawings(hardware, new(unit, definition)).Select(x => x.DeviceId).ToArray();
            }
            MappingChoices = new[] { GenericMapping }.Concat(_templates).Distinct(StringComparer.Ordinal).ToArray();
            MappingOptions = MappingChoices.Select(id => new KeyValuePair<string, string>(id,
                hardware.FirstOrDefault(x => x.DeviceId == id)?.Label ?? id)).ToArray();
            MappingChoice = candidate.Saved?.PreferredHardwareId is { } preferred && MappingChoices.Contains(preferred) ? preferred :
                candidate.Saved?.TemplateCandidates.Count == 1 && MappingChoices.Contains(candidate.Saved.TemplateCandidates[0])
                ? candidate.Saved.TemplateCandidates[0] : GenericMapping;
        }
        #region BEGIN Public
        /// <summary>Gets immutable review evidence.</summary>
        public HardwareInventoryCandidate Candidate { get; }
        /// <summary>Gets or sets explicit selection.</summary>
        public bool Selected { get; set; }
        /// <summary>Gets or sets the distinct instance label.</summary>
        public string Label { get; set; }
        /// <summary>Gets or sets the optional role.</summary>
        public string Role { get; set; }
        /// <summary>Gets or sets explicit adoption of changed controls.</summary>
        public bool AdoptChangedCapabilities { get; set; }
        /// <summary>Gets generic support and compatible bundled candidates for explicit review.</summary>
        public IReadOnlyList<string> MappingChoices { get; }
        /// <summary>Gets friendly shared drawing labels keyed by stable catalog identity.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> MappingOptions { get; }
        /// <summary>Gets or sets a preferred mapping; existing definitions stay pinned until controls are explicitly adopted.</summary>
        public string MappingChoice { get; set; }
        /// <summary>Gets exact physical identity.</summary>
        public string Identity => Candidate.PhysicalGuid.ToString("D").ToUpperInvariant();
        /// <summary>Gets the native display name.</summary>
        public string NativeName => Candidate.Observed?.Device.Name ?? Candidate.Saved!.NativeName;
        /// <summary>Gets observed connectivity and review state.</summary>
        public string Status => Candidate.Observed is null ? "Disconnected / not rescanned" :
            Candidate.Observed.Error is not null ? "Connected · enumeration failed" :
            Candidate.Changed ? "Connected · controls changed (review required)" : "Connected";
        /// <summary>Gets actual enumerated control counts.</summary>
        public string Controls => Candidate.Observed?.Capabilities is { } caps ?
            $"{caps.Inputs.Count(x => x.Kind == ControllerInputKind.Button)} buttons / {caps.Inputs.Count(x => x.Kind == ControllerInputKind.Axis)} axes / {caps.Inputs.Count(x => x.Kind == ControllerInputKind.Hat)} hats" :
            "Saved definition retained; rescan to inspect";
        /// <summary>Gets compatible candidates without automatically adopting artwork.</summary>
        public string Mapping => _templates.Count switch
        {
            0 => "Generic support · actual enumerated controls",
            1 => "Compatible candidate: " + _templates[0] + " · confirm artwork later",
            _ => "Ambiguous candidates: " + string.Join(", ", _templates)
        };
        /// <summary>Gets controls, diagnostics and physical/virtual discovery policy.</summary>
        public string Details => "Native: " + NativeName + "\nPhysical GUID: " + Identity +
            "\nProduct GUID: " + (Candidate.Observed?.Device.ProductGuid.ToString("D") ?? "Not observed") +
            "\n" + Status + "\n" + Controls + "\n" + Mapping +
            "\n\nNames, roles and the preferred drawing are shared by all games. Game assignments remain separate. Only connected adopted devices are available for new game links and input testing. Saved offline devices and assignments are retained.\n\n" +
            (Candidate.Observed?.Error ?? string.Join("\n", Candidate.Observed?.Capabilities?.Inputs.Select(x =>
                x.DcsKey + " · " + x.Kind + " · " + x.Evidence.Name) ?? [])) +
            "\n" + string.Join("\n", Candidate.Observed?.Capabilities?.Unsupported.Select(x =>
                "Unsupported: " + x.Evidence.Name + " [" + x.Evidence.NativeType + "] — " + x.Reason) ?? []);
        /// <summary>Creates an explicit reviewed edit.</summary>
        /// <returns>Application-layer adoption request.</returns>
        public HardwareInventoryEdit Edit() => new(Candidate, Selected, Label, Role, AdoptChangedCapabilities,
            _reviewMapping ? MappingChoice == GenericMapping ? [] : [MappingChoice] : _templates, _reviewMapping);
        #endregion END Public
    }
}

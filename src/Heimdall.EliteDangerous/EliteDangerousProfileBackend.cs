using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Application.Validation;
using Heimdall.Domain.MultiSim;

namespace Heimdall.EliteDangerous
{
    /// <summary>Translates captured Elite bindings without reading or deploying active game files.</summary>
    public sealed class EliteDangerousProfileBackend : ISimProfileBackend, ISimDeviceEditor, ISimGameExport
    {
        private static readonly JsonSerializerOptions EvidenceOptions = new()
        {
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
        };

        #region BEGIN Public
        /// <summary>Gets the versioned declarative contract packaged from the adapter's validation JSON file.</summary>
        public ProfileValidationDefinition ValidationDefinition => ProfileValidationDefinitions.BuiltIn(SimId);
        /// <summary>Validates without mutation using common rules plus the existing native semantic boundary.</summary>
        public ProfileValidationResult Validate(ProfileDocument profile, ProfileValidationDefinition definition, CancellationToken cancellationToken = default) =>
            ProfileValidationEngine.Validate(this, profile, definition, null, cancellationToken);

        /// <summary>Gets the backend identifier.</summary>
        public string SimId => "elite-dangerous";

        /// <summary>Gets supported native semantics; command toggles remain separate metadata.</summary>
        public SimBackendCapabilities Capabilities => new(1, [BindingMode.Press, BindingMode.Hold], true, true, false);

        /// <summary>Imports one captured 4.x preset and optional preset selector files.</summary>
        public ProfileDocument Import(string contextId, IReadOnlyDictionary<string, string> sources,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(contextId);
            ArgumentNullException.ThrowIfNull(sources);
            var native = EliteNativeDocument.Read(sources);
            var profile = EliteProfileProjection.Create(contextId, native);
            profile.NativeData.Add("ed.v1", JsonSerializer.SerializeToElement(new EliteNativeEvidence(contextId,
                new Dictionary<string, string>(sources, StringComparer.Ordinal))));
            cancellationToken.ThrowIfCancellationRequested();
            return profile;
        }

        /// <summary>Creates an atomic output plan; unsupported edits return no files.</summary>
        public SimProfileExportPlan Export(ProfileDocument profile, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var evidence = ReadEvidence(profile);
                var native = EliteNativeDocument.Read(evidence.Sources);
                var baseline = EliteProfileProjection.Create(evidence.ContextId, native);
                var files = EliteProfileEditor.Export(profile, baseline, native);
                cancellationToken.ThrowIfCancellationRequested();
                return new(files, []);
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or JsonException or System.Xml.XmlException)
            {
                return new(new Dictionary<string, string>(), [new ImportDiagnostic("ed.export.blocked", error.Message, true)]);
            }
        }

        /// <summary>Creates a correctly named ED preset while retaining captured evidence unchanged.</summary>
        public SimProfileExportPlan ExportNamed(ProfileDocument profile, string presetName) => ElitePresetExport.Prepare(Export(profile), presetName);
        /// <summary>Scans every native Device field, including unknown settings and modifier-only dependencies.</summary>
        public IReadOnlyList<SimDeviceUsage> DeviceUsages(ProfileDocument profile)
        {
            var plan = Export(profile);
            if (!plan.Succeeded) throw new InvalidOperationException(string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
            var native = EliteNativeDocument.Read(plan.Files);
            return native.Root.Descendants().Where(x => x.Attribute("Device") is not null)
                .GroupBy(x => EliteProfileProjection.DeviceId(EliteProfileProjection.Identity(x)), StringComparer.Ordinal)
                .Select(group => new SimDeviceUsage(group.Key, EliteProfileProjection.Identity(group.First()), group.Select(node =>
                {
                    var command = node.AncestorsAndSelf().First(x => x.Parent == native.Root);
                    return EliteCommandNames.Display(command.Name.LocalName) + " · " + node.Name.LocalName + " · " + (string?)node.Attribute("Key");
                }).ToArray())).ToArray();
        }

        /// <summary>Returns an independent catalog derived from the captured preset, including unassigned commands.</summary>
        public ICommandCatalog GetCatalog(ProfileDocument profile)
        {
            var evidence = ReadEvidence(profile);
            return new EliteCommandCatalog(evidence.ContextId, EliteNativeDocument.Read(evidence.Sources));
        }

        /// <summary>Resolves only explicit instance evidence, never a product token or an ordinal.</summary>
        public SimDeviceResolution ResolveDevice(PhysicalDeviceIdentity identity, IReadOnlyList<PhysicalDeviceIdentity> connected)
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentNullException.ThrowIfNull(connected);
            if (string.IsNullOrWhiteSpace(identity.InstanceId))
            {
                return new("unresolved", []);
            }
            var matches = connected.Where(x => string.Equals(x.InstanceId, identity.InstanceId, StringComparison.OrdinalIgnoreCase)).ToArray();
            return new(matches.Length switch { 0 => "disconnected", 1 => "resolved", _ => "ambiguous" }, matches);
        }
        /// <summary>Adds one canonical unindexed HID product reference without changing captured devices or native source evidence.</summary>
        public ProfileDocument AddDevice(ProfileDocument profile, DetectedDcsDevice observed)
        {
            ArgumentNullException.ThrowIfNull(observed);
            var token = EliteDeviceReferenceMatcher.ProductToken(observed)
                ?? throw new InvalidOperationException("This controller has no supported native HID product identity.");
            if (!Export(profile).Succeeded) throw new InvalidOperationException("Resolve the existing profile diagnostics before adding a controller.");
            if (profile.Devices.Any(x => EliteDeviceReferenceMatcher.Normalize(x.Identity?.SimSpecificIds.GetValueOrDefault("ed.device")) == token))
                throw new InvalidOperationException("This product already has an ED reference. Link an existing reference; identical controllers require captured ED indices.");
            var candidate = JsonSerializer.Deserialize<ProfileDocument>(JsonSerializer.Serialize(profile))!;
            var identity = new PhysicalDeviceIdentity { SimSpecificIds = new() { ["ed.device"] = token } };
            candidate.Devices.Add(new() { LogicalId = EliteProfileProjection.DeviceId(identity), ContextId = profile.ContextId, Identity = identity });
            if (!Export(candidate).Succeeded) throw new InvalidOperationException("The new native controller reference could not be validated.");
            return candidate;
        }
        /// <summary>Returns a validated copy with owned and modifier-dependent assignments cleared.</summary>
        public ProfileDocument RemoveDevice(ProfileDocument profile, string logicalId) =>
            ValidateChange(EliteControllerEditor.Remove(profile, logicalId));
        /// <summary>Returns a validated copy with compatible bindings moved to an explicit native target.</summary>
        public ProfileDocument ReplaceDevice(ProfileDocument profile, string logicalId, string targetId, IReadOnlyCollection<string> nativeInputs) =>
            ValidateChange(EliteControllerEditor.Replace(profile, logicalId, targetId, nativeInputs));
        #endregion END Public

        #region BEGIN private helpers
        private ProfileDocument ValidateChange(ProfileDocument candidate)
        {
            var plan = Export(candidate);
            if (!plan.Succeeded) throw new InvalidOperationException(string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
            return candidate;
        }
        private static EliteNativeEvidence ReadEvidence(ProfileDocument profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            if (profile.SchemaVersion != 1 || profile.BackendId != "elite-dangerous" || profile.BackendVersion != 1)
            {
                throw new InvalidOperationException("Unsupported Elite profile schema or backend version.");
            }
            if (profile.NativeData is null || profile.NativeData.Count != 1 || !profile.NativeData.TryGetValue("ed.v1", out var data))
            {
                throw new InvalidOperationException("Missing Elite native evidence.");
            }
            var evidence = data.Deserialize<EliteNativeEvidence>(EvidenceOptions) ?? throw new InvalidOperationException("Empty Elite native evidence.");
            if (string.IsNullOrWhiteSpace(evidence.ContextId) || evidence.ContextId != profile.ContextId)
            {
                throw new InvalidOperationException("Elite context does not match captured evidence.");
            }
            return evidence;
        }
        #endregion END private helpers
    }
}

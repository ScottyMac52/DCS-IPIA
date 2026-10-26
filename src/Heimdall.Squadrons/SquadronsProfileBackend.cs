using System.Text.Json;
using System.Text.Json.Serialization;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Provides lossless Squadrons import/export and explicit edits to existing captured joystick records.</summary>
    public sealed class SquadronsProfileBackend : ISimProfileBackend, INativeRecordEditor, ISimCapturedDeviceLinks
    {
        private static readonly JsonSerializerOptions EvidenceOptions = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        #region BEGIN Public
        /// <summary>Gets the supported native filename; this backend never reads active game folders.</summary>
        public const string FileName = "ProfileOptions_profile_synced";
        /// <summary>Gets the owning backend.</summary>
        public string SimId => "star-wars-squadrons";
        /// <summary>Maps the adapter's POV spelling to the shared enumerated physical key; axes and buttons retain their keys.</summary>
        public string PhysicalInputKey(string key) => SquadronsInputCodec.PhysicalKey(key);
        /// <summary>Declares initial semantics; unsupported chord/tuning edits are retained as native evidence only.</summary>
        public SimBackendCapabilities Capabilities => new(1, [BindingMode.Press], false, false, false);
        /// <summary>Imports one captured version four settings file and retains its exact source.</summary>
        public ProfileDocument Import(string contextId, IReadOnlyDictionary<string, string> sources, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); ArgumentException.ThrowIfNullOrWhiteSpace(contextId); ArgumentNullException.ThrowIfNull(sources);
            if (sources.Count != 1 || !sources.TryGetValue(FileName, out var text))
                throw new InvalidOperationException("Capture exactly one ProfileOptions_profile_synced file using its native filename.");
            var document = new SquadronsNativeDocument(text);
            var profile = SquadronsProfileProjection.Create(contextId, document);
            profile.NativeData.Add("squadrons.v1", JsonSerializer.SerializeToElement(new SquadronsNativeEvidence(contextId, text)));
            cancellationToken.ThrowIfCancellationRequested();
            return profile;
        }
        /// <summary>Returns exact reviewed output or blocking diagnostics; arbitrary portable mutations cannot silently disappear.</summary>
        public SimProfileExportPlan Export(ProfileDocument profile, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var evidence = Evidence(profile);
                cancellationToken.ThrowIfCancellationRequested();
                return new(new Dictionary<string, string> { [FileName] = evidence.Text }, []);
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or JsonException)
            { return new(new Dictionary<string, string>(), [new("squadrons.export.blocked", error.Message, true)]); }
        }
        /// <summary>Gets all native records, including keyboard/mouse/gamepad, unassigned and unsupported encodings.</summary>
        public IReadOnlyList<SquadronsBindingRow> ReadBindings(ProfileDocument profile) => new SquadronsNativeDocument(Evidence(profile).Text).Bindings;
        /// <summary>Gets concepts scoped to this captured profile, with native context categories.</summary>
        public ICommandCatalog GetCatalog(ProfileDocument profile)
        {
            var evidence = Evidence(profile);
            return new SquadronsCommandCatalog(evidence.ContextId, new(evidence.Text));
        }
        /// <summary>Rebinds an existing unchorded joystick record to an explicit captured slot; game acquisition is not implied.</summary>
        public ProfileDocument RebindJoystick(ProfileDocument profile, string recordId, int targetSlot, SquadronsInput input)
        {
            var evidence = Evidence(profile); var document = new SquadronsNativeDocument(evidence.Text);
            var row = document.Bindings.SingleOrDefault(x => x.RecordId == recordId);
            if (row is null || row.Type != 3 || row.Fields.Count != 8 || row.Fields["modifier"] != 0 || row.Fields["altbutton"] != 0
                || targetSlot is < 1 or > 10 || !document.Values.TryGetValue("GstInput.JoystickDevice" + targetSlot, out var name) || string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Choose an existing unchorded joystick record and a named captured device slot.");
            var (axis, button) = SquadronsInputCodec.Encode(input); var prefix = "GstKeyBinding." + row.RecordId + ".";
            var text = document.ReplaceValues(new Dictionary<string, int> { [prefix + "axis"] = axis, [prefix + "button"] = button, [prefix + "deviceid"] = targetSlot - 1 });
            // Fail closed if the native writer changed anything beyond the requested record or lost its input encoding.
            var written = new SquadronsNativeDocument(text);
            var updated = written.Bindings.Single(x => x.RecordId == recordId);
            if (updated.DeviceId != targetSlot - 1 || updated.Input != input
                || document.Bindings.Count != written.Bindings.Count
                || document.Bindings.Where(x => x.RecordId != recordId).Any(original =>
                    !written.Bindings.Any(candidate => candidate.RecordId == original.RecordId
                        && candidate.Fields.Count == original.Fields.Count
                        && original.Fields.All(field => candidate.Fields.TryGetValue(field.Key, out var value) && value == field.Value))))
                throw new InvalidOperationException("Squadrons native binding validation failed; no profile changes were applied.");
            return Import(evidence.ContextId, new Dictionary<string, string> { [FileName] = text });
        }
        /// <summary>Resolves an explicitly selected captured device and delegates to the native record editor.</summary>
        public ProfileDocument RebindNative(ProfileDocument profile, string recordId, string logicalDeviceId, string key, CommandType type, int direction)
        {
            Evidence(profile);
            var device = profile.Devices.SingleOrDefault(x => x.LogicalId == logicalDeviceId)
                ?? throw new InvalidOperationException("Choose a captured Squadrons controller slot.");
            var slot = int.Parse(device.Identity!.SimSpecificIds["squadrons.slot"], System.Globalization.CultureInfo.InvariantCulture);
            return RebindJoystick(profile, recordId, slot, new(key, type, direction));
        }
        /// <summary>Allows explicit workspace-only links to named captured slots without inferring hardware from their names.</summary>
        public void ValidateCapturedDeviceLink(ProfileDocument profile, string logicalDeviceId)
        {
            Evidence(profile);
            if (!profile.Devices.Any(x => x.LogicalId == logicalDeviceId))
                throw new InvalidOperationException("Choose a named captured Squadrons slot. To add another slot, bind an input on that device in Squadrons and import a fresh capture.");
        }
        /// <summary>Removes an unreferenced slot without reindexing or deleting any bindings.</summary>
        public ProfileDocument RemoveUnusedCapturedDevice(ProfileDocument profile, string logicalDeviceId)
        {
            var evidence = Evidence(profile);
            var device = profile.Devices.SingleOrDefault(x => x.LogicalId == logicalDeviceId)
                ?? throw new InvalidOperationException("Choose a named captured Squadrons slot.");
            var slot = int.Parse(device.Identity!.SimSpecificIds["squadrons.slot"], System.Globalization.CultureInfo.InvariantCulture);
            var document = new SquadronsNativeDocument(evidence.Text);
            if (document.Bindings.Any(row => row.Type == 3 && row.DeviceId == slot - 1))
                throw new InvalidOperationException("Cannot remove a slot with native joystick bindings. Reassign its bindings first.");
            var text = document.RemoveSetting("GstInput.JoystickDevice" + slot);
            var result = Import(evidence.ContextId, new Dictionary<string, string> { [FileName] = text });
            if (new SquadronsNativeDocument(text).Bindings.Count != document.Bindings.Count || result.Devices.Any(x => x.LogicalId == logicalDeviceId))
                throw new InvalidOperationException("Captured slot removal validation failed.");
            return result;
        }
        /// <summary>Resolves explicit physical instance identity only; saved names, duplicates and ordinals never imply a connection.</summary>
        public SimDeviceResolution ResolveDevice(PhysicalDeviceIdentity identity, IReadOnlyList<PhysicalDeviceIdentity> connected)
        {
            ArgumentNullException.ThrowIfNull(identity); ArgumentNullException.ThrowIfNull(connected);
            if (string.IsNullOrWhiteSpace(identity.InstanceId)) return new("unresolved", []);
            var matches = connected.Where(x => string.Equals(x.InstanceId, identity.InstanceId, StringComparison.OrdinalIgnoreCase)).ToArray();
            return new(matches.Length switch { 0 => "disconnected", 1 => "resolved", _ => "ambiguous" }, matches);
        }
        #endregion END Public
        #region BEGIN private helpers
        private SquadronsNativeEvidence Evidence(ProfileDocument profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            if (profile.BackendId != SimId || profile.SchemaVersion != 1 || profile.BackendVersion != 1
                || profile.NativeData is null || profile.NativeData.Count != 1 || !profile.NativeData.TryGetValue("squadrons.v1", out var element))
                throw new InvalidOperationException("Missing, foreign or unsupported Squadrons native evidence.");
            var evidence = element.Deserialize<SquadronsNativeEvidence>(EvidenceOptions)
                ?? throw new InvalidOperationException("Missing Squadrons source evidence.");
            var baseline = Import(evidence.ContextId, new Dictionary<string, string> { [FileName] = evidence.Text });
            if (JsonSerializer.Serialize(profile) != JsonSerializer.Serialize(baseline))
                throw new InvalidOperationException("Direct portable edits are not supported yet. Use the reviewed Squadrons native record editor.");
            return evidence;
        }
        #endregion END private helpers
    }
}

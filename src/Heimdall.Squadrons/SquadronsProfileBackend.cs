using System.Text.Json;
using System.Text.Json.Serialization;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Provides lossless Squadrons import/export and explicit edits to existing captured joystick records.</summary>
    public sealed class SquadronsProfileBackend : ISimProfileBackend, INativeRecordCreator, INativeAxisPairEditor, ISimCapturedDeviceLinks
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
        /// <summary>Rebinds both captured axis directions together without changing their
        /// independent identifiers, negate values or unrelated native records.</summary>
        public ProfileDocument RebindAxisPair(ProfileDocument profile, string positiveRecordId,
            string negativeRecordId, string logicalDeviceId, string physicalAxisKey)
        {
            var evidence = Evidence(profile);
            var device = profile.Devices.SingleOrDefault(x => x.LogicalId == logicalDeviceId)
                ?? throw new InvalidOperationException("Select a captured Squadrons controller slot.");
            var slot = int.Parse(device.Identity!.SimSpecificIds["squadrons.slot"],
                System.Globalization.CultureInfo.InvariantCulture);
            var document = new SquadronsNativeDocument(evidence.Text);
            var positive = document.Bindings.SingleOrDefault(x => x.RecordId == positiveRecordId);
            var negative = document.Bindings.SingleOrDefault(x => x.RecordId == negativeRecordId);
            ValidateAxisPair(document, positive, negative, slot);
            RejectAxisCollision(document, positiveRecordId, negativeRecordId, slot, physicalAxisKey, positive!.Concept);
            var plus = new SquadronsInput(physicalAxisKey, CommandType.Axis, 1);
            var minus = new SquadronsInput(physicalAxisKey, CommandType.Axis, -1);
            var (plusAxis, plusButton) = SquadronsInputCodec.Encode(plus);
            var (minusAxis, minusButton) = SquadronsInputCodec.Encode(minus);
            var changes = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (record, axis, button) in new[]
            {
                (positive!, plusAxis, plusButton), (negative!, minusAxis, minusButton)
            })
            {
                var prefix = "GstKeyBinding." + record.RecordId + ".";
                changes[prefix + "axis"] = axis;
                changes[prefix + "button"] = button;
                changes[prefix + "deviceid"] = slot - 1;
            }
            var text = document.ReplaceValues(changes);
            var written = new SquadronsNativeDocument(text);
            ValidateAxisPairWrite(document, written, positiveRecordId, negativeRecordId, slot, plus, minus, changes);
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
        /// <summary>Adapter-neutral creation entry point used by the game workspace.</summary>
        public ProfileDocument AddNativeBinding(ProfileDocument profile, string concept,
            string logicalDeviceId, string key, CommandType type, int direction) =>
            AddJoystickBinding(profile, concept, logicalDeviceId, new(key, type, direction));
        /// <summary>Adds a new record for a captured command using its verified native identifier.
        /// All other native records and scalar settings must remain identical.</summary>
        public ProfileDocument AddJoystickBinding(ProfileDocument profile, string concept,
            string logicalDeviceId, SquadronsInput input)
        {
            var evidence = Evidence(profile);
            var device = profile.Devices.SingleOrDefault(x => x.LogicalId == logicalDeviceId)
                ?? throw new InvalidOperationException("Select a captured Squadrons joystick slot.");
            if (!GetCatalog(profile).Commands.Any(x => x.Metadata["squadrons.concept"] == concept))
                throw new InvalidOperationException("Unknown Squadrons command concept.");
            var slot = int.Parse(device.Identity!.SimSpecificIds["squadrons.slot"],
                System.Globalization.CultureInfo.InvariantCulture);
            var original = new SquadronsNativeDocument(evidence.Text);
            if (original.Bindings.Any(x => x.Concept == concept && x.Type == 3 &&
                x.DeviceId == slot - 1 && x.Input == input))
                throw new InvalidOperationException("This input is already assigned to the selected command on this controller.");
            var written = new SquadronsNativeDocument(original.AppendJoystickRecord(concept, slot, input));
            if (written.Bindings.Count != original.Bindings.Count + 1 ||
                original.Bindings.Any(old => !written.Bindings.Any(next =>
                    next.RecordId == old.RecordId && next.Fields.Count == old.Fields.Count &&
                    old.Fields.All(f => next.Fields.TryGetValue(f.Key, out var v) && v == f.Value))) ||
                written.Bindings.ExceptBy(original.Bindings.Select(x => x.RecordId), x => x.RecordId)
                    .SingleOrDefault() is not { } added ||
                added.Concept != concept || added.Type != 3 || added.DeviceId != slot - 1 || added.Input != input)
                throw new InvalidOperationException("Native record creation failed round-trip validation.");
            return Import(evidence.ContextId, new Dictionary<string, string> { [FileName] = written.Text });
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
        /// <summary>Counts all indexed records, including unknown joystick encodings, for one captured slot.</summary>
        public int CountCapturedAssignments(ProfileDocument profile, string logicalDeviceId)
        {
            var evidence = Evidence(profile);
            var device = profile.Devices.Single(x => x.LogicalId == logicalDeviceId);
            var slot = int.Parse(device.Identity!.SimSpecificIds["squadrons.slot"], System.Globalization.CultureInfo.InvariantCulture);
            return new SquadronsNativeDocument(evidence.Text).Bindings.Count(x => x.Type == 3 && x.DeviceId == slot - 1);
        }
        /// <summary>Clears one selected joystick binding while keeping all other native records unchanged.</summary>
        public ProfileDocument ClearCapturedRecord(ProfileDocument profile, string recordId)
        {
            var evidence = Evidence(profile);
            var source = new SquadronsNativeDocument(evidence.Text);
            var target = source.Bindings.SingleOrDefault(x => x.RecordId == recordId);
            if (target is null || target.Type != 3)
                throw new InvalidOperationException("Choose an existing joystick assignment.");
            var updated = new SquadronsNativeDocument(source.RemoveJoystickRecord(recordId));
            if (updated.Bindings.Count != source.Bindings.Count - 1 ||
                source.Bindings.Where(x => x.RecordId != recordId).Any(row =>
                    !updated.Bindings.Any(other => other.RecordId == row.RecordId
                    && other.Fields.Count == row.Fields.Count
                    && row.Fields.All(f => other.Fields.TryGetValue(f.Key, out var v) && v == f.Value))))
                throw new InvalidOperationException("Single native assignment clear failed validation.");
            return Import(evidence.ContextId, new Dictionary<string, string> { [FileName] = updated.Text });
        }
        /// <summary>Clears every native joystick binding owned by the captured slot, preserving all other settings.</summary>
        public ProfileDocument ClearCapturedAssignments(ProfileDocument profile, string logicalDeviceId)
        {
            var evidence = Evidence(profile);
            var device = profile.Devices.Single(x => x.LogicalId == logicalDeviceId);
            var slot = int.Parse(device.Identity!.SimSpecificIds["squadrons.slot"], System.Globalization.CultureInfo.InvariantCulture);
            var original = new SquadronsNativeDocument(evidence.Text);
            var updated = new SquadronsNativeDocument(original.RemoveJoystickBindings(slot));
            var expected = original.Bindings.Where(x => x.Type != 3 || x.DeviceId != slot - 1).ToArray();
            if (updated.Bindings.Count != expected.Length || expected.Any(row => !updated.Bindings.Any(other =>
                other.RecordId == row.RecordId && row.Fields.Count == other.Fields.Count
                && row.Fields.All(field => other.Fields.TryGetValue(field.Key, out var number) && number == field.Value))))
                throw new InvalidOperationException("Native assignment clearing failed validation.");
            return Import(evidence.ContextId, new Dictionary<string, string> { [FileName] = updated.Text });
        }
        /// <summary>Reindexes every captured slot and joystick binding atomically without changing input codes.</summary>
        public ProfileDocument ReorderCapturedSlots(ProfileDocument profile, IReadOnlyList<string> order)
        {
            var evidence = Evidence(profile);
            var slots = order.Select(id => profile.Devices.Single(x => x.LogicalId == id))
                .Select(x => int.Parse(x.Identity!.SimSpecificIds["squadrons.slot"], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var document = new SquadronsNativeDocument(evidence.Text);
            var updated = new SquadronsNativeDocument(document.ReorderJoystickSlots(slots));
            if (updated.Bindings.Count != document.Bindings.Count ||
                document.Bindings.Any(row => !updated.Bindings.Any(other =>
                    other.RecordId == row.RecordId && other.Fields.Count == row.Fields.Count
                    && row.Fields.All(field => other.Fields.TryGetValue(field.Key, out var value) &&
                        value == (field.Key == "deviceid" && row.Type == 3 ? Array.IndexOf(slots, row.DeviceId + 1) : field.Value)))))
                throw new InvalidOperationException("Native slot permutation failed validation.");
            return Import(evidence.ContextId, new Dictionary<string, string> { [FileName] = updated.Text });
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
        private static void ValidateAxisPair(SquadronsNativeDocument document,
            SquadronsBindingRow? positive, SquadronsBindingRow? negative, int slot)
        {
            if (positive is null || negative is null || positive.RecordId == negative.RecordId ||
                positive.Concept != negative.Concept || positive.Type != 3 || negative.Type != 3 ||
                positive.Fields.Count != 8 || negative.Fields.Count != 8 ||
                positive.Fields["modifier"] != 0 || negative.Fields["modifier"] != 0 ||
                positive.Fields["altbutton"] != 0 || negative.Fields["altbutton"] != 0 ||
                positive.Input is not { Type: CommandType.Axis, Direction: 1 } ||
                negative.Input is not { Type: CommandType.Axis, Direction: -1 } ||
                slot is < 1 or > 10 || !document.Values.TryGetValue("GstInput.JoystickDevice" + slot, out var name) ||
                string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Both editable directional records are required to assign this axis.");
        }
        private static void RejectAxisCollision(SquadronsNativeDocument document,
            string positiveRecordId, string negativeRecordId, int slot, string physicalAxisKey, string concept)
        {
            // A complete physical axis may not silently acquire an unrelated command.
            // Reject the collision before writing either directional native record.
            // Keep the old source-axis bindings untouched; the user must explicitly
            // reassign or clear the conflicting command through a validated workflow.
            var collisions = document.Bindings.Where(row =>
                row.RecordId != positiveRecordId && row.RecordId != negativeRecordId &&
                row.Type == 3 && row.DeviceId == slot - 1 &&
                row.Input is { Type: CommandType.Axis } existing &&
                existing.PhysicalKey == SquadronsInputCodec.PhysicalKey(physicalAxisKey) &&
                row.Concept != concept)
                .Select(row => row.Concept).Distinct(StringComparer.Ordinal).ToArray();
            if (collisions.Length > 0)
                throw new InvalidOperationException(
                    "This physical axis already has another Squadrons command: " +
                    string.Join(", ", collisions.Select(x => x[(x.LastIndexOf('.') + 1)..]
                        .Replace("Concept", "", StringComparison.Ordinal))) +
                    ". Choose a different axis or explicitly clear/reassign the existing command.");
        }
        private static void ValidateAxisPairWrite(SquadronsNativeDocument document, SquadronsNativeDocument written,
            string positiveRecordId, string negativeRecordId, int slot, SquadronsInput plus, SquadronsInput minus,
            Dictionary<string, int> changes)
        {
            if (written.Bindings.Count != document.Bindings.Count ||
                written.Bindings.Single(x => x.RecordId == positiveRecordId).Input != plus ||
                written.Bindings.Single(x => x.RecordId == negativeRecordId).Input != minus ||
                written.Bindings.Where(x => x.RecordId == positiveRecordId || x.RecordId == negativeRecordId)
                    .Any(x => x.DeviceId != slot - 1) ||
                document.Bindings.Any(old => !written.Bindings.Any(updated =>
                    updated.RecordId == old.RecordId && old.Fields.All(field =>
                        updated.Fields.TryGetValue(field.Key, out var value) &&
                        (changes.ContainsKey("GstKeyBinding." + old.RecordId + "." + field.Key) ||
                         value == field.Value)))))
                throw new InvalidOperationException("Bidirectional native axis validation failed; nothing was staged.");
        }

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

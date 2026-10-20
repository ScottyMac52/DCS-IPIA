using System.Text.Json;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Games
{
    /// <summary>Stages isolated, capability-validated edits; failed edits never replace current or saved state.</summary>
    public sealed class GameProfileSession
    {
        private readonly ISimProfileBackend _backend;
        private readonly EditionPolicy _policy;
        private readonly ISimDeviceReferenceMatcher? _referenceMatcher;
        private readonly IReadOnlyCollection<string> _catalogIds;
        private readonly Func<IReadOnlyList<DetectedDcsDevice>> _observed;
        private GameWorkspace _current;
        private GameWorkspace _saved;
        /// <summary>Opens a validated document using authoritative edition and hardware services.</summary>
        public GameProfileSession(ISimProfileBackend backend, GameWorkspace workspace, EditionPolicy policy,
            IReadOnlyCollection<string> catalogIds, Func<IReadOnlyList<DetectedDcsDevice>> observed, ISimDeviceReferenceMatcher? referenceMatcher = null)
        {
            _backend = backend; _policy = policy; _referenceMatcher = referenceMatcher; _catalogIds = catalogIds; _observed = observed;
            GameWorkspaceContract.Validate(workspace);
            ValidateProfile(workspace.Profile);
            _current = GameWorkspaceContract.Clone(workspace);
            _saved = GameWorkspaceContract.Clone(workspace);
        }
        #region BEGIN Public
        /// <summary>Notifies the UI after successful state transitions.</summary>
        public event EventHandler? Changed;
        /// <summary>Gets an independent document; callers cannot bypass the editing boundary.</summary>
        public GameWorkspace Snapshot => GameWorkspaceContract.Clone(_current);
        /// <summary>Gets whether current authoring differs from the last saved state.</summary>
        public bool HasChanges => JsonSerializer.Serialize(_current) != JsonSerializer.Serialize(_saved);
        /// <summary>Gets a user-facing edition restriction, or empty when editing is allowed.</summary>
        public string AccessMessage
        {
            get
            {
                try { RequireAccess(); return ""; }
                catch (InvalidOperationException error) { return error.Message; }
            }
        }
        /// <summary>Gets the captured native catalog.</summary>
        public ICommandCatalog Catalog => _backend.GetCatalog(_current.Profile);
        /// <summary>Marks the current state saved only after persistence succeeds.</summary>
        public void AcceptSaved() { _saved = GameWorkspaceContract.Clone(_current); Changed?.Invoke(this, EventArgs.Empty); }
        /// <summary>Restores the exact saved document and associations.</summary>
        public void Discard() { _current = GameWorkspaceContract.Clone(_saved); Changed?.Invoke(this, EventArgs.Empty); }
        /// <summary>Associates a native reference with an explicitly observed unit and reviewed drawing.</summary>
        public void Associate(string logicalId, string hardwareId, Guid physicalGuid)
        {
            ConnectController(physicalGuid, hardwareId, logicalId);
        }
        /// <summary>Gets compatible captured references; this list never infers ED's ordering of identical units.</summary>
        public IReadOnlyList<string> ControllerReferences(Guid physicalGuid)
        {
            var observed = Observed(physicalGuid, _observed());
            return _current.Profile.Devices.Where(x => _referenceMatcher?.Matches(x.Identity!, observed) == true).Select(x => x.LogicalId).ToArray();
        }
        /// <summary>Links one explicitly chosen unit to an existing reference or adds a unique supported product; artwork is optional.</summary>
        public string ConnectController(Guid physicalGuid, string? hardwareId = null, string? logicalId = null)
        {
            var observations = _observed(); var observed = Observed(physicalGuid, observations);
            RequireDrawing(hardwareId);
            var pinned = _current.Hardware.Where(x => x.Value.Device.PhysicalGuid == physicalGuid).Select(x => x.Key).ToArray();
            if (logicalId is null && pinned.Length == 1) return pinned[0];
            if (pinned.Any(x => x != logicalId)) throw new InvalidOperationException("This controller is already linked. Select its existing entry instead.");
            var candidate = GameWorkspaceContract.Clone(_current);
            var matching = candidate.Profile.Devices.Where(x => _referenceMatcher?.Matches(x.Identity!, observed) == true).ToArray();
            var reference = ChooseReference(candidate, matching, logicalId);
            if (reference is null)
            {
                if (observations.Count(x => x.Connected && x.ProductGuid == observed.ProductGuid) > 1)
                    throw new InvalidOperationException("Identical controllers require ED's DeviceIndex values. Bind an input on each in Elite, reimport, then choose each captured reference explicitly.");
                if (_backend is not ISimDeviceEditor editor) throw new InvalidOperationException("This game adapter cannot add controller references.");
                candidate = candidate with { Profile = editor.AddDevice(candidate.Profile, observed) };
                reference = candidate.Profile.Devices.Single(x => !_current.Profile.Devices.Any(old => old.LogicalId == x.LogicalId));
            }
            ValidateLink(candidate, reference, observed, observations);
            var hidden = candidate.Hardware.TryGetValue(reference.LogicalId, out var previous) && previous.HardwareId == hardwareId && previous.DrawingHidden;
            candidate.Hardware[reference.LogicalId] = new(hardwareId, observed) { DrawingHidden = hidden };
            GameWorkspaceContract.Validate(candidate); ValidateProfile(candidate.Profile);
            _current = candidate;
            Changed?.Invoke(this, EventArgs.Empty);
            return reference.LogicalId;
        }
        /// <summary>Previews clearing a controller and every binding using it as a modifier, without changing state.</summary>
        public GameControllerChange PreviewRemoveController(string logicalId)
        {
            var baseline = JsonSerializer.Serialize(_current);
            var candidate = GameWorkspaceContract.Clone(_current);
            candidate = candidate with { Profile = DeviceEditor().RemoveDevice(candidate.Profile, logicalId) };
            candidate.Hardware.Remove(logicalId);
            ValidateControllerCandidate(candidate);
            return new(this, baseline, candidate,
                DescribeControllerChange("Remove controller", logicalId, candidate.Profile), null);
        }
        /// <summary>Previews replacement using fresh native capability evidence and an explicit target index when needed.</summary>
        public GameControllerChange PreviewReplaceController(string logicalId, Guid physicalGuid,
            Func<IReadOnlyCollection<string>> nativeInputs, string? targetLogicalId = null, string? hardwareId = null)
        {
            _policy.RequireOwner();
            var baseline = JsonSerializer.Serialize(_current);
            ArgumentNullException.ThrowIfNull(nativeInputs);
            var observations = _observed(); var observed = Observed(physicalGuid, observations);
            RequireDrawing(hardwareId);
            var candidate = GameWorkspaceContract.Clone(_current);
            var pinned = candidate.Hardware.Where(x => x.Value.Device.PhysicalGuid == physicalGuid).Select(x => x.Key).ToArray();
            if (pinned.Length > 1) throw new InvalidOperationException("The replacement has ambiguous existing links.");
            var matches = candidate.Profile.Devices.Where(x => _referenceMatcher?.Matches(x.Identity!, observed) == true).ToArray();
            var selected = targetLogicalId ?? pinned.SingleOrDefault();
            var target = selected is not null ? ChooseReference(candidate, matches, selected)
                : matches.Length == 1 ? matches[0] : ChooseReference(candidate, matches, null);
            if (target is null)
            {
                if (observations.Count(x => x.Connected && x.ProductGuid == observed.ProductGuid) > 1)
                    throw new InvalidOperationException("Identical replacements require captured ED DeviceIndex references. Bind each in Elite and reimport first.");
                candidate = candidate with { Profile = DeviceEditor().AddDevice(candidate.Profile, observed) };
                target = candidate.Profile.Devices.Single(x => !_current.Profile.Devices.Any(old => old.LogicalId == x.LogicalId));
            }
            ValidateLink(candidate, target, observed, observations);
            var keys = nativeInputs().ToArray();
            candidate = candidate with { Profile = DeviceEditor().ReplaceDevice(candidate.Profile, logicalId, target.LogicalId, keys) };
            var previous = candidate.Hardware.GetValueOrDefault(target.LogicalId);
            if (logicalId != target.LogicalId) candidate.Hardware.Remove(logicalId);
            candidate.Hardware[target.LogicalId] = previous is not null && previous.Device.PhysicalGuid == physicalGuid
                ? previous with { Device = observed } : new(hardwareId, observed);
            ValidateControllerCandidate(candidate);
            return new(this, baseline, candidate,
                DescribeControllerChange("Replace with " + observed.Name + " · " + target.Identity!.SimSpecificIds["ed.device"] +
                    (target.Identity.SimSpecificIds.TryGetValue("ed.deviceIndex", out var index) ? " · ED index " + index : ""), logicalId, candidate.Profile),
                target.LogicalId, observed, nativeInputs, keys);
        }
        /// <summary>Stages a reviewed plan only if its session, profile revision, physical target and capabilities still match.</summary>
        public void ApplyControllerChange(GameControllerChange change)
        {
            ArgumentNullException.ThrowIfNull(change);
            if (!ReferenceEquals(change.Owner, this) || change.Baseline != JsonSerializer.Serialize(_current))
                throw new InvalidOperationException("The profile changed after this preview. Review the controller change again.");
            if (change.Target is { } target)
            {
                _policy.RequireOwner();
                var observed = Observed(target.PhysicalGuid, _observed());
                if (observed.ProductGuid != target.ProductGuid || !change.Keys.SetEquals(change.Inputs!()))
                    throw new InvalidOperationException("Replacement identity or inputs changed. Review the replacement again.");
            }
            var candidate = GameWorkspaceContract.Clone(change.Candidate);
            ValidateControllerCandidate(candidate);
            if (change.Baseline != JsonSerializer.Serialize(_current))
                throw new InvalidOperationException("The profile changed during validation. Review the change again.");
            _current = candidate;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        /// <summary>Changes or removes the shared illustration without changing the physical or native target.</summary>
        public void ChangeDrawing(string logicalId, string? hardwareId)
        {
            RequireDrawing(hardwareId);
            if (!_current.Hardware.TryGetValue(logicalId, out var pin)) throw new InvalidOperationException("Connect this controller before choosing its drawing.");
            var candidate = GameWorkspaceContract.Clone(_current);
            candidate.Hardware[logicalId] = pin with { HardwareId = hardwareId ?? pin.HardwareId, DrawingHidden = hardwareId is null };
            GameWorkspaceContract.Validate(candidate); _current = candidate; Changed?.Invoke(this, EventArgs.Empty);
        }
        /// <summary>Stages a primary or secondary button assignment without disturbing other command slots.</summary>
        public void AssignButton(string deviceId, string commandId, string slot, string key, BindingMode mode, IReadOnlyList<Modifier> modifiers) =>
            Edit(profile =>
            {
                Remove(profile, commandId, slot);
                profile.Devices.Single(x => x.LogicalId == deviceId).Buttons.Add(new()
                {
                    CommandId = commandId, SourceButton = key, Mode = mode,
                    Modifiers = modifiers.Select(x => new Modifier { Source = x.Source, Value = x.Value }).ToList(),
                    SimSpecific = new() { ["ed.slot"] = slot }
                });
            });
        /// <summary>Stages an axis assignment with native inversion and percentage deadzone.</summary>
        public void AssignAxis(string deviceId, string commandId, string key, bool invert, float deadzone) =>
            Edit(profile =>
            {
                Remove(profile, commandId, "Binding");
                profile.Devices.Single(x => x.LogicalId == deviceId).Axes.Add(new()
                {
                    CommandId = commandId, SourceAxis = key, Invert = invert, Deadzone = deadzone,
                    SimSpecific = new() { ["ed.slot"] = "Binding" }
                });
            });
        /// <summary>Clears one explicit native command slot.</summary>
        public void Clear(string commandId, string slot) => Edit(profile => Remove(profile, commandId, slot));
        /// <summary>Changes an existing command-level toggle independently from button hold mode.</summary>
        public void SetToggle(string commandId, bool value) => Edit(profile =>
        {
            var key = "ed.toggle:" + commandId;
            if (!profile.Metadata.ContainsKey(key)) throw new InvalidOperationException("This command has no native toggle setting.");
            profile.Metadata[key] = value ? "1" : "0";
        });
        /// <summary>Creates a reviewed native payload only after edition and conversion validation.</summary>
        public SimProfileExportPlan Export() { RequireAccess(); return ValidateProfile(_current.Profile); }
        #endregion END Public
        #region BEGIN private helpers
        private ISimDeviceEditor DeviceEditor() => _backend as ISimDeviceEditor
            ?? throw new InvalidOperationException("This game adapter cannot edit controllers.");
        private void ValidateControllerCandidate(GameWorkspace candidate)
        {
            GameWorkspaceContract.Validate(candidate);
            GameWorkspaceAccess.Require(candidate, _policy, _catalogIds, _observed(), _referenceMatcher);
            ValidateProfile(candidate.Profile);
        }
        private string DescribeControllerChange(string action, string logicalId, ProfileDocument after)
        {
            var source = _current.Profile.Devices.Single(x => x.LogicalId == logicalId);
            var affected = new List<(string Command, string Slot, string Key)>();
            foreach (var device in _current.Profile.Devices)
            {
                affected.AddRange(device.Buttons.Where(x => device.LogicalId == logicalId || x.Modifiers.Any(m => m.Source == logicalId))
                    .Select(x => (x.CommandId, x.SimSpecific["ed.slot"].ToString()!, x.SourceButton)));
                affected.AddRange(device.Axes.Where(x => device.LogicalId == logicalId || x.Modifiers.Any(m => m.Source == logicalId))
                    .Select(x => (x.CommandId, "Binding", x.SourceAxis)));
            }
            var commands = Catalog.Commands.ToDictionary(x => x.Id, x => x.DisplayName, StringComparer.Ordinal);
            var lines = new List<string>(); var cleared = 0;
            foreach (var row in affected)
            {
                var survives = after.Devices.Any(d => d.Buttons.Any(x => x.CommandId == row.Command && x.SimSpecific["ed.slot"].ToString() == row.Slot)
                    || (row.Slot == "Binding" && d.Axes.Any(x => x.CommandId == row.Command)));
                if (!survives) cleared++;
                lines.Add((survives ? "KEEP / TRANSFER: " : "CLEAR: ") + commands[row.Command] + " · " + row.Slot + " · " + row.Key);
            }
            return action + "\nFrom: " + source.Identity!.SimSpecificIds["ed.device"] +
                (source.Identity.SimSpecificIds.TryGetValue("ed.deviceIndex", out var index) ? " · ED index " + index : "") +
                "\nAffected bindings: " + affected.Count + "; cleared: " + cleared + "; retained/transferred: " + (affected.Count - cleared) +
                "\nA missing modifier clears the entire chord; it never becomes an unmodified command.\n" +
                "Replacement preserves native key names; it does not guess equivalent button locations.\n" +
                string.Join("\n", lines) + "\n\nThis stages workspace edits only. Save persists them; Preview / Export writes a separate reviewed .binds file.";
        }
        private DetectedDcsDevice Observed(Guid physicalGuid, IReadOnlyList<DetectedDcsDevice> observations)
        {
            var matches = observations.Where(x => x.Connected && x.PhysicalGuid == physicalGuid).ToArray();
            if (physicalGuid == Guid.Empty || matches.Length != 1 || !_policy.AllowsDevice(matches[0]))
                throw new InvalidOperationException("Choose one connected controller eligible for your edition. Device names cannot establish manufacturer eligibility.");
            return matches[0];
        }
        private void RequireDrawing(string? hardwareId)
        {
            if (hardwareId is null) return;
            if (!_catalogIds.Contains(hardwareId)) throw new InvalidOperationException("Choose a reviewed shared hardware drawing or no drawing.");
            _policy.RequireHardware([hardwareId]);
        }
        private static LogicalDevice? ChooseReference(GameWorkspace candidate, LogicalDevice[] matching, string? logicalId)
        {
            if (logicalId is not null) return matching.SingleOrDefault(x => x.LogicalId == logicalId)
                ?? throw new InvalidOperationException("The selected ED reference does not match this controller's native product.");
            var available = matching.Where(x => !candidate.Hardware.ContainsKey(x.LogicalId)).ToArray();
            if (available.Length == 1) return available[0];
            if (matching.Length > 0) throw new InvalidOperationException("Choose which matching ED reference to link. Existing links are retained.");
            return null;
        }
        private static void ValidateLink(GameWorkspace candidate, LogicalDevice reference, DetectedDcsDevice observed, IReadOnlyList<DetectedDcsDevice> observations)
        {
            if (candidate.Hardware.TryGetValue(reference.LogicalId, out var old) && old.Device.PhysicalGuid != observed.PhysicalGuid
                && observations.Any(x => x.Connected && x.PhysicalGuid == old.Device.PhysicalGuid))
                throw new InvalidOperationException("That ED reference is already linked to another connected controller. Choose a different captured reference.");
            if (observations.Count(x => x.Connected && x.ProductGuid == observed.ProductGuid) > 1
                && !reference.Identity!.SimSpecificIds.ContainsKey("ed.deviceIndex"))
                throw new InvalidOperationException("ED's reference has no DeviceIndex for these identical controllers. Bind each controller in Elite and reimport its indexed references first.");
        }
        private void RequireAccess()
        {
            if (!_policy.IsOwner) GameWorkspaceAccess.Require(_current, _policy, _catalogIds, _observed(), _referenceMatcher);
        }
        private SimProfileExportPlan ValidateProfile(ProfileDocument profile)
        {
            var plan = _backend.Export(profile);
            if (!plan.Succeeded) throw new InvalidOperationException(string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
            return plan;
        }
        private void Edit(Action<ProfileDocument> edit)
        {
            RequireAccess();
            var candidate = GameWorkspaceContract.Clone(_current);
            edit(candidate.Profile);
            ValidateProfile(candidate.Profile);
            _current = candidate;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        private static void Remove(ProfileDocument profile, string commandId, string slot)
        {
            foreach (var device in profile.Devices)
            {
                device.Buttons.RemoveAll(x => x.CommandId == commandId && x.SimSpecific["ed.slot"].ToString() == slot);
                if (slot == "Binding") device.Axes.RemoveAll(x => x.CommandId == commandId);
            }
        }
        #endregion END private helpers
    }
}


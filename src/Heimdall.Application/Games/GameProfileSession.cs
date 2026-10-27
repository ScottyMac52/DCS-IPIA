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
        private IReadOnlyCollection<string> _catalogIds;
        private readonly Func<IReadOnlyList<DetectedDcsDevice>> _observed;
        private readonly Func<IReadOnlyList<ControllerObservation>> _controllerInputs;
        private GameWorkspace _current;
        private GameWorkspace _saved;
        /// <summary>Opens a validated document using authoritative edition and hardware services.</summary>
        public GameProfileSession(ISimProfileBackend backend, GameWorkspace workspace, EditionPolicy policy,
            IReadOnlyCollection<string> catalogIds, Func<IReadOnlyList<DetectedDcsDevice>> observed, ISimDeviceReferenceMatcher? referenceMatcher = null,
            Func<IReadOnlyList<ControllerObservation>>? controllerInputs = null)
        {
            _backend = backend; _policy = policy; _referenceMatcher = referenceMatcher; _catalogIds = catalogIds; _observed = observed;
            _controllerInputs = controllerInputs ?? (() => []);
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
        /// <summary>Gets whether native indexed-record editing is implemented by the active adapter.</summary>
        public bool CanEditNativeRecords => _backend is INativeRecordEditor;
        /// <summary>Stages one validated native record edit atomically; foreign backend/context results are rejected.</summary>
        public void RebindNative(string recordId, string logicalDeviceId, string key, CommandType type, int direction)
        {
            RequireAccess();
            var editor = _backend as INativeRecordEditor ?? throw new InvalidOperationException("This game does not support native record editing.");
            var pin = _current.Hardware.GetValueOrDefault(logicalDeviceId);
            var keys = GameControllerInputs.VerifiedKeys(pin, _controllerInputs());
            if (!GameInputReadiness.Ready(pin, _observed()) || keys?.Contains(editor.PhysicalInputKey(key), StringComparer.Ordinal) != true)
                throw new InvalidOperationException("Choose an input verified on this linked controller. Link its captured slot and rescan connected hardware; buttons and hats absent from the device cannot be assigned.");
            var profile = editor.RebindNative(Snapshot.Profile, recordId, logicalDeviceId, key, type, direction);
            if (profile.BackendId != _backend.SimId || profile.ContextId != _current.Profile.ContextId || profile.ProfileId != _current.Profile.ProfileId)
                throw new InvalidOperationException("The native edit changed the owning game or profile identity.");
            ValidateProfile(profile);
            _current = GameWorkspaceContract.Clone(_current with { Profile = profile }); Changed?.Invoke(this, EventArgs.Empty);
        }
        /// <summary>Atomically stages both native directions of a single analog control,
        /// applying the same physical link and verified-input checks as normal edits.</summary>
        public void RebindNativeAxisPair(string positiveRecordId, string negativeRecordId,
            string logicalDeviceId, string axisKey)
        {
            RequireAccess();
            if (_backend is not INativeAxisPairEditor editor)
                throw new InvalidOperationException("This game does not support bidirectional native axis editing.");
            var pin = _current.Hardware.GetValueOrDefault(logicalDeviceId);
            var keys = GameControllerInputs.VerifiedKeys(pin, _controllerInputs());
            if (!GameInputReadiness.Ready(pin, _observed()) ||
                keys?.Contains(editor.PhysicalInputKey(axisKey), StringComparer.Ordinal) != true)
                throw new InvalidOperationException("Link and verify this physical axis before assigning it.");
            var profile = editor.RebindAxisPair(Snapshot.Profile, positiveRecordId,
                negativeRecordId, logicalDeviceId, axisKey);
            if (profile.BackendId != _backend.SimId ||
                profile.ContextId != _current.Profile.ContextId ||
                profile.ProfileId != _current.Profile.ProfileId)
                throw new InvalidOperationException("The native axis edit changed the owning game or profile identity.");
            ValidateProfile(profile);
            _current = GameWorkspaceContract.Clone(_current with { Profile = profile });
            Changed?.Invoke(this, EventArgs.Empty);
        }
        /// <summary>Stages a new captured Squadrons record only after the same physical
        /// device and verified key checks used for edits to existing records.</summary>
        public void AddSquadronsNativeBinding(string concept, string logicalDeviceId,
            string key, CommandType type, int direction)
        {
            RequireAccess();
            if (_backend is not INativeRecordCreator backend)
                throw new InvalidOperationException("This game does not support native record creation.");
            var pin = _current.Hardware.GetValueOrDefault(logicalDeviceId);
            var keys = GameControllerInputs.VerifiedKeys(pin, _controllerInputs());
            if (!GameInputReadiness.Ready(pin, _observed()) ||
                keys?.Contains(backend.PhysicalInputKey(key), StringComparer.Ordinal) != true)
                throw new InvalidOperationException("Choose an input verified on this linked controller before creating the record.");
            var profile = backend.AddNativeBinding(Snapshot.Profile, concept, logicalDeviceId, key, type, direction);
            if (profile.BackendId != _backend.SimId || profile.ContextId != _current.Profile.ContextId ||
                profile.ProfileId != _current.Profile.ProfileId)
                throw new InvalidOperationException("Native creation changed game or profile identity.");
            ValidateProfile(profile);
            _current = GameWorkspaceContract.Clone(_current with { Profile = profile });
            Changed?.Invoke(this, EventArgs.Empty);
        }
        /// <summary>Stages a configuration display name independently of immutable native source metadata.</summary>
        public void Rename(string name)
        {
            RequireAccess(); GameConfigurationName.Validate(name);
            _current = _current with { Name = name }; Changed?.Invoke(this, EventArgs.Empty);
        }
        /// <summary>Gets whether the adapter supports reviewed physical links to captured slots without native identity inference.</summary>
        public bool CanLinkCapturedDevices => _backend is ISimCapturedDeviceLinks;
        /// <summary>Reviews a new or replacement physical link without altering native slots or bindings.</summary>
        public GameControllerChange PreviewCapturedDeviceLink(string logicalId, Guid physicalGuid)
        {
            RequireCapturedDevice(logicalId);
            var observed = Observed(physicalGuid, _observed());
            if (observed.ProductGuid == Guid.Empty) throw new InvalidOperationException("Rescan this controller to obtain its native product identity.");
            if (_current.Hardware.Any(x => x.Key != logicalId && x.Value.Device.PhysicalGuid == physicalGuid))
                throw new InvalidOperationException("This physical controller is already linked to another captured slot. Unlink that slot first.");
            var candidate = Snapshot;
            var previous = candidate.Hardware.GetValueOrDefault(logicalId);
            candidate.Hardware[logicalId] = previous is not null && previous.Device.PhysicalGuid == physicalGuid && previous.Device.ProductGuid == observed.ProductGuid
                ? previous with { Device = observed } : new(null, observed);
            return CapturedDeviceChange(logicalId, candidate, observed);
        }
        /// <summary>Reviews removal of the physical association while retaining the captured slot and all native assignments.</summary>
        public GameControllerChange PreviewCapturedDeviceUnlink(string logicalId)
        {
            RequireCapturedDevice(logicalId);
            var candidate = Snapshot;
            if (!candidate.Hardware.Remove(logicalId)) throw new InvalidOperationException("This captured slot has no physical link to remove.");
            return CapturedDeviceChange(logicalId, candidate, null);
        }
        /// <summary>Reviews removal of an unused and unlinked captured slot.</summary>
        public GameControllerChange PreviewRemoveUnusedCapturedDevice(string logicalId)
        {
            RequireCapturedDevice(logicalId);
            if (_current.Hardware.ContainsKey(logicalId))
                throw new InvalidOperationException("Unlink this physical controller before removing its captured slot.");
            var editor = (ISimCapturedDeviceLinks)_backend;
            var candidate = Snapshot;
            candidate = candidate with { Profile = editor.RemoveUnusedCapturedDevice(candidate.Profile, logicalId),
                ControllerOrder = candidate.ControllerOrder.Where(x => x != logicalId).ToList() };
            ValidateControllerCandidate(candidate);
            return new(this, JsonSerializer.Serialize(_current), candidate,
                "Remove unused captured slot " + logicalId +
                "\nOnly its native joystick declaration will be removed; no binding records or inventory entries change." +
                "\nSave profile before export.", null);
        }
        /// <summary>Reviews removing one exact native joystick record without rewriting any other assignments.</summary>
        public GameControllerChange PreviewClearCapturedRecord(string recordId)
        {
            _policy.RequireSimulator(_backend.SimId);
            if (_backend is not ISimCapturedDeviceLinks adapter)
                throw new InvalidOperationException("Native record clearing is unavailable for this simulator.");
            var candidate = Snapshot;
            candidate = candidate with { Profile = adapter.ClearCapturedRecord(candidate.Profile, recordId) };
            ValidateControllerCandidate(candidate);
            return new(this, JsonSerializer.Serialize(_current), candidate,
                "Clear selected Squadrons native binding: " + recordId +
                "\nOther native records, devices, and physical links remain unchanged." +
                "\nSave the profile before exporting a separate review copy.", null);
        }
        /// <summary>Reviews clearing all native joystick records belonging to one captured slot.</summary>
        public GameControllerChange PreviewClearCapturedAssignments(string logicalId)
        {
            RequireCapturedDevice(logicalId);
            if (_backend is not ISimCapturedDeviceLinks adapter)
                throw new InvalidOperationException("Native joystick clearing is unavailable for this simulator.");
            var before = adapter.CountCapturedAssignments(_current.Profile, logicalId);
            if (before == 0) throw new InvalidOperationException("No captured joystick assignments exist on this slot.");
            var candidate = Snapshot;
            candidate = candidate with { Profile = adapter.ClearCapturedAssignments(candidate.Profile, logicalId) };
            ValidateControllerCandidate(candidate);
            return new(this, JsonSerializer.Serialize(_current), candidate,
                "Clear all " + before + " native joystick records on " + logicalId +
                "\nThis removes button, axis, hat, unsupported and other captured joystick records on the selected slot." +
                "\nOther controllers and their records are untouched. Save profile before exporting.", logicalId);
        }
        /// <summary>Reviews physical native slot renumbering; every binding reference and hardware association follows its unit.</summary>
        public GameControllerChange PreviewMoveCapturedSlot(string source, string target, bool after = false)
        {
            RequireCapturedDevice(source);
            RequireCapturedDevice(target);
            if (_backend is not ISimCapturedDeviceLinks adapter)
                throw new InvalidOperationException("Native slot renumbering is unavailable for this simulator.");
            if (source == target) throw new InvalidOperationException("Choose a different destination slot.");
            var order = _current.Profile.Devices
                .OrderBy(x => int.Parse(x.Identity!.SimSpecificIds["squadrons.slot"], System.Globalization.CultureInfo.InvariantCulture))
                .Select(x => x.LogicalId).ToList();
            order.Remove(source);
            order.Insert(order.IndexOf(target) + (after ? 1 : 0), source);
            var oldSlots = order.Select((old, index) => (old, next: "squadrons.joystick." + (index + 1)))
                .ToDictionary(x => x.old, x => x.next, StringComparer.Ordinal);
            var candidate = Snapshot;
            var profile = adapter.ReorderCapturedSlots(candidate.Profile, order);
            var pins = candidate.Hardware.ToDictionary(x => oldSlots[x.Key], x => x.Value, StringComparer.Ordinal);
            candidate = candidate with { Profile = profile, Hardware = pins,
                ControllerOrder = order.Select(id => oldSlots[id]).ToList() };
            ValidateControllerCandidate(candidate);
            var moves = order.Select((id, index) => id + " → Slot " + (index + 1));
            return new(this, JsonSerializer.Serialize(_current), candidate,
                "Renumber Squadrons native joystick slots:\n" + string.Join("\n", moves) +
                "\nAll joystick binding device IDs and physical links follow the original units." +
                "\nSave profile and export a review copy; game-side recognition still needs validation.", oldSlots[source]);
        }
        /// <summary>Moves a controller in saved display order; native identities, indices and bindings are untouched.</summary>
        public void MoveController(string source, string target, bool after = false)
        {
            _policy.RequireSimulator(_backend.SimId);
            var order = GameControllerOrder.For(_current).ToList();
            if (!order.Contains(source) || !order.Contains(target))
                throw new ArgumentException("Choose two controllers in this profile.");
            if (source == target) return;
            order.Remove(source); order.Insert(order.IndexOf(target) + (after ? 1 : 0), source);
            if (order.SequenceEqual(GameControllerOrder.For(_current))) return;
            _current = _current with { ControllerOrder = order };
            Changed?.Invoke(this, EventArgs.Empty);
        }
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
            _policy.RequireSimulator(_backend.SimId);
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
            candidate = candidate with { ControllerOrder = GameControllerOrder.For(candidate).ToList() };
            ValidateControllerCandidate(candidate);
            return new(this, baseline, candidate,
                DescribeControllerChange("Remove controller", logicalId, candidate.Profile), null);
        }
        /// <summary>Previews replacement using fresh native capability evidence and an explicit target index when needed.</summary>
        public GameControllerChange PreviewReplaceController(string logicalId, Guid physicalGuid,
            Func<IReadOnlyCollection<string>> nativeInputs, string? targetLogicalId = null, string? hardwareId = null)
        {
            _policy.RequirePremium();
            var baseline = JsonSerializer.Serialize(_current);
            ArgumentNullException.ThrowIfNull(nativeInputs);
            _policy.RequireSimulator(_backend.SimId);
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
            candidate = candidate with { ControllerOrder = GameControllerOrder.For(_current)
                .Where(id => id != target.LogicalId || id == logicalId)
                .Select(id => id == logicalId ? target.LogicalId : id).ToList() };
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
                _policy.RequirePremium();
                var observed = Observed(target.PhysicalGuid, _observed());
                if (observed.ProductGuid != target.ProductGuid || (change.Inputs is { } inputs && !change.Keys.SetEquals(inputs())))
                    throw new InvalidOperationException("Replacement identity or inputs changed. Review the replacement again.");
            }
            var candidate = GameWorkspaceContract.Clone(change.Candidate);
            ValidateControllerCandidate(candidate);
            if (change.Baseline != JsonSerializer.Serialize(_current))
                throw new InvalidOperationException("The profile changed during validation. Review the change again.");
            _current = candidate;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        /// <summary>Registers a verified bundled drawing catalog after asynchronous artwork discovery.
        /// Updates validation metadata only; it never changes a staged or saved workspace.</summary>
        public void SetDrawingCatalog(IReadOnlyCollection<string> hardwareIds)
        {
            ArgumentNullException.ThrowIfNull(hardwareIds);
            if (hardwareIds.Any(string.IsNullOrWhiteSpace) ||
                hardwareIds.Distinct(StringComparer.Ordinal).Count() != hardwareIds.Count)
                throw new ArgumentException("Drawing catalog must contain unique nonempty hardware IDs.", nameof(hardwareIds));
            _catalogIds = hardwareIds.ToArray();
        }
        /// <summary>Changes or removes the shared illustration without changing the physical or native target.</summary>
        public void ChangeDrawing(string logicalId, string? hardwareId)
        {
            _policy.RequireSimulator(_backend.SimId);
            RequireDrawing(hardwareId);
            if (!_current.Hardware.TryGetValue(logicalId, out var pin)) throw new InvalidOperationException("Connect this controller before choosing its drawing.");
            var candidate = GameWorkspaceContract.Clone(_current);
            candidate.Hardware[logicalId] = pin with { HardwareId = hardwareId ?? pin.HardwareId, DrawingHidden = hardwareId is null };
            GameWorkspaceContract.Validate(candidate); _current = candidate; Changed?.Invoke(this, EventArgs.Empty);
        }
        /// <summary>Reviews a complete draft with the authoritative adapter without publishing edits or events.</summary>
        public GameAssignmentReview ReviewAssignment(GameAssignmentEdit draft)
        {
            try
            {
                RequireAccess();
                var candidate = GameWorkspaceContract.Clone(_current);
                ApplyDraft(candidate.Profile, draft);
                var output = ValidateProfile(candidate.Profile);
                var current = ValidateProfile(_current.Profile);
                var changed = !output.Files.OrderBy(x => x.Key, StringComparer.Ordinal)
                    .SequenceEqual(current.Files.OrderBy(x => x.Key, StringComparer.Ordinal));
                return new(true, changed, "");
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException)
            { return new(false, false, error.Message); }
        }
        /// <summary>Stages the entire reviewed draft atomically, including its toggle, with one change event.</summary>
        public void Assign(GameAssignmentEdit draft) => Edit(profile => ApplyDraft(profile, draft));
        /// <summary>Stages a primary or secondary button assignment without disturbing other command slots.</summary>
        public void AssignButton(string deviceId, string commandId, string slot, string key, BindingMode mode, IReadOnlyList<Modifier> modifiers)
        {
            ArgumentNullException.ThrowIfNull(modifiers);
            Assign(new(deviceId, commandId, slot, key, Mode: mode, Modifiers: modifiers));
        }
        /// <summary>Stages an axis assignment with native inversion and percentage deadzone.</summary>
        public void AssignAxis(string deviceId, string commandId, string key, bool invert, float deadzone) =>
            Assign(new(deviceId, commandId, "Binding", key, IsAxis: true, Invert: invert, Deadzone: deadzone));
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
        /// <summary>Reviews all output references against current hardware, independently of offline format validation.</summary>
        public IReadOnlyList<GameExportIssue> ReviewGameExport()
        {
            RequireAccess();
            var exporter = _backend as ISimGameExport ?? throw new InvalidOperationException("This adapter cannot prepare a game preset.");
            return GameExportPreflight.Review(_current, exporter.DeviceUsages(_current.Profile), _observed(), _referenceMatcher);
        }
        /// <summary>Produces a named game preset only after fresh connected-device preflight; no files are written here.</summary>
        public SimProfileExportPlan ExportForGame()
        {
            var issues = ReviewGameExport();
            if (issues.Count > 0) throw new InvalidOperationException("Resolve controller references before export: " + string.Join("; ", issues.Select(x => x.Reference + ": " + x.Reason)));
            var plan = ((ISimGameExport)_backend).ExportNamed(_current.Profile, _current.Name);
            if (!plan.Succeeded) throw new InvalidOperationException(string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
            return plan;
        }
        #endregion END Public
        #region BEGIN private helpers
        private void RequireCapturedDevice(string logicalId)
        {
            RequireAccess();
            var links = _backend as ISimCapturedDeviceLinks ?? throw new InvalidOperationException("This adapter does not support captured-slot physical links.");
            links.ValidateCapturedDeviceLink(Snapshot.Profile, logicalId);
        }
        private GameControllerChange CapturedDeviceChange(string logicalId, GameWorkspace candidate, DetectedDcsDevice? target)
        {
            ValidateControllerCandidate(candidate);
            var source = _current.Profile.Devices.Single(x => x.LogicalId == logicalId);
            var previous = _current.Hardware.GetValueOrDefault(logicalId)?.Device;
            var summary = (target is null ? "Unlink physical controller" : "Link / replace physical controller") +
                "\nCaptured slot: " + logicalId + " · " + source.Identity!.WindowsName +
                "\nPrevious: " + (previous is null ? "Unlinked" : previous.Name + " · " + previous.PhysicalGuid) +
                "\nNew: " + (target is null ? "Unlinked" : target.Name + " · " + target.PhysicalGuid + "\nProduct: " + target.ProductGuid) +
                "\n\nNative slots and bindings are unchanged. This stages a workspace association only; save to keep it." +
                "\nOperating a device identifies its physical instance, but cannot prove its captured game slot. Verify that slot in the game." +
                "\nThis does not confirm that the game acquired the controller. A replacement may have different input capabilities; review assignments before use.";
            return new(this, JsonSerializer.Serialize(_current), candidate, summary, logicalId, target);
        }
        private ISimDeviceEditor DeviceEditor() => _backend as ISimDeviceEditor
            ?? throw new InvalidOperationException("This game adapter cannot edit controllers.");
        private void ValidateControllerCandidate(GameWorkspace candidate)
        {
            GameWorkspaceContract.Validate(candidate);
            _policy.RequireSimulator(_backend.SimId);
            GameWorkspaceAccess.Require(candidate, _policy);
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
            _policy.RequireSimulator(_backend.SimId);
            GameWorkspaceAccess.Require(_current, _policy);
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
        private static void ApplyDraft(ProfileDocument profile, GameAssignmentEdit draft)
        {
            ArgumentNullException.ThrowIfNull(draft);
            if (string.IsNullOrWhiteSpace(draft.Key) || !profile.Devices.Any(x => x.LogicalId == draft.DeviceId)
                || (draft.IsAxis && (draft.Slot != "Binding" || !float.IsFinite(draft.Deadzone) || draft.Modifiers is { Count: > 0 })))
                throw new InvalidOperationException("Choose a controller, a nonempty native key and valid axis settings.");
            Remove(profile, draft.CommandId, draft.Slot);
            var device = profile.Devices.Single(x => x.LogicalId == draft.DeviceId);
            if (draft.IsAxis)
                device.Axes.Add(new() { CommandId = draft.CommandId, SourceAxis = draft.Key, Invert = draft.Invert,
                    Deadzone = draft.Deadzone, SimSpecific = new() { ["ed.slot"] = draft.Slot } });
            else
                device.Buttons.Add(new() { CommandId = draft.CommandId, SourceButton = draft.Key, Mode = draft.Mode,
                    Modifiers = (draft.Modifiers ?? []).Select(x => new Modifier { Source = x.Source, Value = x.Value }).ToList(),
                    SimSpecific = new() { ["ed.slot"] = draft.Slot } });
            if (draft.Toggle is { } toggle)
            {
                var key = "ed.toggle:" + draft.CommandId;
                if (!profile.Metadata.ContainsKey(key)) throw new InvalidOperationException("This command has no native toggle setting.");
                profile.Metadata[key] = toggle ? "1" : "0";
            }
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


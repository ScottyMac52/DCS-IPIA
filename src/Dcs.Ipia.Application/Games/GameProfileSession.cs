using System.Text.Json;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Domain.MultiSim;

namespace Dcs.Ipia.Application.Games
{
    /// <summary>Stages isolated, capability-validated edits; failed edits never replace current or saved state.</summary>
    public sealed class GameProfileSession
    {
        private readonly ISimProfileBackend _backend;
        private readonly EditionPolicy _policy;
        private readonly IReadOnlyCollection<string> _catalogIds;
        private readonly Func<IReadOnlyList<DetectedDcsDevice>> _observed;
        private GameWorkspace _current;
        private GameWorkspace _saved;
        /// <summary>Opens a validated document using authoritative edition and hardware services.</summary>
        public GameProfileSession(ISimProfileBackend backend, GameWorkspace workspace, EditionPolicy policy,
            IReadOnlyCollection<string> catalogIds, Func<IReadOnlyList<DetectedDcsDevice>> observed)
        {
            _backend = backend; _policy = policy; _catalogIds = catalogIds; _observed = observed;
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
            if (!_current.Profile.Devices.Any(x => x.LogicalId == logicalId) || !_catalogIds.Contains(hardwareId))
                throw new InvalidOperationException("Choose a captured device and a shared hardware drawing.");
            _policy.RequireHardware([hardwareId]);
            var matches = _observed().Where(x => x.Connected && x.PhysicalGuid == physicalGuid).ToArray();
            if (physicalGuid == Guid.Empty || matches.Length != 1 || !_policy.AllowsDevice(matches[0]))
                throw new InvalidOperationException("Choose one connected, eligible physical controller. A native game token is not manufacturer evidence.");
            _current.Hardware[logicalId] = new(hardwareId, matches[0]);
            Changed?.Invoke(this, EventArgs.Empty);
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
        private void RequireAccess()
        {
            if (!_policy.IsOwner) GameWorkspaceAccess.Require(_current, _policy, _catalogIds, _observed());
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

using Heimdall.Application.Interfaces;
using System.Text.Json;

namespace Heimdall.Application
{
    /// <summary>Stages discovery for explicit adoption; scans never write inventory or module assignments.</summary>
    public sealed class HardwareInventoryManager
    {
        private readonly IHardwareInventoryRepository _repository;
        private readonly IControllerCapabilityDiscovery _discovery;
        /// <summary>Creates inventory use cases for the selected profile.</summary>
        /// <param name="repository">Profile-owned persistence.</param>
        /// <param name="discovery">Cancellable current-machine capability adapter.</param>
        public HardwareInventoryManager(IHardwareInventoryRepository repository, IControllerCapabilityDiscovery discovery)
        {
            _repository = repository;
            _discovery = discovery;
        }
        #region BEGIN Public
        /// <summary>Reads reviewed inventory without discovery or module changes.</summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Validated reusable definitions and physical instances.</returns>
        public async Task<HardwareInventorySnapshot> ReadSnapshotAsync(CancellationToken ct = default)
        {
            var snapshot = await _repository.ReadAsync(ct);
            HardwareInventoryContract.Validate(snapshot);
            return snapshot;
        }

        /// <summary>Loads saved selections as disconnected until a successful rescan.</summary>
        /// <param name="ct">Read cancellation.</param>
        /// <returns>Review rows.</returns>
        public async Task<IReadOnlyList<HardwareInventoryCandidate>> LoadAsync(CancellationToken ct = default) =>
            Merge(await _repository.ReadAsync(ct), []);
        /// <summary>Observes attached devices without saving, rebinding or removing disconnected units.</summary>
        /// <param name="ct">Scan cancellation.</param>
        /// <returns>Saved units merged by exact physical GUID with current evidence.</returns>
        public async Task<IReadOnlyList<HardwareInventoryCandidate>> ScanAsync(CancellationToken ct = default)
        {
            var saved = await _repository.ReadAsync(ct);
            var observed = await _discovery.DiscoverAsync(ct);
            ct.ThrowIfCancellationRequested();
            return Merge(saved, observed);
        }
        /// <summary>Commits explicit selection, labels and capability adoption atomically.</summary>
        /// <param name="edits">Reviewed rows; unselected new units are not adopted.</param>
        /// <param name="ct">Save cancellation.</param>
        /// <returns>Committed inventory.</returns>
        public async Task<HardwareInventorySnapshot> SaveAsync(IReadOnlyList<HardwareInventoryEdit> edits, CancellationToken ct = default)
        {
            var snapshot = Prepare(await _repository.ReadAsync(ct), edits);
            await _repository.SaveAsync(snapshot, ct);
            return snapshot;
        }
        /// <summary>Validates and stages the same reviewed adoption for inventory save or transactional setup.</summary>
        /// <param name="saved">Current inventory used for stale-edit checks.</param>
        /// <param name="edits">Explicit selections and capability decisions.</param>
        /// <returns>Validated inventory without any persistence.</returns>
        public static HardwareInventorySnapshot Prepare(HardwareInventorySnapshot saved, IReadOnlyList<HardwareInventoryEdit> edits)
        {
            HardwareInventoryContract.Validate(saved);
            var definitions = saved.Definitions.ToDictionary(x => x.Id, StringComparer.Ordinal);
            var instances = saved.Instances.ToDictionary(x => x.PhysicalGuid);
            var observations = saved.Observations.ToDictionary(x => x.Device.PhysicalGuid);
            var identities = new HashSet<Guid>();
            foreach (var edit in edits)
            {
                var candidate = edit.Candidate;
                if (!identities.Add(candidate.PhysicalGuid)) throw new InvalidDataException("Duplicate inventory edits.");
                instances.TryGetValue(candidate.PhysicalGuid, out var existing);
                if (JsonSerializer.Serialize(existing) != JsonSerializer.Serialize(candidate.Saved))
                    throw new InvalidOperationException("Inventory changed; reopen before saving.");
                if (candidate.Observed is { } observed)
                {
                    if (observed.Device.PhysicalGuid != candidate.PhysicalGuid) throw new InvalidDataException("Observation identity mismatch.");
                    observations[candidate.PhysicalGuid] = observed;
                }
                if (existing is null && !edit.Selected) continue;
                var caps = candidate.Observed?.Capabilities;
                var changed = existing is not null && caps is not null &&
                    existing.DefinitionId != ControllerCapabilityMapper.Fingerprint(caps);
                var definitionId = existing?.DefinitionId;
                if (existing is null || (changed && edit.AdoptChangedCapabilities))
                {
                    if (caps is null || caps.Inputs.Count == 0) throw new InvalidOperationException("Enumerate supported controls before adopting this unit.");
                    definitionId = ControllerCapabilityMapper.Fingerprint(caps);
                    definitions.TryAdd(definitionId, new(definitionId, caps));
                }
                instances[candidate.PhysicalGuid] = new(candidate.PhysicalGuid,
                    candidate.Observed?.Device.Name ?? existing!.NativeName, definitionId!, edit.Label.Trim(), edit.Role.Trim(), edit.Selected,
                    existing is null || (changed && edit.AdoptChangedCapabilities) || (edit.ReviewMapping && !changed) ? edit.TemplateCandidates : existing.TemplateCandidates);
            }
            var snapshot = new HardwareInventorySnapshot(1, definitions.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(),
                instances.Values.OrderBy(x => x.PhysicalGuid).ToArray(), observations.Values.OrderBy(x => x.Device.PhysicalGuid).ToArray());
            HardwareInventoryContract.Validate(snapshot);
            return snapshot;
        }
        /// <summary>Merges only exact physical identities; display names never identify a unit.</summary>
        /// <param name="saved">Previously reviewed inventory.</param>
        /// <param name="observed">Successful current scan evidence.</param>
        /// <returns>Stable review rows, retaining disconnected selections.</returns>
        public static IReadOnlyList<HardwareInventoryCandidate> Merge(HardwareInventorySnapshot saved, IReadOnlyList<ControllerObservation> observed)
        {
            HardwareInventoryContract.Validate(saved);
            if (observed.Any(x => x.Device.PhysicalGuid == Guid.Empty) ||
                observed.Select(x => x.Device.PhysicalGuid).Distinct().Count() != observed.Count)
                throw new InvalidDataException("Discovery returned ambiguous physical identities.");
            var old = saved.Instances.ToDictionary(x => x.PhysicalGuid);
            var scan = observed.ToDictionary(x => x.Device.PhysicalGuid);
            return old.Keys.Union(scan.Keys).Order().Select(id =>
            {
                old.TryGetValue(id, out var instance);
                scan.TryGetValue(id, out var current);
                var changed = instance is not null && current?.Capabilities is not null &&
                    instance.DefinitionId != ControllerCapabilityMapper.Fingerprint(current.Capabilities);
                return new HardwareInventoryCandidate(id, instance, current, changed);
            }).ToArray();
        }
        #endregion END Public
    }
}

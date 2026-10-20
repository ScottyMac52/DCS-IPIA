using System.Globalization;
using System.Text.Json;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Application
{
    /// <summary>Projects saved evidence into a privacy-preserving report without discovery or writes.</summary>
    public sealed class SupportReportService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        private readonly ISupportDatabaseInspector _database;
        private readonly IHardwareInventoryRepository _inventory;
        private readonly IEditorWorkspaceRepository _workspaces;
        private readonly IUserMappingLibrary _mappings;
        private readonly Version _version;
        private readonly int _schema;
        /// <summary>Uses read boundaries and trusted version metadata for a single selected profile.</summary>
        /// <param name="database">Read-only SQLite inspector.</param>
        /// <param name="inventory">Saved inventory without native discovery.</param>
        /// <param name="workspaces">Saved modules without editor drafts.</param>
        /// <param name="mappings">Verified user drawing library.</param>
        /// <param name="version">Application assembly version.</param>
        /// <param name="supportedSchema">Current supported database schema.</param>
        public SupportReportService(ISupportDatabaseInspector database, IHardwareInventoryRepository inventory,
            IEditorWorkspaceRepository workspaces, IUserMappingLibrary mappings, Version version, int supportedSchema)
        {
            _database = database;
            _inventory = inventory;
            _workspaces = workspaces;
            _mappings = mappings;
            _version = version;
            _schema = supportedSchema;
        }
        #region BEGIN Public
        /// <summary>Captures readable scopes independently; unreadable evidence is never reported as empty.</summary>
        /// <param name="ct">Cancellation; cancellation propagates instead of becoming failure evidence.</param>
        /// <returns>Complete preview text, which should be saved verbatim after review.</returns>
        public async Task<string> CaptureAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var failures = new List<SupportFailure>();
            SupportDatabaseStatus? database = null;
            IReadOnlyList<SupportDeviceStatus>? devices = null;
            List<SupportModuleStatus>? modules = null;
            int? mappings = null;
            try
            {
                database = await _database.InspectAsync(ct);
                if (!database.IntegrityPassed)
                    failures.Add(new("database", "integrity-failed", "Close HEIMDALL and use Data & recovery to restore a verified backup."));
                if (database.SchemaVersion is null || database.SchemaVersion < 1 || database.SchemaVersion > _schema)
                    failures.Add(new("database", "unsupported-schema", "Use the matching application version or restore a compatible verified backup."));
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { failures.Add(Failure("database", ex)); }
            try
            {
                var inventory = await _inventory.ReadAsync(ct);
                HardwareInventoryContract.Validate(inventory);
                devices = inventory.Instances.Select((unit, index) =>
                {
                    var caps = inventory.Definitions.Single(x => x.Id == unit.DefinitionId).Capabilities;
                    return new SupportDeviceStatus(index + 1, unit.Selected, caps.Inputs.Count, caps.Unsupported.Count,
                        unit.TemplateCandidates.Count, inventory.Observations.Any(x => x.Device.PhysicalGuid == unit.PhysicalGuid && x.Error is not null));
                }).ToArray();
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { failures.Add(Failure("inventory", ex)); }
            try
            {
                var ids = await _workspaces.ListAsync(ct);
                modules = [];
                for (var i = 0; i < ids.Count; i++)
                {
                    var stage = "module-" + (i + 1).ToString(CultureInfo.InvariantCulture);
                    try
                    {
                        var module = await _workspaces.GetAsync(ids[i], ct) ?? throw new InvalidDataException();
                        modules.Add(new(i + 1, module.Configuration.Profiles.Count, module.Assignments.Count,
                            module.Diagrams.Count(x => x.GenericCapabilities is null && x.CustomMapping is null),
                            module.Diagrams.Count(x => x.GenericCapabilities is not null && x.CustomMapping is null),
                            module.Diagrams.Count(x => x.CustomMapping is not null), module.Pages.Count));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException) { failures.Add(Failure(stage, ex)); }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { failures.Add(Failure("modules", ex)); }
            try { ct.ThrowIfCancellationRequested(); mappings = (await _mappings.ListAsync()).Count; }
            catch (Exception ex) when (ex is not OperationCanceledException) { failures.Add(Failure("custom-mappings", ex)); }
            ct.ThrowIfCancellationRequested();
            return JsonSerializer.Serialize(new SupportReport(1, _version.ToString(4), Environment.Version.ToString(),
                _schema, database, devices, modules, mappings, failures), JsonOptions);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static SupportFailure Failure(string stage, Exception ex) => ex switch
        {
            UnauthorizedAccessException => new(stage, "access-denied", "Check permission to the selected user-data folder, then retry."),
            InvalidDataException or JsonException => new(stage, "invalid-data", "Retain the original data; use Data & recovery or reimport a verified module/drawing."),
            IOException => new(stage, "file-unavailable", "Close other HEIMDALL instances and check available disk space, then retry."),
            _ => new(stage, "read-failed", "Inspect the local Error log for this stage; retain data and restore a verified backup if needed.")
        };
        #endregion END private helpers
    }
}


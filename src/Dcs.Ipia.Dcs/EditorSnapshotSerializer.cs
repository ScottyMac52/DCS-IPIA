using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs.SnapshotModels;
using Dcs.Ipia.Rendering;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Exports and verifies the complete saved editor workspace as canonical immutable JSON.</summary>
    public static class EditorSnapshotSerializer
    {
        private static readonly JsonSerializerOptions Options =
            new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        #region BEGIN Public
        /// <summary>Current editor snapshot schema; independent from the configuration schema.</summary>
        public const int CurrentSchemaVersion = 1;
        /// <summary>Freezes saved assignments, source provenance and pinned assets without reading repositories.</summary>
        /// <param name="workspace">Complete saved module workspace.</param>
        /// <returns>Canonical UTF-8 JSON and the SHA-256 of those exact bytes.</returns>
        public static ImmutableSnapshot Serialize(EditorWorkspaceSnapshot workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            GenericHardwareLayout.ValidateWorkspace(workspace);
            foreach (var revision in workspace.Diagrams.Where(x => x.CustomMapping is not null).Select(x => x.CustomMapping!).DistinctBy(x => x.Id))
                UserMappingImageVerifier.Validate(revision);
            var module = workspace.Configuration.Module.Id;
            if (workspace.Assignments.Any(x => x.Slot.ModuleId != module) ||
                workspace.Pages.Any(x => x.ModuleId != module))
                throw new ArgumentException("Pages and assignments must belong to one module.", nameof(workspace));
            if (workspace.Images.Any(x => Convert.ToHexStringLower(SHA256.HashData(x.Bytes)) != x.Sha256))
                throw new ArgumentException("Pinned image bytes do not match their source hashes.", nameof(workspace));
            var config = ConfigurationSnapshotSerializer.Serialize([workspace.Configuration]);
            using var parsed = JsonDocument.Parse(config.Content);
            var document = new EditorSnapshotDocument(
                CurrentSchemaVersion, parsed.RootElement.Clone(),
                workspace.Pages.OrderBy(x => x.Order).ThenBy(x => x.StableKey, StringComparer.Ordinal).ToArray(),
                workspace.Images.OrderBy(x => x.PageKey, StringComparer.Ordinal)
                    .ThenBy(x => x.Path, StringComparer.Ordinal)
                    .ToArray(),
                workspace.Diagrams.OrderBy(x => x.ProfileId)
                    .ThenBy(x => x.Path, StringComparer.Ordinal)
                    .Select(
                        x => x with { Controls = x.Controls.OrderBy(y => y.Control, StringComparer.Ordinal).ToArray(),
                                      Callouts = x.Callouts.OrderBy(y => y.Control, StringComparer.Ordinal)
                                                     .ThenBy(y => y.Modifier, StringComparer.Ordinal)
                                                     .ToArray() })
                    .ToArray(),
                workspace.ModuleCommands.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(),
                workspace.UiCommands.OrderBy(x => x.DcsCommandId, StringComparer.Ordinal).ThenBy(x => x.Id).ToArray(),
                workspace.Assignments.OrderBy(x => x.Slot.Key, StringComparer.Ordinal).ToArray(),
                new SortedDictionary<string, string>(
                    workspace.Labels.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
                    StringComparer.Ordinal),
                workspace.Sources is null
                    ? null
                    : new SortedDictionary<string, string>(
                          workspace.Sources.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
                          StringComparer.Ordinal))
            {
                SharedDefault = workspace.SharedDefault is null ? null : SharedUiLayerRevisionReference.Freeze(workspace.SharedDefault)
            };
            var content =
                JsonSerializer.Serialize(document, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
            return new(content, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))));
        }
        /// <summary>Restores a workspace only when its digest, schema and canonical form are valid.</summary>
        /// <param name="snapshot">Previously exported content and hash.</param>
        /// <returns>The saved workspace without consulting SQLite or source repositories.</returns>
        public static EditorWorkspaceSnapshot Deserialize(ImmutableSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot.Content))),
                               snapshot.Sha256, StringComparison.Ordinal))
                throw new InvalidDataException("Editor snapshot SHA-256 mismatch.");
            var document = JsonSerializer.Deserialize<EditorSnapshotDocument>(snapshot.Content, Options) ??
                           throw new JsonException("Editor snapshot is empty.");
            if (document.SchemaVersion != CurrentSchemaVersion)
                throw new JsonException($"Unsupported editor snapshot schema {document.SchemaVersion}.");
            var configurations = ConfigurationSnapshotSerializer.Deserialize(document.Configuration.GetRawText());
            if (configurations.Count != 1)
                throw new JsonException("Editor snapshot must contain exactly one module.");
            var workspace = new EditorWorkspaceSnapshot(configurations[0], document.Pages, document.Images,
                                                        document.Diagrams, document.ModuleCommands, document.UiCommands,
                                                        document.Assignments, document.Labels, document.Sources)
            {
                SharedDefault = document.SharedDefault is null ? null : SharedUiLayerRevisionReference.Freeze(document.SharedDefault)
            };
            if (Serialize(workspace).Content != snapshot.Content)
                throw new InvalidDataException("Editor snapshot is not canonical.");
            return workspace;
        }
        #endregion END Public
    }
}


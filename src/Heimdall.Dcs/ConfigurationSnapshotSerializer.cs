using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Heimdall.Domain;
using Heimdall.Dcs.SnapshotModels;

namespace Heimdall.Dcs
{
    /// <summary>Serializes and restores immutable module configuration snapshots.</summary>
    public static class ConfigurationSnapshotSerializer
    {
        private static readonly JsonSerializerOptions Options =
            new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        #region BEGIN Public
        /// <summary>The current schema version.</summary>
        public const int CurrentSchemaVersion = 1;
        /// <summary>Creates canonical serialized content.</summary>
        /// <param name="configurations">The configurations.</param>
        /// <returns>The result of the operation.</returns>
        public static ImmutableSnapshot Serialize(IEnumerable<ModuleConfiguration> configurations)
        {
            ArgumentNullException.ThrowIfNull(configurations);
            var modules = configurations.OrderBy(x => x.Module.DcsId, StringComparer.Ordinal)
                              .ThenBy(x => x.Module.Id)
                              .Select(ToDto)
                              .ToArray();
            var content = JsonSerializer.Serialize(new ConfigurationSnapshotDto(CurrentSchemaVersion, modules), Options)
                              .Replace("\r\n", "\n", StringComparison.Ordinal) +
                          "\n";
            return new(content,
                       Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant());
        }
        /// <summary>Reads canonical serialized content.</summary>
        /// <param name="content">The content.</param>
        /// <returns>The result of the operation.</returns>
        public static IReadOnlyList<ModuleConfiguration> Deserialize(string content)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(content);
            var snapshot = JsonSerializer.Deserialize<ConfigurationSnapshotDto>(content, Options) ??
                           throw new JsonException("Snapshot is empty.");
            if (snapshot.SchemaVersion != CurrentSchemaVersion)
                throw new JsonException($"Unsupported snapshot schema {snapshot.SchemaVersion}.");
            return snapshot.Modules.Select(FromDto).ToArray();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ConfigurationModuleDto ToDto(ModuleConfiguration value) => new(
            value.Module.Id, value.Module.DcsId, value.Module.DisplayName, value.Provenance,
            value.Profiles.Select(x => new ConfigurationProfileDto(x.Id, x.DeviceId, x.Instance, x.Role, x.SourceName))
                .ToArray(),
            value.Modifiers.Select(x => new ConfigurationModifierDto(x.Name, x.Device, x.Key, x.Mode)).ToArray(),
            value.Bindings
                .Select(x => new ConfigurationBindingDto(x.ProfileId, x.Section, x.CommandId, x.CommandName,
                                                         x.Disposition, x.InputKey, x.Reformers, x.Filter))
                .ToArray());
        private static ModuleConfiguration FromDto(ConfigurationModuleDto value) =>
            new(new(value.Id, value.DcsId, value.DisplayName),
                value.Profiles.Select(x => new DeviceProfile(x.Id, x.DeviceId, x.Instance, x.Role, x.SourceName)),
                value.Modifiers.Select(x => new DcsModifier(x.Name, x.Device, x.Key, x.Mode)),
                value.Bindings.Select(x => new DcsBinding(x.ProfileId, x.Section, x.CommandId, x.CommandName,
                                                          x.Disposition, x.InputKey, x.Reformers, x.Filter)),
                value.Provenance);
        #endregion END private helpers
    }
}

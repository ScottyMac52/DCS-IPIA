using System.Text.Json;
using Heimdall.Application;
using Heimdall.Tests.Fakes;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>Represents snapshot tests.</summary>
    public sealed class SnapshotTests
    {
        #region BEGIN Public
        /// <summary>full snapshot is deterministic hashed and lossless.</summary>
        [Fact]
        public void Full_snapshot_is_deterministic_hashed_and_lossless()
        {
            var huey = Fixture("UH-1H", "Huey", 1);
            var tomcat = Fixture("F-14", "Tomcat", 2);
            var first = DcsConfigurationSnapshotSerializer.Serialize([huey, tomcat]);
            var second = DcsConfigurationSnapshotSerializer.Serialize([tomcat, huey]);
            Assert.Equal(first, second);
            Assert.Equal(64, first.Sha256.Length);
            var restored = DcsConfigurationSnapshotSerializer.Deserialize(first.Content);
            Assert.Equal(first.Content, DcsConfigurationSnapshotSerializer.Serialize(restored).Content);
            Assert.EndsWith("\n", first.Content, StringComparison.Ordinal);
        }
        /// <summary>snapshot rejects invalid content.</summary>
        [Fact]
        public void Snapshot_rejects_invalid_content()
        {
            Assert.Throws<ArgumentNullException>(() => DcsConfigurationSnapshotSerializer.Serialize(null!));
            Assert.Throws<ArgumentException>(() => DcsConfigurationSnapshotSerializer.Deserialize(""));
            Assert.Throws<JsonException>(() => DcsConfigurationSnapshotSerializer.Deserialize("null"));
            Assert.Throws<JsonException>(
                () => DcsConfigurationSnapshotSerializer.Deserialize("{\"schemaVersion\":99,\"modules\":[]}"));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DcsModuleConfiguration Fixture(string dcs, string name, int seed)
        {
            var id = new Guid(seed, 0, 0, new byte[8]);
            var profile = new DcsDeviceProfile(new Guid(seed + 10, 0, 0, new byte[8]), "tm-mfd", $"MFD{seed}", "panel",
                                            "profile.lua");
            return new(new(id, dcs, name), [profile], [],
                       [new(profile.Id, BindingSection.Key, "c", "Command", BindingDisposition.Added, "JOY_BTN1")],
                       $"repo@{seed}");
        }
        #endregion END private helpers
    }
}

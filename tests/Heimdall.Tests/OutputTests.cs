using Heimdall.Application;
using Heimdall.Tests.Fakes;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Heimdall.Packaging;
using Heimdall.Rendering;
using Microsoft.Data.Sqlite;
using Xunit;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>Represents output tests.</summary>
    public sealed class OutputTests
    {
        private static readonly ModuleDto Huey =
            new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "UH-1H", "Huey");
        private static readonly ModuleDto Tomcat =
            new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "F-14", "Tomcat | B(U)");
        #region BEGIN Public
        /// <summary>snapshot is deterministic and round trips.</summary>
        [Fact]
        public void Snapshot_is_deterministic_and_round_trips()
        {
            var first = CanonicalSnapshotSerializer.Serialize([Huey, Tomcat]);
            Assert.Equal(first, CanonicalSnapshotSerializer.Serialize([Tomcat, Huey]));
            Assert.Equal([Tomcat, Huey], CanonicalSnapshotSerializer.Deserialize(first));
        }
        /// <summary>snapshot validates input.</summary>
        [Fact]
        public void Snapshot_validates_input()
        {
            Assert.Throws<ArgumentNullException>(() => CanonicalSnapshotSerializer.Serialize(null!));
            Assert.Throws<ArgumentException>(() => CanonicalSnapshotSerializer.Deserialize(""));
            Assert.Throws<System.Text.Json.JsonException>(() => CanonicalSnapshotSerializer.Deserialize("null"));
        }
        /// <summary>markdown is ordered and escaped.</summary>
        [Fact]
        public void Markdown_is_ordered_and_escaped()
        {
            var value = ModuleMarkdownRenderer.Render([Huey, Tomcat]);
            Assert.True(value.IndexOf("F-14", StringComparison.Ordinal) <
                        value.IndexOf("UH-1H", StringComparison.Ordinal));
            Assert.Contains("Tomcat \\| B(U)", value, StringComparison.Ordinal);
            Assert.Throws<ArgumentNullException>(() => ModuleMarkdownRenderer.Render(null!));
        }
        /// <summary>manifest is unique and ordered.</summary>
        [Fact]
        public void Manifest_is_unique_and_ordered()
        {
            var value = PackageManifestBuilder.Build([Huey, Tomcat, Huey]);
            Assert.Equal("1", value.SchemaVersion);
            Assert.Equal(["F-14", "UH-1H"], value.ModuleIds);
            Assert.Throws<ArgumentNullException>(() => PackageManifestBuilder.Build(null!));
        }
        #endregion END Public
    }
}

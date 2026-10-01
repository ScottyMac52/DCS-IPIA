using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Infrastructure;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks source-free creation, identity safeguards and isolated real SQLite persistence.</summary>
    public sealed class NewModuleWorkspaceFactoryTests
    {
        #region BEGIN Public
        /// <summary>Rejects path traversal, reserved folders and invalid display names.</summary>
        /// <param name="id">Unsafe DCS input folder.</param>
        [Theory]
        [InlineData("../aircraft")]
        [InlineData("aircraft/joystick")]
        [InlineData("aircraft\\joystick")]
        [InlineData("UiLayer")]
        [InlineData("CON")]
        [InlineData("nul.txt")]
        [InlineData(" aircraft")]
        [InlineData("aircraft.")]
        [InlineData("")]
        public void Unsafe_identity_is_rejected(string id)
        {
            Assert.Throws<ArgumentException>(() => NewModuleWorkspaceFactory.ValidateIdentity(id, "Aircraft"));
            Assert.Throws<ArgumentException>(() => NewModuleWorkspaceFactory.ValidateIdentity("AIRCRAFT", ""));
            Assert.Throws<ArgumentException>(() => NewModuleWorkspaceFactory.ValidateIdentity("AIRCRAFT", "bad\nname"));
        }

        /// <summary>Creates and reopens distinct local modules without consumer repositories, preserving their default pins.</summary>
        /// <returns>Completion of real SQLite checks.</returns>
        [Fact]
        public async Task New_module_round_trips_without_sources_and_duplicate_identity_does_not_overwrite()
        {
            var path = Path.Combine(Path.GetTempPath(), $"ipia-create-{Guid.NewGuid():N}.db");
            var connection = "Data Source=" + path + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var sources = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = "return {}",
                    ["assets/shared/ui-layer/functions.json"] = """{"schemaVersion":1,"functions":[{"id":"pause","command":"d3001pnilu3001cdnilvd1vpnilvunil","label":"Pause","category":"General"}]}"""
                };
                var commit = new string('a', 40);
                var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(commit + "\n" + JsonSerializer.Serialize(sources))));
                var defaults = new SharedUiLayerWorkspace(1, commit, sources, digest);
                var hardware = new Dictionary<string, string> { ["config/ipia-hardware.json"] = "[]" };
                var first = NewModuleWorkspaceFactory.Create("TEST-A", "Test aircraft A", defaults, hardware);
                var second = NewModuleWorkspaceFactory.Create("TEST-B", "Test aircraft B", defaults, hardware);
                Assert.NotEqual(first.Configuration.Module.Id, second.Configuration.Module.Id);
                Assert.Empty(first.ModuleCommands);
                Assert.Empty(first.Assignments);
                Assert.Single(first.UiCommands);
                Assert.NotSame(sources, first.SharedDefault!.Sources);
                await repository.SaveAsync(first);
                await repository.SaveAsync(second);
                var reopened = (await repository.GetAsync(first.Configuration.Module.Id))!;
                Assert.Equal("TEST-A", reopened.Configuration.Module.DcsId);
                Assert.Equal(digest, reopened.SharedDefault!.ContentSha256);
                Assert.Equal(first.Sources!["config/kneeboard.json"], reopened.Sources!["config/kneeboard.json"]);
                var duplicate = NewModuleWorkspaceFactory.Create("test-a", "Duplicate", defaults, hardware);
                await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => repository.SaveAsync(duplicate));
                Assert.Equal(2, (await repository.ListAsync()).Count);
                Assert.Equal("Test aircraft A", (await repository.GetAsync(first.Configuration.Module.Id))!.Configuration.Module.DisplayName);
                sources["assets/shared/ui-layer/functions.json"] = "{}";
                Assert.Equal(digest, (await repository.GetAsync(second.Configuration.Module.Id))!.SharedDefault!.ContentSha256);
                Assert.Throws<InvalidDataException>(() => NewModuleWorkspaceFactory.Create("TEST-C", "Corrupt baseline", defaults, hardware));
            }
            finally
            {
                File.Delete(path);
            }
        }
        #endregion END Public
    }
}

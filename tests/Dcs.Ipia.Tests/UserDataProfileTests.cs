using System.Security.Cryptography;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;
using Dcs.Ipia.Application;

namespace Dcs.Ipia.Tests
{
    /// <summary>Protects production bytes across profile selection, setup rerun and recoverable reset.</summary>
    public sealed class UserDataProfileTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ipia-profiles-" + Guid.NewGuid().ToString("N"));
        #region BEGIN Public
        /// <summary>Rejects path traversal, root paths and Windows reserved device names.</summary>
        [Theory]
        [InlineData("")]
        [InlineData("../production")]
        [InlineData("/")]
        [InlineData("fresh/test")]
        [InlineData("CON")]
        [InlineData("lpt1")]
        [InlineData("trailing.")]
        public void Rejects_unsafe_names(string name) =>
            Assert.Throws<ArgumentException>(() => UserDataProfile.Select(_root, name));

        /// <summary>Root ambiguity and links cannot redirect a profile onto other data.</summary>
        [Fact]
        public void Rejects_root_and_linked_paths()
        {
            Assert.Throws<ArgumentException>(() => UserDataProfile.Select(Path.GetPathRoot(_root)!));
            Directory.CreateDirectory(_root);
            var outside = Path.Combine(_root, "outside");
            Directory.CreateDirectory(outside);
            Directory.CreateSymbolicLink(Path.Combine(_root, "DCS-IPIA-TestProfiles"), outside);
            Assert.Throws<IOException>(() => UserDataProfile.Select(_root, "test"));
            Directory.Delete(Path.Combine(_root, "DCS-IPIA-TestProfiles"));
        }

        /// <summary>Selection, settings, assets, temp files and onboarding survive restart independently.</summary>
        [Fact]
        public async Task Test_profile_reset_preserves_production_and_has_verified_recovery()
        {
            var production = UserDataProfile.Select(_root);
            var test = UserDataProfile.Select(_root, "Fresh");
            var second = UserDataProfile.Select(_root, "other");
            Assert.False(production.IsTest);
            Assert.True(test.IsTest);
            Assert.Equal(test.DataDirectory, UserDataProfile.Select(_root, "FRESH").DataDirectory);
            Assert.NotEqual(second.DataDirectory, test.DataDirectory);
            await Create(production, "production");
            await Create(test, "test");
            var productionSettings = Settings(production);
            Assert.True(productionSettings.TryUpdateTheme(AppearanceTheme.Light));
            Assert.True(Settings(test).TryUpdateTheme(AppearanceTheme.Dark));
            Assert.Equal(AppearanceTheme.Dark, Settings(UserDataProfile.Select(_root, "fresh")).Current.Theme);
            Assert.Equal(AppearanceTheme.Light, Settings(production).Current.Theme);
            SqliteConnection.ClearAllPools();
            var before = Hashes(production.DataDirectory);
            var lifecycle = new UserDataProfileService(test, "1.0");
            Assert.False(lifecycle.SetupCompleted);
            lifecycle.SetSetupCompleted(true);
            Assert.True(new UserDataProfileService(UserDataProfile.Select(_root, "fresh"), "1.0").SetupCompleted);
            lifecycle.SetSetupCompleted(false);
            lifecycle.SetSetupCompleted(false);
            Assert.False(lifecycle.SetupCompleted);
            Assert.Equal("test", File.ReadAllText(Path.Combine(test.DataDirectory, "hardware", "custom.txt")));
            var backup = Path.Combine(_root, "before-reset.zip");
            var retained = lifecycle.Reset(true, backup);
            Assert.True(Directory.Exists(retained));
            Assert.True(File.Exists(Path.Combine(retained, "hardware", "custom.txt")));
            Assert.False(File.Exists(Path.Combine(test.DataDirectory, "hardware", "custom.txt")));
            Assert.False(lifecycle.SetupCompleted);
            var recovery = new UserDataRecoveryService(test.DataDirectory, "1.0");
            Assert.Equal(AppearanceTheme.Light, Settings(test).Current.Theme);
            Assert.True(recovery.Inspect(backup).FileCount > 1);
            recovery.Restore(backup);
            Assert.Equal(AppearanceTheme.Dark, Settings(test).Current.Theme);
            Assert.Equal("test", File.ReadAllText(Path.Combine(test.DataDirectory, "hardware", "custom.txt")));
            Assert.Equal(before, Hashes(production.DataDirectory));
        }

        /// <summary>Cancellation, an existing archive and in-profile backup destinations preserve all data.</summary>
        [Fact]
        public async Task Failed_reset_does_not_replace_data()
        {
            var profile = UserDataProfile.Select(_root, "test");
            await Create(profile, "saved");
            var before = Hashes(profile.DataDirectory);
            var service = new UserDataProfileService(profile, "1.0");
            Assert.Throws<InvalidOperationException>(() => service.Reset(false, Path.Combine(_root, "cancel.zip")));
            Assert.Throws<ArgumentException>(() => service.Reset(true, Path.Combine(profile.DataDirectory, "bad.zip")));
            Assert.Throws<ArgumentException>(() => service.Reset(true, Path.Combine(UserDataProfile.Select(_root).DataDirectory, "bad.zip")));
            Assert.Throws<ArgumentException>(() => service.Reset(true, UserDataProfile.Select(_root).DataDirectory));
            var occupied = Path.Combine(_root, "occupied.zip");
            File.WriteAllText(occupied, "keep");
            Assert.Throws<IOException>(() => service.Reset(true, occupied));
            Assert.Equal("keep", File.ReadAllText(occupied));
            Assert.Equal(before, Hashes(profile.DataDirectory));
        }
        /// <summary>A broken SQLite database prevents reset because a recoverable backup cannot be verified.</summary>
        [Fact]
        public void Invalid_database_blocks_reset()
        {
            var profile = UserDataProfile.Select(_root, "broken");
            Directory.CreateDirectory(profile.DataDirectory);
            File.WriteAllText(Path.Combine(profile.DataDirectory, "ipia.db"), "corrupt");
            var service = new UserDataProfileService(profile, "1.0");
            Assert.Throws<SqliteException>(() => service.Reset(true, Path.Combine(_root, "failed.zip")));
            Assert.Equal("corrupt", File.ReadAllText(Path.Combine(profile.DataDirectory, "ipia.db")));
            Assert.False(File.Exists(Path.Combine(_root, "failed.zip")));
        }

        /// <summary>A test service never opens stale production deployments or touches a real destination.</summary>
        [Fact]
        public async Task Test_deployment_is_blocked()
        {
            Directory.CreateDirectory(_root);
            var sentinel = Path.Combine(_root, "Saved Games.txt");
            File.WriteAllText(sentinel, "unchanged");
            var service = new IsolatedFlyNowService();
            Assert.Null(await service.GetActiveAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeployAsync(Guid.NewGuid(), _root));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ClearAsync());
            Assert.Equal("unchanged", File.ReadAllText(sentinel));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
            GC.SuppressFinalize(this);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static async Task Create(UserDataProfile profile, string value)
        {
            Directory.CreateDirectory(profile.DataDirectory);
            await new SqliteMigrationRunner(new SqliteConnectionStringBuilder
            { DataSource = Path.Combine(profile.DataDirectory, "ipia.db"), Pooling = false }.ToString()).MigrateAsync();
            Directory.CreateDirectory(Path.Combine(profile.DataDirectory, "hardware"));
            File.WriteAllText(Path.Combine(profile.DataDirectory, "hardware", "custom.txt"), value);
            File.WriteAllText(Path.Combine(profile.DataDirectory, "appsettings.json"), value);
            Directory.CreateDirectory(profile.TemporaryDirectory);
            File.WriteAllText(Path.Combine(profile.TemporaryDirectory, "work.txt"), value);
        }
        private static string[] Hashes(string directory) => Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(path => Path.GetRelativePath(directory, path) + ":" +
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))).ToArray();
        private static SqliteAppSettingsService Settings(UserDataProfile profile) => new(
            new SqliteConnectionStringBuilder { DataSource = Path.Combine(profile.DataDirectory, "ipia.db"), Pooling = false }.ToString(),
            Path.Combine(profile.DataDirectory, "defaults.json"), Path.Combine(profile.DataDirectory, "user-settings.json"),
            Path.Combine(profile.DataDirectory, "theme.txt"), Path.Combine(profile.DataDirectory, "appsettings.json"));
        #endregion END private helpers
    }
}

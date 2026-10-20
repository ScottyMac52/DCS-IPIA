using Heimdall.Infrastructure;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects existing profiles, licenses and preferences during the source/product rename.</summary>
    public sealed class ProductDataPathsTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "heimdall-paths-" + Guid.NewGuid().ToString("N"));
        #region BEGIN Public
        /// <summary>Fresh installs use HEIMDALL directories for production, test profiles and licensing.</summary>
        [Fact]
        public void Fresh_install_uses_new_names()
        {
            Assert.Equal(Path.Combine(_root, "HEIMDALL"), UserDataProfile.Select(_root).DataDirectory);
            Assert.Equal(Path.Combine(_root, "HEIMDALL-TestProfiles", "fresh"), UserDataProfile.Select(_root, "Fresh").DataDirectory);
            Assert.Equal(Path.Combine(_root, "VyperIndustries", "HEIMDALL-Licensing", "owner-license.json"), ProductDataPaths.OwnerLicenseFile(_root));
            Assert.Equal(Path.Combine(_root, "HEIMDALL", "workspace-layout.json"), ProductDataPaths.LayoutFile(_root));
        }
        /// <summary>Logging to a new directory cannot hide a legacy production database or named test profile.</summary>
        [Fact]
        public void Existing_data_is_selected_without_moving_or_overwriting_it()
        {
            var legacy = Path.Combine(_root, "DCS-IPIA");
            Directory.CreateDirectory(legacy);
            File.WriteAllText(Path.Combine(legacy, "ipia.db"), "existing database bytes");
            Directory.CreateDirectory(Path.Combine(_root, "HEIMDALL", "Logs"));
            Assert.Equal(legacy, UserDataProfile.Select(_root).DataDirectory);
            var oldTest = Path.Combine(_root, "DCS-IPIA-TestProfiles", "pilot");
            Directory.CreateDirectory(oldTest);
            Directory.CreateDirectory(Path.Combine(_root, "HEIMDALL-TestProfiles"));
            Assert.Equal(oldTest, UserDataProfile.Select(_root, "pilot").DataDirectory);
            Assert.Equal("existing database bytes", File.ReadAllText(Path.Combine(legacy, "ipia.db")));
            var current = Path.Combine(_root, "HEIMDALL");
            File.WriteAllText(Path.Combine(current, "ipia.db"), "explicit new profile");
            Assert.Equal(current, UserDataProfile.Select(_root).DataDirectory);
            var newTest = Path.Combine(_root, "HEIMDALL-TestProfiles", "pilot");
            Directory.CreateDirectory(newTest);
            Assert.Equal(newTest, UserDataProfile.Select(_root, "pilot").DataDirectory);
        }
        /// <summary>Legacy license/layout files remain usable; explicit current files take precedence.</summary>
        [Fact]
        public void Existing_license_and_layout_are_preserved()
        {
            var oldLicense = Path.Combine(_root, "VyperIndustries", "DCS-IPIA-Licensing", "owner-license.json");
            var newLicense = Path.Combine(_root, "VyperIndustries", "HEIMDALL-Licensing", "owner-license.json");
            var oldLayout = Path.Combine(_root, "DCS-IPIA", "workspace-layout.json");
            var newLayout = Path.Combine(_root, "HEIMDALL", "workspace-layout.json");
            foreach (var path in new[] { oldLicense, oldLayout })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "existing bytes");
            }
            Assert.Equal(oldLicense, ProductDataPaths.OwnerLicenseFile(_root));
            Assert.Equal(oldLayout, ProductDataPaths.LayoutFile(_root));
            foreach (var path in new[] { newLicense, newLayout })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "current bytes");
            }
            Assert.Equal(newLicense, ProductDataPaths.OwnerLicenseFile(_root));
            Assert.Equal(newLayout, ProductDataPaths.LayoutFile(_root));
            Assert.Equal("existing bytes", File.ReadAllText(oldLicense));
            Assert.Equal("existing bytes", File.ReadAllText(oldLayout));
        }
        /// <summary>Reset backups cannot be written into either generation of managed profile directories.</summary>
        [Theory]
        [InlineData("HEIMDALL")]
        [InlineData("HEIMDALL-TestProfiles")]
        [InlineData("DCS-IPIA")]
        [InlineData("DCS-IPIA-TestProfiles")]
        public void Backup_destination_protects_both_generations(string directory) =>
            Assert.Throws<ArgumentException>(() => UserDataProfile.Select(_root).ValidateBackupDestination(Path.Combine(_root, directory, "backup.zip")));
        /// <inheritdoc/>
        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
            GC.SuppressFinalize(this);
        }
        #endregion END Public
    }
}

using Heimdall.App;
using Xunit;

namespace Heimdall.Tests
{

    /// <summary>Verifies local layout storage and recovery independently of WPF.</summary>
    public sealed class WorkspaceLayoutStoreTests : IDisposable
    {
        private readonly string _previous = WorkspaceLayoutStore.FilePath;
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ipia-layout-tests-" + Guid.NewGuid().ToString("N"));

        #region BEGIN Public
        public WorkspaceLayoutStoreTests() => WorkspaceLayoutStore.FilePath = Path.Combine(_root, "layout.json");

        /// <summary>Checks persisted layout behavior without changing module storage.</summary>
        [Fact]
        public void Missing_preferences_use_default_without_creating_a_file()
        {
            Assert.Equal(.3, WorkspaceLayoutStore.Read("split", .3, .15, .7));
            Assert.False(File.Exists(WorkspaceLayoutStore.FilePath));
        }

        /// <summary>Checks persisted layout behavior without changing module storage.</summary>
        [Fact]
        public void Values_survive_reload_without_erasing_other_preferences()
        {
            WorkspaceLayoutStore.Write("split", .4);
            WorkspaceLayoutStore.Write("zoom", 1.5);
            Assert.Equal(.4, WorkspaceLayoutStore.Read("split", .3, .15, .7));
            Assert.Equal(1.5, WorkspaceLayoutStore.Read("zoom", 0, 0, 5));
        }

        /// <summary>Checks persisted layout behavior without changing module storage.</summary>
        [Theory]
        [InlineData(-100, .15)]
        [InlineData(100, .7)]
        public void Saved_geometry_is_clamped(double saved, double expected)
        {
            WorkspaceLayoutStore.Write("split", saved);
            Assert.Equal(expected, WorkspaceLayoutStore.Read("split", .3, .15, .7));
        }

        /// <summary>Checks persisted layout behavior without changing module storage.</summary>
        [Theory]
        [InlineData("invalid json")]
        [InlineData("null")]
        [InlineData("{\"zoom\":\"bad\"}")]
        public void Corrupt_preferences_can_be_replaced_without_blocking_the_workspace(string text)
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(WorkspaceLayoutStore.FilePath, text);
            Assert.Equal(1, WorkspaceLayoutStore.Read("zoom", 1, 0, 5));
            WorkspaceLayoutStore.Write("zoom", .5);
            Assert.Equal(.5, WorkspaceLayoutStore.Read("zoom", 1, 0, 5));
        }

        /// <summary>Checks persisted layout behavior without changing module storage.</summary>
        [Fact]
        public void Nonfinite_values_do_not_overwrite_valid_preferences()
        {
            WorkspaceLayoutStore.Write("zoom", 1.5);
            WorkspaceLayoutStore.Write("zoom", double.NaN);
            WorkspaceLayoutStore.Write("zoom", double.PositiveInfinity);
            Assert.Equal(1.5, WorkspaceLayoutStore.Read("zoom", 1, 0, 5));
        }

        /// <summary>Checks persisted layout behavior without changing module storage.</summary>
        [Fact]
        public void An_unavailable_preferences_location_does_not_block_editing()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "not-a-directory"), "occupied");
            WorkspaceLayoutStore.FilePath = Path.Combine(_root, "not-a-directory", "layout.json");
            WorkspaceLayoutStore.Write("zoom", 2);
            Assert.Equal(1, WorkspaceLayoutStore.Read("zoom", 1, 0, 5));
        }

        /// <summary>Restores the store location and removes the isolated test profile.</summary>
        public void Dispose()
        {
            WorkspaceLayoutStore.FilePath = _previous;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
            GC.SuppressFinalize(this);
        }
        #endregion END Public
    }
}

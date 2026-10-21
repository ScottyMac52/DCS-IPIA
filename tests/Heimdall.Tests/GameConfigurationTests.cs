using System.Text.Json;
using Heimdall.Application.Games;
using Heimdall.EliteDangerous;
using Heimdall.Infrastructure;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks independent configuration variants, native names, collisions and revision-checked deletion.</summary>
    public sealed class GameConfigurationTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "heimdall-configurations-" + Guid.NewGuid().ToString("N"));
        #region BEGIN Public
        /// <summary>New clears every authored slot while Duplicate retains all data in an isolated document.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Variants_are_independent_and_export_with_their_own_names(bool clear)
        {
            var backend = new EliteDangerousProfileBackend(); var source = Source();
            source.ControllerOrder.Add(source.Profile.Devices[0].LogicalId);
            var frozen = JsonSerializer.Serialize(source);
            var copy = GameConfigurations.Create(source, "Combat & Exploration", clear);
            Assert.NotEqual(source.Id, copy.Id); Assert.Equal("Combat & Exploration", copy.Name);
            Assert.Equal(source.ControllerOrder, copy.ControllerOrder);
            Assert.Equal(source.Profile.Devices.Count, copy.Profile.Devices.Count);
            Assert.Equal(source.Profile.Metadata, copy.Profile.Metadata);
            var plan = backend.ExportNamed(copy.Profile, copy.Name); Assert.True(plan.Succeeded);
            var xml = plan.Files["Combat & Exploration.4.2.binds"];
            Assert.Contains("PresetName=\"Combat &amp; Exploration\"", xml);
            Assert.Contains("FutureSetting", xml);
            var roundtrip = backend.Import("controls", plan.Files);
            Assert.Equal(backend.GetCatalog(source.Profile).Commands.Count, backend.GetCatalog(roundtrip).Commands.Count);
            if (clear)
            {
                Assert.Empty(roundtrip.Devices.SelectMany(x => x.Buttons)); Assert.Empty(roundtrip.Devices.SelectMany(x => x.Axes));
                Assert.DoesNotContain("<Modifier", xml);
            }
            else
            {
                Assert.Equal(source.Profile.Devices.Sum(x => x.Buttons.Count), roundtrip.Devices.Sum(x => x.Buttons.Count));
                Assert.Equal(source.Profile.Devices.Sum(x => x.Axes.Count), roundtrip.Devices.Sum(x => x.Axes.Count));
            }
            copy.ControllerOrder.Clear(); copy.Profile.Devices.Clear(); copy.Profile.Metadata.Clear();
            Assert.Equal(frozen, JsonSerializer.Serialize(source));
        }
        /// <summary>Several configurations reopen independently and case-insensitive names cannot collide.</summary>
        [Fact]
        public void Save_rename_and_multiple_configurations_preserve_identities()
        {
            var store = new FileGameWorkspaceStore(_root); var source = Source();
            var revision = store.Save(source, null);
            var second = GameConfigurations.Create(source, "Exploration", false); var otherRevision = store.Save(second, null);
            Assert.Equal(2, new FileGameWorkspaceStore(_root).List().Count);
            Assert.Throws<InvalidOperationException>(() => store.Save(GameConfigurations.Create(source, "exploration", false), null));
            Assert.Throws<InvalidOperationException>(() => store.Save(source with { Name = "EXPLORATION" }, revision));
            Assert.Equal(revision, store.Load(source.Id).Revision);
            var renamed = source with { Name = "Trading" }; var renamedRevision = store.Save(renamed, revision);
            Assert.Equal(source.Id, store.Load(source.Id).Workspace.Id); Assert.Equal("Trading", store.Load(source.Id).Workspace.Name);
            Assert.Equal(otherRevision, store.Load(second.Id).Revision);
            Assert.Throws<InvalidOperationException>(() => store.Delete(source.Id, revision));
            Assert.Equal(renamedRevision, store.Load(source.Id).Revision);
            store.Delete(source.Id, renamedRevision);
            Assert.Single(store.List()); Assert.Equal(second.Id, store.List()[0].Id);
            Assert.Throws<FileNotFoundException>(() => store.Load(source.Id));
            Assert.Throws<FileNotFoundException>(() => store.Delete(source.Id, renamedRevision));
            Assert.Throws<ArgumentException>(() => store.Delete(second.Id, ""));
            Assert.Throws<ArgumentException>(() => store.Delete(Guid.Empty, otherRevision));
            store.Delete(second.Id, otherRevision); Assert.Empty(store.List());
        }
        /// <summary>Invalid preset names are rejected consistently before creating any saved configuration.</summary>
        [Theory]
        [InlineData("")][InlineData(" ")][InlineData("Trailing.")][InlineData(" Leading")]
        [InlineData("CON")][InlineData("LPT1")][InlineData("../escape")][InlineData("A:B")]
        [InlineData("A\\B")][InlineData("A<B")][InlineData("A\nB")]
        public void Invalid_names_cannot_be_saved_or_duplicated(string name)
        {
            var source = Source(); var store = new FileGameWorkspaceStore(_root);
            Assert.ThrowsAny<Exception>(() => store.Save(source with { Name = name }, null));
            Assert.Throws<InvalidOperationException>(() => GameConfigurations.Create(source, name, false));
            Assert.Empty(store.List());
        }
        /// <summary>Length limits and ordinary spaces share the exporter contract.</summary>
        [Fact]
        public void Names_use_the_shared_native_export_contract()
        {
            GameConfigurationName.Validate("Scott Combat"); GameConfigurationName.Validate(new string('x', 100));
            Assert.Throws<InvalidOperationException>(() => GameConfigurationName.Validate(new string('x', 101)));
            Assert.Throws<InvalidOperationException>(() => GameConfigurationName.Validate(null!));
        }
        /// <summary>Releases temporary saved configurations.</summary>
        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); GC.SuppressFinalize(this); }
        #endregion END Public
        #region BEGIN private helpers
        private static GameWorkspace Source()
        {
            var backend = new EliteDangerousProfileBackend();
            var profile = backend.Import("controls", new Dictionary<string, string> { ["Synthetic.4.2.binds"] =
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EliteDangerous", "Synthetic.4.2.binds")) });
            return new(1, Guid.NewGuid(), "Combat", profile, new());
        }
        #endregion END private helpers
    }
}

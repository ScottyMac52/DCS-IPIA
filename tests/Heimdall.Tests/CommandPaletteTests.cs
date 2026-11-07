using Heimdall.Application;
using Heimdall.Domain;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Verifies palette ownership, provenance limits, and deterministic searching.</summary>
    public sealed class CommandPaletteTests
    {
        #region BEGIN Public
        /// <summary>Imported bindings yield distinct module commands without leaking canonical UI Layer commands.</summary>
        [Fact]
        public void Module_palette_contains_only_imported_commands()
        {
            var profile = Guid.NewGuid();
            var bindings = new[] { new DcsBinding(profile, BindingSection.Key, "iCommandFoo", "Trim",
                                                  BindingDisposition.Added, "JOY_BTN1"),
                                   new DcsBinding(profile, BindingSection.Key, "iCommandFoo", "Trim",
                                                  BindingDisposition.Added, "JOY_BTN2"),
                                   new DcsBinding(profile, BindingSection.Axis, "iCommandAxis", "Collective",
                                                  BindingDisposition.Added, "JOY_X") };
            var aircraft = DcsCommandPalette.FromBindings(bindings);
            var ui =
                DcsCommandPalette.FromUiLayer([new(Guid.NewGuid(), Guid.NewGuid(), "UI_ZOOM", "VR Zoom", "View", false)]);
            Assert.Equal(2, aircraft.Count);
            Assert.Equal("Collective", aircraft[0].Name);
            Assert.True(aircraft[0].IsAxis);
            Assert.All(aircraft, x => Assert.Equal("Uncategorized", x.Category));
            Assert.Single(DcsCommandPalette.Search(aircraft, "FOO"));
            Assert.Empty(DcsCommandPalette.Search(aircraft, "VR Zoom"));
            Assert.Single(DcsCommandPalette.Search(ui, "view"));
            Assert.Empty(DcsCommandPalette.Search(ui, "Trim"));
        }
        /// <summary>Named catalog categories stay separate from key/axis command type.</summary>
        [Fact]
        public void Module_catalog_supplies_named_categories_and_unbound_commands()
        {
            var bindings = new[] { new DcsBinding(Guid.NewGuid(), BindingSection.Key, "dAuto", "Old name",
                                                  BindingDisposition.Added, "JOY_BTN1"),
                                   new DcsBinding(Guid.NewGuid(), BindingSection.Axis, "aMissing", "Throttle",
                                                  BindingDisposition.Added, "JOY_X") };
            var catalog = new[] { new DcsCommandPaletteEntry("dAuto", "Autopilot On", "Autopilot / Modes", false),
                                  new DcsCommandPaletteEntry("aCollective", "Collective", "Flight Controls", true) };
            var entries = DcsCommandPalette.FromBindings(bindings, catalog);
            Assert.Equal(3, entries.Count);
            Assert.Equal("Autopilot / Modes",
                         Assert.Single(DcsCommandPalette.Filter(entries, null, "Autopilot / Modes")).Category);
            Assert.Equal("Autopilot On", Assert.Single(entries, x => x.Id == "dAuto").Name);
            Assert.Equal("Uncategorized", Assert.Single(entries, x => x.Id == "aMissing").Category);
            Assert.True(Assert.Single(entries, x => x.Id == "aCollective").IsAxis);
            Assert.Throws<ArgumentException>(
                () => DcsCommandPalette.FromBindings(bindings,
                                                  [new DcsCommandPaletteEntry("dAuto", "Wrong type", "Autopilot", true)]));
        }
        /// <summary>Exact category and free-text search combine without leaking commands from another scope.</summary>
        [Fact]
        public void Filters_exact_category_and_search_together()
        {
            var entries = DcsCommandPalette.FromUiLayer([
                new(Guid.NewGuid(), Guid.NewGuid(), "ZOOM", "VR Zoom", "VR", false),
                new(Guid.NewGuid(), Guid.NewGuid(), "PAUSE", "Active Pause", "General", false),
                new(Guid.NewGuid(), Guid.NewGuid(), "RECENTER", "Recenter VR", "VR", false)
            ]);
            Assert.Equal(2, DcsCommandPalette.Filter(entries, null, "VR").Count);
            Assert.Equal("VR Zoom", Assert.Single(DcsCommandPalette.Filter(entries, "zoom", "VR")).Name);
            Assert.Empty(DcsCommandPalette.Filter(entries, "zoom", "General"));
            Assert.Equal(3, DcsCommandPalette.Filter(entries, " ", null).Count);
        }
        /// <summary>Search keeps category and command ID semantics and does not reorder the input palette.</summary>
        [Fact]
        public void Search_is_case_insensitive_and_source_scoped()
        {
            var catalog =
                new[] { new DcsCommandDefinition(Guid.NewGuid(), Guid.NewGuid(), "ZOOM", "VR Zoom", "View", false),
                        new DcsCommandDefinition(Guid.NewGuid(), Guid.NewGuid(), "PAUSE", "Active Pause", "Simulation",
                                                 false) };
            var entries = DcsCommandPalette.FromUiLayer(catalog);
            Assert.Equal("Active Pause", entries[0].Name);
            Assert.Equal("VR Zoom", entries[1].Name);
            Assert.Equal("VR Zoom", Assert.Single(DcsCommandPalette.Search(entries, "zoom")).Name);
            Assert.Equal("Active Pause", Assert.Single(DcsCommandPalette.Search(entries, "simulation")).Name);
            Assert.Equal(2, DcsCommandPalette.Search(entries, " ").Count);
        }
        #endregion END Public
    }
}

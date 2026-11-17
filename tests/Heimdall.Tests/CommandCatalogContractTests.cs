using System.Text.Json;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Heimdall.Squadrons;
using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks exact lookup, ambiguity and mutable-copy isolation through the shared catalog contract.</summary>
    public sealed class CommandCatalogContractTests
    {
        #region BEGIN Public
        /// <summary>Only an exact ordinal identifier matches; scopes, casing and whitespace remain significant.</summary>
        [Theory]
        [InlineData("controls/Fire", true)]
        [InlineData("controls/fire", false)]
        [InlineData("other/Fire", false)]
        [InlineData("Fire", false)]
        [InlineData(" controls/Fire", false)]
        [InlineData("controls/Fire ", false)]
        [InlineData("", false)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859", Justification = "This test deliberately exercises lookup through the shared catalog contract.")]
        public void Lookup_requires_an_exact_identifier(string id, bool found)
        {
            var command = new SimCommand { Id = "controls/Fire" };
            ICommandCatalog catalog = new CommandCatalogProbe([command]);
            Assert.Equal(found, catalog.Find(id) is not null);
            if (found) Assert.Same(command, catalog.Find(id));
        }

        /// <summary>An empty catalog returns null; ambiguity is rejected only for the requested identifier.</summary>
        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859", Justification = "This test deliberately exercises lookup through the shared catalog contract.")]
        public void Duplicate_matches_are_rejected_without_hiding_unique_commands()
        {
            ICommandCatalog empty = new CommandCatalogProbe([]);
            Assert.Null(empty.Find("missing"));
            var unique = new SimCommand { Id = "controls/Unique" };
            ICommandCatalog catalog = new CommandCatalogProbe([new() { Id = "controls/Fire" }, new() { Id = "controls/Fire" }, unique]);
            Assert.Throws<InvalidOperationException>(() => catalog.Find("controls/Fire"));
            Assert.Same(unique, catalog.Find(unique.Id));
            Assert.Null(catalog.Find("missing"));
        }

        /// <summary>All production adapters use shared lookup and retain native catalogs and export bytes after caller mutations.</summary>
        [Theory, InlineData("dcs"), InlineData("elite-dangerous"), InlineData("star-wars-squadrons"), InlineData("war-thunder")]
        public void Native_catalogs_share_lookup_and_preserve_independent_commands(string game)
        {
            var (backend, profile) = Open(game);
            ICommandCatalog catalog = backend.GetCatalog(profile);
            Assert.IsAssignableFrom<CommandCatalogBase>(catalog);
            Assert.Equal(game, catalog.SimId);
            var commands = catalog.Commands;
            Assert.NotEmpty(commands);
            Assert.Equal(commands.Count, commands.Select(command => command.Id).Distinct(StringComparer.Ordinal).Count());
            foreach (var command in commands)
            {
                Assert.Equal(JsonSerializer.Serialize(command), JsonSerializer.Serialize(catalog.Find(command.Id)));
            }
            var first = commands[0];
            var id = first.Id;
            var baseline = JsonSerializer.Serialize(commands);
            var native = backend.Export(profile);
            Assert.True(native.Succeeded);
            Assert.Null(catalog.Find("missing-catalog-command"));
            Assert.Null(catalog.Find(" " + id));
            Assert.Null(catalog.Find(id == id.ToUpperInvariant() ? id.ToLowerInvariant() : id.ToUpperInvariant()));
            var found = catalog.Find(id)!;
            Assert.NotSame(first, found);
            Assert.NotSame(first.Metadata, found.Metadata);
            found.Id = "changed"; found.DisplayName = "changed"; found.Metadata.Clear();
            first.Id = "changed"; first.DisplayName = "changed"; first.Metadata.Clear();
            Assert.Equal(baseline, JsonSerializer.Serialize(catalog.Commands));
            Assert.Equal(id, catalog.Find(id)!.Id);
            var after = backend.Export(profile);
            Assert.True(after.Succeeded);
            Assert.Equal(native.Files.OrderBy(pair => pair.Key), after.Files.OrderBy(pair => pair.Key));
        }
        #endregion END Public

        #region BEGIN private helpers
        private static (ISimProfileBackend Backend, ProfileDocument Profile) Open(string game)
        {
            var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
            if (game == "dcs")
            {
                ISimProfileBackend dcs = new DcsSimProfileBackend();
                var root = Path.Combine(fixtures, "F4U", "src");
                var sources = Directory.GetFiles(root, "*.lua", SearchOption.AllDirectories)
                    .ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText);
                return (dcs, dcs.Import("F4U-1D", sources));
            }
            var (backend, folder, file, name) = game switch
            {
                "elite-dangerous" => ((ISimProfileBackend)new EliteDangerousProfileBackend(), "EliteDangerous", "Synthetic.4.2.binds", "fixture.binds"),
                "star-wars-squadrons" => (new SquadronsProfileBackend(), "Squadrons", "Synthetic_profile_synced", SquadronsProfileBackend.FileName),
                "war-thunder" => (new WarThunderProfileBackend(), "WarThunder", "Synthetic.blk", "fixture.blk"),
                _ => throw new ArgumentOutOfRangeException(nameof(game))
            };
            return (backend, backend.Import("controls", new Dictionary<string, string> { [name] = File.ReadAllText(Path.Combine(fixtures, folder, file)) }));
        }
        #endregion END private helpers
    }
}

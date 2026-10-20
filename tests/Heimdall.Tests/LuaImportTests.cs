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
    /// <summary>Represents lua import tests.</summary>
    public sealed class LuaImportTests
    {
        private static readonly Guid ProfileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private const string Profile = """
         local diff = {
         ["keyDiffs"] = {
          ["d1"] = { ["added"] = { [1] = { ["key"] = "JOY_BTN1", ["reformers"] = { [1] = "SHIFT" } }, }, ["removed"] = { [1] = { ["key"] = "JOY_BTN2" }, }, ["name"] = "Fire \"weapon\"", },
         },
         ["axisDiffs"] = {
          ["a1"] = { ["changed"] = { [1] = { ["key"] = "JOY_X", ["filter"] = { ["deadzone"] = .1, ["saturationX"] = 1, ["saturationY"] = .9, ["curvature"] = { [1] = -0.2, [2] = 0.2 }, ["invert"] = true, ["slider"] = false, ["hardwareDetent"] = true, ["hardwareDetentAB"] = .7, ["hardwareDetentMax"] = .95 } }, }, ["name"] = "Roll", },
         },}
         return diff
         """;
        private const string Modifiers = """
         local modifiers = { ["SHIFT"] = { ["device"] = "MOZA", ["key"] = "JOY_BTN3", ["switch"] = false, }, ["MODE"] = { ["device"] = "Throttle", ["key"] = "JOY_BTN4", ["switch"] = true, }, }
         return modifiers
         """;
        #region BEGIN Public
        /// <summary>parser preserves added removed changed filters and chords.</summary>
        [Fact]
        public void Parser_preserves_added_removed_changed_filters_and_chords()
        {
            var values = DcsLuaParser.ParseProfile(Profile, ProfileId);
            Assert.Equal(3, values.Count);
            var added = Assert.Single(values, x => x.Disposition == BindingDisposition.Added);
            Assert.Equal("Fire \"weapon\"", added.CommandName);
            Assert.Equal(["SHIFT"], added.Reformers);
            Assert.Single(values, x => x.Disposition == BindingDisposition.Removed);
            var axis = Assert.Single(values, x => x.Disposition == BindingDisposition.Changed);
            Assert.Equal(BindingSection.Axis, axis.Section);
            Assert.Equal([-.2, .2], axis.Filter!.Curvature);
            Assert.True(axis.Filter.Invert is true);
            Assert.True(axis.Filter.Slider is false);
            Assert.Equal(.95, axis.Filter.HardwareDetentMax);
        }
        /// <summary>modifier parser preserves modes.</summary>
        [Fact]
        public void Modifier_parser_preserves_modes()
        {
            var values = DcsLuaParser.ParseModifiers(Modifiers);
            Assert.Equal(["MODE", "SHIFT"], values.Select(x => x.Name));
            Assert.Equal(ModifierMode.Toggle, values[0].Mode);
            Assert.Equal(ModifierMode.Hold, values[1].Mode);
        }
        [Theory]
        [InlineData("local diff = {}", "return the diff")]
        [InlineData(
            "local diff = { [\"keyDiffs\"] = { [\"c\"] = { [\"added\"] = { [1] = { [\"key\"] = \"K\" } } } } } return diff",
            "has no name")]
        [InlineData(
            "local diff = { [\"keyDiffs\"] = { [\"c\"] = { [\"added\"] = { [1] = { } }, [\"name\"] = \"N\" } } } return diff",
            "has no key")]
        [InlineData("local diff = {", "return the diff")]
        /// <summary>invalid profiles report actionable errors.</summary>
        /// <param name="source">The source.</param>
        /// <param name="message">The message.</param>
        public void Invalid_profiles_report_actionable_errors(string source, string message) => Assert.Contains(
            message, Assert.Throws<FormatException>(() => DcsLuaParser.ParseProfile(source, ProfileId)).Message,
            StringComparison.Ordinal);
        /// <summary>invalid profile arguments are rejected.</summary>
        [Fact]
        public void Invalid_profile_arguments_are_rejected()
        {
            Assert.Throws<ArgumentException>(() => DcsLuaParser.ParseProfile("", ProfileId));
            Assert.Throws<ArgumentException>(() => DcsLuaParser.ParseProfile("return diff", Guid.Empty));
            Assert.Throws<FormatException>(
                () => DcsLuaParser.ParseProfile("local diff = { [\"keyDiffs\"] = { return diff", ProfileId));
        }
        [Theory]
        [InlineData("local modifiers = {}", "return the modifiers")]
        [InlineData("return modifiers", "declare local modifiers")]
        [InlineData("local modifiers = { [\"M\"] = { [\"device\"] = \"D\" } } return modifiers", "requires device")]
        [InlineData(
            "local modifiers = { [\"M\"] = { [\"device\"] = \"D\", [\"key\"] = \"K\", [\"switch\"] = false }, [\"m\"] = { [\"device\"] = \"D2\", [\"key\"] = \"K2\", [\"switch\"] = true } } return modifiers",
            "unique ignoring case")]
        [InlineData(
            "local modifiers = { [\"M\"] = { [\"device\"] = \"D\", [\"key\"] = \"K\", [\"switch\"] = false }, [\"N\"] = { [\"device\"] = \"d\", [\"key\"] = \"k\", [\"switch\"] = true } } return modifiers",
            "physical inputs")]
        /// <summary>invalid modifiers report actionable errors.</summary>
        /// <param name="source">The source.</param>
        /// <param name="message">The message.</param>
        public void Invalid_modifiers_report_actionable_errors(string source, string message) =>
            Assert.Contains(message, Assert.Throws<FormatException>(() => DcsLuaParser.ParseModifiers(source)).Message,
                            StringComparison.Ordinal);
        /// <summary>Malformed Lua tables and quoted strings report format errors.</summary>
        [Fact]
        public void Parser_rejects_malformed_lua_tables()
        {
            Assert.Throws<FormatException>(() => DcsLuaParser.ParseModifiers("local modifiers = return modifiers"));
            Assert.Throws<FormatException>(
                () => DcsLuaParser.ParseModifiers(
                    "local modifiers = { [\"M\"] = { [\"device\"] = \"unterminated } } return modifiers"));
            Assert.Throws<FormatException>(
                () => DcsLuaParser.ParseProfile("local diff = { [\"keyDiffs\"] = broken } return diff", ProfileId));
        }
        /// <summary>importer is idempotent and reports source errors.</summary>
        /// <returns>A task that completes when the operation finishes</returns>
        [Fact]
        public async Task Importer_is_idempotent_and_reports_source_errors()
        {
            var module = new DcsModule(Guid.Parse("11111111-1111-1111-1111-111111111111"), "UH-1H", "Huey");
            var request = new ModuleImportRequest(module, [new("tm-mfd", "MFD1", "panel", "mfd.lua", Profile)],
                                                  Modifiers, "Huey@abc");
            var importer = new DcsLuaImporter();
            var first = await importer.ImportAsync(request);
            var second = await importer.ImportAsync(request);
            Assert.True(first.Succeeded);
            Assert.Equal(ConfigurationSnapshotSerializer.Serialize([first.Configuration!]).Content,
                         ConfigurationSnapshotSerializer.Serialize([second.Configuration!]).Content);
            Assert.Contains(first.Diagnostics, x => x.Code == "imported" && !x.IsError);
            var badModifiers = await importer.ImportAsync(request with { ModifiersContent = "bad" });
            Assert.False(badModifiers.Succeeded);
            Assert.Equal("invalid-modifiers", Assert.Single(badModifiers.Diagnostics).Code);
            var badProfile =
                await importer.ImportAsync(request with { Profiles = [new("d", null, "r", "bad.lua", "bad")] });
            Assert.False(badProfile.Succeeded);
            Assert.Equal("invalid-profile", Assert.Single(badProfile.Diagnostics).Code);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => importer.ImportAsync(request, new(true)));
        }
        #endregion END Public
    }
}

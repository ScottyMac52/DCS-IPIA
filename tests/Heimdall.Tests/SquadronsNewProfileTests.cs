using System.Text.Json;
using Heimdall.Squadrons;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks native blank-profile creation without inheriting archived user assignment recipes.</summary>
    public sealed class SquadronsNewProfileTests
    {
        #region BEGIN Public
        /// <summary>New clears every joystick record while retaining all system inputs, declarations and settings.</summary>
        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public void New_profile_preserves_native_context_and_has_no_joystick_assignments(string newline)
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"))
                .Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", newline, StringComparison.Ordinal);
            source += "GstKeyBinding.IncomStarshipInputConcepts.ConceptFire.2.future 7" + newline;
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var original = backend.ReadBindings(profile);
            var fresh = backend.CreateUnassignedProfile(profile);
            var rows = backend.ReadBindings(fresh);
            Assert.Equal(JsonSerializer.Serialize(original.Where(row => row.Type != 3)), JsonSerializer.Serialize(rows));
            Assert.DoesNotContain(rows, row => row.Type == 3);
            Assert.Equal(JsonSerializer.Serialize(profile.Devices.Select(device => device.Identity)),
                JsonSerializer.Serialize(fresh.Devices.Select(device => device.Identity)));
            var text = backend.Export(fresh).Files[SquadronsProfileBackend.FileName];
            Assert.Contains("Future.Setting preserved", text, StringComparison.Ordinal);
            Assert.False(fresh.NativeData["squadrons.v1"].TryGetProperty("Templates", out _));
            Assert.Equal(125, backend.GetCatalog(fresh).Commands.Count);
            Assert.Equal(text, backend.Export(backend.CreateUnassignedProfile(fresh)).Files[SquadronsProfileBackend.FileName]);
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
            Assert.Equal(text, backend.Export(backend.Import("controls",
                new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = text })).Files[SquadronsProfileBackend.FileName]);
            if (newline == "\r\n") Assert.DoesNotContain("\n", text.Replace("\r\n", "", StringComparison.Ordinal), StringComparison.Ordinal);
        }
        /// <summary>Existing cleared-record archives cannot become hidden personal prerequisites of a new profile.</summary>
        [Fact]
        public void New_profile_drops_the_prior_assignment_archive()
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var archived = backend.ClearCapturedAssignments(profile, "squadrons.joystick.3");
            Assert.True(archived.NativeData["squadrons.v1"].TryGetProperty("Templates", out _));
            var fresh = backend.CreateUnassignedProfile(archived);
            Assert.False(fresh.NativeData["squadrons.v1"].TryGetProperty("Templates", out _));
            Assert.DoesNotContain(backend.ReadBindings(fresh), row => row.Type == 3);
            Assert.DoesNotContain(backend.ReadAssignableBindings(fresh), row => row.Type == 3 && row.Fields["identifier"] == 123);
            Assert.Equal(125, backend.GetCatalog(fresh).Commands.Count);
        }
        #endregion END Public
    }
}

using Heimdall.Dcs;
using Heimdall.Domain;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Characterizes read-only Saved Games joystick folder inspection.</summary>
    public sealed class DcsFolderImporterTests
    {
        #region BEGIN Public
        /// <summary>The live folder can be selected directly without a Git checkout or a shared catalog.</summary>
        [Fact]
        public async Task Imports_huey_joystick_folder_and_adjacent_modifiers()
        {
            var root = CreateFixture();
            try
            {
                var joystick = Path.Combine(root, "joystick");
                var importer = new DcsFolderImporter();
                Assert.True(importer.HasProfiles(joystick));
                Assert.True(importer.HasProfiles(root));
                var result = await importer.ImportAsync(joystick);
                Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
                Assert.Equal(5, result.Profiles.Count);
                Assert.Single(result.Modifiers);
                Assert.Contains(result.Bindings, x => x.Section == BindingSection.Axis);
                Assert.Contains(result.Bindings, x => x.Reformers.Contains("MOZA_F16_F18_BTN3"));
                Assert.All(result.Profiles, x => Assert.Equal("unresolved device", x.Role));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
        /// <summary>Changing physical GUIDs changes profile identity without changing the display name.</summary>
        [Fact]
        public async Task Profile_id_preserves_physical_instance()
        {
            var root = CreateFixture();
            try
            {
                var importer = new DcsFolderImporter();
                var first = await importer.ImportAsync(root);
                var old = Directory.GetFiles(Path.Combine(root, "joystick"), "F16 MFD 1 *.diff.lua").Single();
                File.Move(old, old.Replace("51FA60C0-CB32-11ed-800B-444553540000",
                                           "11111111-1111-1111-1111-111111111111", StringComparison.Ordinal));
                var second = await importer.ImportAsync(root);
                Assert.NotEqual(first.Profiles.Single(x => x.DeviceId == "F16 MFD 1").Id,
                             second.Profiles.Single(x => x.DeviceId == "F16 MFD 1").Id);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
        /// <summary>Repeated controller names with different physical GUIDs remain separate profiles.</summary>
        [Fact]
        public async Task Repeated_device_names_preserve_both_physical_units()
        {
            var root = CreateFixture();
            try
            {
                var old = Directory.GetFiles(Path.Combine(root, "joystick"), "F16 MFD 1 *.diff.lua").Single();
                File.Copy(old, old.Replace("51FA60C0-CB32-11ed-800B-444553540000",
                                           "11111111-1111-1111-1111-111111111111", StringComparison.Ordinal));
                var result = await new DcsFolderImporter().ImportAsync(root);
                Assert.True(result.Succeeded);
                Assert.Equal(2, result.Profiles.Count(x => x.DeviceId == "F16 MFD 1"));
                Assert.Equal(result.Profiles.Count, result.Profiles.Select(x => x.Id).Distinct().Count());
                Assert.DoesNotContain(result.Diagnostics, x => x.Code == "ambiguous-profile");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
        /// <summary>A missing adjacent modifier file leaves an explicit diagnostic for bound chords.</summary>
        [Fact]
        public async Task Missing_modifier_definitions_are_reported()
        {
            var root = CreateFixture();
            try
            {
                File.Delete(Path.Combine(root, "modifiers.lua"));
                var result = await new DcsFolderImporter().ImportAsync(root);
                Assert.False(result.Succeeded);
                Assert.Equal(5, result.Profiles.Count);
                Assert.NotEmpty(result.Bindings);
                Assert.Contains(result.Diagnostics, x => x.Code == "missing-modifiers");
                Assert.Contains(result.Diagnostics, x => x.Code == "unknown-modifier");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
        /// <summary>Malformed Lua is identified with its source path.</summary>
        [Fact]
        public async Task Invalid_profile_is_reported()
        {
            var root = CreateFixture();
            try
            {
                var file = Directory.GetFiles(Path.Combine(root, "joystick"), "F16 MFD 1 *.diff.lua").Single();
                File.WriteAllText(file, "return {}");
                var result = await new DcsFolderImporter().ImportAsync(root);
                Assert.False(result.Succeeded);
                Assert.Equal(4, result.Profiles.Count);
                Assert.DoesNotContain(result.Profiles, x => x.DeviceId == "F16 MFD 1");
                Assert.Contains(result.Diagnostics, x => x.Code == "invalid-profile" &&
                                                         x.Message.Contains("F16 MFD 1", StringComparison.Ordinal));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
        /// <summary>An empty or unrelated folder returns diagnostics without an exception.</summary>
        [Fact]
        public async Task Missing_joystick_folder_is_reported()
        {
            var root = Path.Combine(Path.GetTempPath(), "Heimdall.AppDcsFolder-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                var importer = new DcsFolderImporter();
                Assert.False(importer.HasProfiles(root));
                var result = await importer.ImportAsync(root);
                Assert.False(result.Succeeded);
                Assert.Contains(result.Diagnostics, x => x.Code == "missing-joystick-folder");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
        /// <summary>An empty joystick directory reports missing profiles even when modifiers exist.</summary>
        [Fact]
        public async Task Empty_joystick_folder_is_reported()
        {
            var root = CreateFixture();
            try
            {
                foreach (var file in Directory.GetFiles(Path.Combine(root, "joystick"), "*.diff.lua"))
                    File.Delete(file);
                var importer = new DcsFolderImporter();
                Assert.False(importer.HasProfiles(Path.Combine(root, "joystick")));
                var result = await importer.ImportAsync(root);
                Assert.False(result.Succeeded);
                Assert.Contains(result.Diagnostics, x => x.Code == "missing-profiles");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
        /// <summary>Broken modifiers are diagnosed while valid profile files remain available for review.</summary>
        [Fact]
        public async Task Invalid_modifiers_are_reported()
        {
            var root = CreateFixture();
            try
            {
                File.WriteAllText(Path.Combine(root, "modifiers.lua"), "return {}");
                var result = await new DcsFolderImporter().ImportAsync(root);
                Assert.False(result.Succeeded);
                Assert.Equal(5, result.Profiles.Count);
                Assert.Contains(result.Diagnostics, x => x.Code == "invalid-modifiers");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
        /// <summary>Keyboard source text is captured independently and malformed keyboard profiles prevent a partial import.</summary>
        [Fact]
        public async Task Keyboard_profiles_are_frozen_and_validated()
        {
            var root = CreateFixture();
            try
            {
                var keyboard = Path.Combine(root, "keyboard");
                Directory.CreateDirectory(keyboard);
                var file = Path.Combine(keyboard, "Keyboard.diff.lua");
                const string text = "local diff = {} return diff";
                File.WriteAllText(file, text);
                var result = await new DcsFolderImporter().ImportAsync(Path.Combine(root, "joystick"));
                Assert.True(result.Succeeded);
                Assert.Equal(text, result.Sources["keyboard/Keyboard.diff.lua"]);
                Assert.Equal(5, result.Profiles.Count);
                Assert.Contains("modifiers.lua", result.Sources.Keys);
                File.WriteAllText(file, "invalid lua");
                Assert.Equal(text, result.Sources["keyboard/Keyboard.diff.lua"]);
                result = await new DcsFolderImporter().ImportAsync(root);
                Assert.False(result.Succeeded);
                Assert.Contains(result.Diagnostics, x => x.Code == "invalid-keyboard-profile");
            }
            finally { Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string CreateFixture()
        {
            var root = Path.Combine(Path.GetTempPath(), "Heimdall.AppDcsFolder-" + Guid.NewGuid().ToString("N"));
            var target = Path.Combine(root, "joystick");
            Directory.CreateDirectory(target);
            var fixtures =
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "Huey", "src", "Config", "Input", "UH-1H");
            foreach (var file in Directory.GetFiles(Path.Combine(fixtures, "joystick"), "*.diff.lua"))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            File.Copy(Path.Combine(fixtures, "modifiers.lua"), Path.Combine(root, "modifiers.lua"));
            return root;
        }
        #endregion END private helpers
    }
}

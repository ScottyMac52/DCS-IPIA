using System.Text.Json;
using Heimdall.Application;
using Heimdall.Domain;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks import of the authored kneeboard labels and Hornet POV key spelling.</summary>
    public sealed class KneeboardLabelImporterTests
    {
        #region BEGIN Public
        /// <summary>Maps Base and shifted labels to the physical DCS input and independent modifier slots.</summary>
        [Fact]
        public void Hornet_pov_and_module_labels_match_authored_kneeboard()
        {
            var module = new DcsModule(Guid.NewGuid(), "UH-1H", "Huey");
            var profile =
                new DeviceProfile(Guid.NewGuid(), "moza-ab9-hornet-grip", null, "Hornet grip", "MOZA.diff.lua");
            var configuration = new ModuleConfiguration(
                module, [profile], [new DcsModifier("MOZA_F16_F18_BTN3", "MOZA", "JOY_BTN3", ModifierMode.Hold)],
                [
                    new DcsBinding(profile.Id, BindingSection.Key, "TRIM_BASE", "Pilot Trimmer",
                                   BindingDisposition.Added, "JOY_BTN_POV1_U"),
                    new DcsBinding(profile.Id, BindingSection.Key, "TRIM_SHIFT", "Pilot Sight Elevation Increase",
                                   BindingDisposition.Added, "JOY_BTN_POV1_U", ["MOZA_F16_F18_BTN3"]),
                    new DcsBinding(profile.Id, BindingSection.Key, "ARM_BASE", "Armament Selector Up",
                                   BindingDisposition.Added, "JOY_BTN11"),
                    new DcsBinding(profile.Id, BindingSection.Key, "ARM_SHIFT", "Gun Selector Switch Up",
                                   BindingDisposition.Added, "JOY_BTN11", ["MOZA_F16_F18_BTN3"])
                ],
                "import");
            const string json = """
               {
                "labels":{"base-trim":"Pilot","shift-trim":"Pilot Sight Elevation Increase","base-arm":"Arm Up","shift-arm":"Gun Up"},
                "pages":[{
                 "deviceId":"moza-ab9-hornet-grip","deviceInstance":null,
                 "layers":[
                  {"id":"base","controls":{
                   "hornet-grip-trim-up":{"key":"JOY_BTN_POV1_U","command":"TRIM_BASE","labelId":"base-trim"},
                   "hornet-grip-vr-up":{"key":"JOY_BTN11","command":"ARM_BASE","labelId":"base-arm"}
                  }},
                  {"id":"MOZA_F16_F18_BTN3","controls":{
                   "hornet-grip-trim-up":{"key":"JOY_BTN_POV1_U","command":"TRIM_SHIFT","labelId":"shift-trim","modifiers":["MOZA_F16_F18_BTN3"]},
                   "hornet-grip-vr-up":{"key":"JOY_BTN11","command":"ARM_SHIFT","labelId":"shift-arm","modifiers":["MOZA_F16_F18_BTN3"]}
                  }}
                 ]
                }]
               }
               """;
            using var document = JsonDocument.Parse(json);
            var labels = KneeboardLabelImporter.Import(
                new Dictionary<string, JsonElement> { ["config/kneeboard.json"] = document.RootElement },
                configuration);
            var baseTrim = new DraftAssignmentSlot(module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Key,
                                                   "JOY_BTN_POV1_U");
            Assert.Equal("Pilot", labels[baseTrim.Key]);
            Assert.Equal("Pilot Sight Elevation Increase",
                         labels[(baseTrim with { ModifierChord = "MOZA_F16_F18_BTN3" }).Key]);
            var baseArm = baseTrim with { InputKey = "JOY_BTN11" };
            Assert.Equal("Arm Up", labels[baseArm.Key]);
            Assert.Equal("Gun Up", labels[(baseArm with { ModifierChord = "MOZA_F16_F18_BTN3" }).Key]);
            Assert.True(DcsInputKey.SamePhysicalInput("JOY_POV1_U", "JOY_BTN_POV1_U"));
            Assert.Equal("JOY_BTN_POV1_U",
                         DcsInputKey.Resolve(configuration, profile.Id, BindingSection.Key, "JOY_POV1_U"));
        }
        #endregion END Public
    }
}

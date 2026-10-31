using Heimdall.Domain.MultiSim;
using Heimdall.WarThunder;

namespace Heimdall.Tests
{
    /// <summary>Authored native-v5 examples contain no personal capture data or claimed physical axis mappings.</summary>
    internal static class WarThunderBackendFixture
    {
        #region BEGIN Public
        /// <summary>Gets a generic export with repeated alternatives, chords, empty commands and unknown preserved parameters.</summary>
        internal const string Source = """
            // synthetic source, retained verbatim
            controls{
              version:i=5
              basePresetPaths{default:t="config/hotkeys/synthetic.blk";}
              hotkeys{
                ID_FIRE{joyButton:i=1;activationType:i=9;}
                ID_FIRE{keyboardKey:i=30;keyboardKey:i=31;}
                ID_CHORD{joyButton:i=0;joyButton:i=2;keyboardKey:i=4;}
                ID_EMPTY{}
                ID_FUTURE{keyboardKey:i=9;future:t="retained";}
              }
              axes{
                pitch{axisId:i=1;inverse:b=no;kMul:r=1.25;mouseAxisId:i=-1;futureParam:r=2;}
                throttle{axisId:i=2;nonlinearity:i=1;}
                empty_axis{}
              }
              deviceMapping{
                joystick{devId:t="0001:0002";axesOffset:i=0;axesCount:i=2;buttonsOffset:i=0;buttonsCount:i=4;name:t="Repeated Device";}
                joystick{devId:t="0001:0002";axesOffset:i=2;axesCount:i=2;buttonsOffset:i=4;buttonsCount:i=4;name:t="Repeated Device";}
              }
              future{setting:t="leave unchanged";}
            }
            settings{cameraInvertY:b=no;}
            """;
        /// <summary>Imports only explicitly supplied text without touching game folders.</summary>
        internal static ProfileDocument Import(WarThunderProfileBackend backend, string? source = null) =>
            backend.Import("test-context", new Dictionary<string, string> { ["Test controls.blk"] = source ?? Source });
        /// <summary>Gets one explicitly selected command block from the native inventory.</summary>
        internal static WarThunderBinding Binding(WarThunderProfileBackend backend, ProfileDocument profile, string command, int occurrence = 0) =>
            backend.ReadBindings(profile).Where(binding => binding.Command == command).ElementAt(occurrence);
        /// <summary>Gets one direct scalar occurrence without relying on native path uniqueness.</summary>
        internal static WarThunderBlkScalar Field(WarThunderBinding binding, string name, int occurrence = 0) =>
            binding.Fields.Where(field => field.Path.EndsWith("/" + name, StringComparison.Ordinal)).ElementAt(occurrence);
        /// <summary>Reads the only exported native source after validating the complete plan.</summary>
        internal static string Export(WarThunderProfileBackend backend, ProfileDocument profile)
        {
            var plan = backend.Export(profile);
            Xunit.Assert.True(plan.Succeeded, string.Join("; ", plan.Diagnostics.Select(diagnostic => diagnostic.Message)));
            return Xunit.Assert.Single(plan.Files).Value;
        }
        #endregion END Public
    }
}

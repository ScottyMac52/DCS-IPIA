using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Generic authored examples exercise root-level controls, device mapping and native boolean spelling.</summary>
    public sealed class WarThunderExportShapeTests
    {
        private const string Capture = """
            controls{
              version:i=5
              hotkeys{
                ACTION_TEST{joyButton:i=1;activationType:i=2;}
                ACTION_TEST{keyboardKey:i=30;keyboardKey:i=31;}
              }
              axes{pitch{axisId:i=1;inverse:b=yes;relative:b=no;deadZone:r=0.1;}}
              deviceMapping{
                joystick{devId:t="0000:0001";axesOffset:i=0;name:t="Synthetic Stick";buttonsOffset:i=0;buttonsCount:i=4;axesCount:i=2;}
                joystick{devId:t="0000:0002";axesOffset:i=2;name:t="Synthetic Throttle";buttonsOffset:i=4;buttonsCount:i=6;axesCount:i=1;}
              }
            }
            settings{cameraInvertY:b=no;}
            """;
        #region BEGIN Public
        /// <summary>Both exported root controls and the legacy wrapped shape have populated native inventories.</summary>
        [Theory]
        [InlineData("")]
        [InlineData("content")]
        public void Root_and_wrapped_controls_retain_repeated_native_records(string wrapper)
        {
            var source = wrapper.Length == 0 ? Capture : wrapper + "{" + Capture + "}";
            var prefix = wrapper.Length == 0 ? "" : wrapper + "/";
            var controls = new WarThunderControls(source);
            Assert.False(controls.Document.HasOpaqueSyntax);
            Assert.Equal(4, controls.Hotkeys.Count);
            Assert.Equal(4, controls.Axes.Count);
            Assert.Equal(12, controls.Joysticks.Count);
            Assert.Equal(2, Assert.Single(controls.ActivationCodes()));
            Assert.Equal(2, controls.Document.Blocks.Count(block => block.Path == prefix + "controls/deviceMapping/joystick"));
            Assert.DoesNotContain(controls.EditableSettings, scalar => scalar.Path == prefix + "controls/hotkeys/ACTION_TEST/keyboardKey");
            Assert.Throws<InvalidOperationException>(() => controls.ReplaceReviewedSetting(prefix + "controls/deviceMapping/joystick/name", "\"Replacement\""));
            Assert.Equal(source.Replace("inverse:b=yes", "inverse:b=no", StringComparison.Ordinal),
                controls.ReplaceReviewedSetting(prefix + "controls/axes/pitch/inverse", "no"));
            Assert.Equal(source, controls.Document.SerializeUnchanged());
        }
        /// <summary>Native yes/no and existing integer flag literals survive capture and isolated edits.</summary>
        [Theory]
        [InlineData("yes", "no")]
        [InlineData("no", "yes")]
        [InlineData("0", "1")]
        [InlineData("1", "0")]
        public void Supported_boolean_literals_are_not_normalized(string before, string after)
        {
            var source = "root{flag:b=" + before + ";other:i=7;}";
            var document = WarThunderBlkDocument.Parse(source);
            Assert.False(document.HasOpaqueSyntax);
            Assert.Equal(before, Assert.Single(document.Scalars, scalar => scalar.Path == "root/flag").Value);
            Assert.Equal(source.Replace("flag:b=" + before, "flag:b=" + after, StringComparison.Ordinal), document.ReplaceScalar("root/flag", after));
            Assert.Equal(source, document.SerializeUnchanged());
        }
        /// <summary>Other flag tokens remain opaque evidence and cannot enter an edited document.</summary>
        [Theory]
        [InlineData("2")]
        [InlineData("-1")]
        [InlineData("true")]
        [InlineData("on")]
        public void Unsupported_boolean_literals_block_scalar_writes(string value)
        {
            var source = "root{flag:b=" + value + ";}";
            Assert.True(WarThunderBlkDocument.Parse(source).HasOpaqueSyntax);
            Assert.Equal(source, WarThunderBlkDocument.Parse(source).SerializeUnchanged());
            var known = WarThunderBlkDocument.Parse("root{flag:b=yes;}");
            Assert.Throws<ArgumentException>(() => known.ReplaceScalar("root/flag", value));
        }
        #endregion END Public
    }
}

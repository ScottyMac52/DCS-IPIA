using System;
using System.IO;
using System.Linq;
using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects lossless native capture and conservative scalar edits before desktop activation.</summary>
    public sealed class WarThunderBlkTests
    {

        private const string Capture =
            "version:i=12\r\n" +
            "content{\r\n" +
            " controls{\r\n" +
            " hotkeys{\r\n" +
            " fire{activationType:i=3;}\r\n" +
            " }\r\n" +
            " axes{pitch{deadZone:r=0.1;}}\r\n" +
            " joysticks{stick{deviceName:t=\"Captured Device\";}}\r\n" +
            " }\r\n" +
            "}\r\n";
        private static readonly int[] expectedActivationCodes = new[] { 3, 9 };

        #region BEGIN Public
        /// <summary>Unchanged export preserves the complete original native text.</summary>
        [Fact]
        public void ImportAndNoOpExportPreserveEveryCharacter()
        {
            var document = WarThunderBlkDocument.Parse(Capture);
            Assert.Equal(Capture, document.SerializeUnchanged());
            Assert.Contains(document.Blocks, x => x.Path == "content/controls/hotkeys/fire");
            Assert.Contains(document.Scalars, x => x.Path == "content/controls/hotkeys/fire/activationType" && x.Value == "3");
        }

        /// <summary>Inventory discovery reports numeric codes without assigning game meanings.</summary>
        [Fact]
        public void InventoryKeepsActivationCodesOpaque()
        {
            var controls = new WarThunderControls(Capture);

            Assert.Equal(3, Assert.Single(controls.ActivationCodes()));
            Assert.Single(controls.Axes);
            Assert.Single(controls.Joysticks);
        }

        /// <summary>A scalar replacement changes only its captured source span.</summary>
        [Fact]
        public void ReviewedReplacementOnlyChangesTargetValue()
        {
            var document = WarThunderBlkDocument.Parse(Capture);
            var output = document.ReplaceScalar("content/controls/hotkeys/fire/activationType", "4");
            Assert.Equal(Capture.Replace("activationType:i=3", "activationType:i=4", StringComparison.Ordinal), output);
            Assert.Equal(Capture, document.SerializeUnchanged());
        }

        /// <summary>Missing, ambiguous and incorrectly typed values cannot be changed.</summary>
        [Fact]
        public void InvalidAndAmbiguousEditsAreBlocked()
        {
            var document = WarThunderBlkDocument.Parse(Capture);
            Assert.Throws<InvalidOperationException>(() => document.ReplaceScalar("missing", "2"));
            Assert.Throws<ArgumentException>(() => document.ReplaceScalar("content/controls/hotkeys/fire/activationType", "not-an-integer"));
            Assert.Throws<InvalidOperationException>(() => WarThunderBlkDocument.Parse("a:i=1;a:i=2;").ReplaceScalar("a", "3"));
        }

        /// <summary>Comments and quoted text remain exact through isolated numeric changes.</summary>
        [Fact]
        public void CommentsAndQuotedStringsSurviveNativeRoundTrip()
        {
            const string capture = "// preserve this\r\nroot{ /* comment */ label:s=\"Quoted text\"; value:r=1.25; }";
            var document = WarThunderBlkDocument.Parse(capture);
            Assert.Equal(capture, document.SerializeUnchanged());
            Assert.Contains(document.Scalars, x => x.Path == "root/label" && x.Value == "\"Quoted text\"");
            Assert.Equal(capture.Replace("value:r=1.25", "value:r=2.5", StringComparison.Ordinal),
                document.ReplaceScalar("root/value", "2.5"));
            Assert.Throws<ArgumentException>(() => document.ReplaceScalar("root/label", "unquoted"));
            Assert.Throws<ArgumentException>(() => document.ReplaceScalar("root/value", "not-a-real"));
        }

        /// <summary>Rejected writes leave the immutable source available for later valid edits.</summary>
        [Fact]
        public void RejectedReplacementsNeverChangeOriginalCapture()
        {
            const string capture = "group{count:i=3;flag:b=1;}";
            var doc = WarThunderBlkDocument.Parse(capture);
            Assert.Throws<ArgumentException>(() => doc.ReplaceScalar("group/count", "bad"));
            Assert.Throws<ArgumentException>(() => doc.ReplaceScalar("group/count", "4\\nother:i=1"));
            Assert.Throws<ArgumentException>(() => doc.ReplaceScalar("group/flag", "on"));
            Assert.Throws<InvalidOperationException>(() => doc.ReplaceScalar("group/missing", "1"));
            Assert.Equal(capture, doc.SerializeUnchanged());
            Assert.Equal("group{count:i=7;flag:b=1;}", doc.ReplaceScalar("group/count", "7"));
        }

        /// <summary>Unknown numeric activation values and native identity fields remain evidence.</summary>
        [Fact]
        public void InventoryDoesNotInterpretUnknownActivationCodesOrHardwareOwnership()
        {
            const string capture = "content{controls{hotkeys{fire{activationType:i=9;}zoom{activationType:i=3;}}axes{pitch{range:r=0.9;}}joysticks{one{deviceId:i=2;}}}}";
            var controls = new WarThunderControls(capture);
            Assert.Equal(expectedActivationCodes, controls.ActivationCodes());
            Assert.Single(controls.Axes);
            Assert.Single(controls.Joysticks);
            Assert.Contains(controls.Hotkeys, x => x.Value == "9");
            Assert.Equal(capture, controls.Document.SerializeUnchanged());
        }

        /// <summary>Broken structure and binary content are outside the supported capture contract.</summary>
        [Fact]
        public void RejectsBrokenStructureAndBinaryContent()
        {
            Assert.Throws<InvalidDataException>(() => WarThunderBlkDocument.Parse("content{"));
            Assert.Throws<InvalidDataException>(() => WarThunderBlkDocument.Parse("}"));
            Assert.Throws<InvalidDataException>(
                () => WarThunderBlkDocument.Parse("abc\0def"));
        }
        #endregion END Public
    }
}

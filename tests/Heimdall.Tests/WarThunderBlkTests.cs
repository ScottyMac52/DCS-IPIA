using System;
using System.IO;
using System.Linq;
using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    public sealed class WarThunderBlkTests
    {

        private static readonly string[] ExpectedItems =
        [
            "item1",
            "item2"
        ];

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

        [Fact]
        public void ImportAndNoOpExportPreserveEveryCharacter()
        {
            var document = WarThunderBlkDocument.Parse(Capture);
            Assert.Equal(Capture, document.SerializeUnchanged());
            Assert.Contains(document.Blocks, x => x.Path == "content/controls/hotkeys/fire");
            Assert.Contains(document.Scalars, x => x.Path == "content/controls/hotkeys/fire/activationType" && x.Value == "3");
        }

        [Fact]
        public void InventoryKeepsActivationCodesOpaque()
        {
            var controls = new WarThunderControls(Capture);

            Assert.Equal(3, Assert.Single(controls.ActivationCodes()));
            Assert.Single(controls.Axes);
            Assert.Single(controls.Joysticks);
        }

        [Fact]
        public void ReviewedReplacementOnlyChangesTargetValue()
        {
            var document = WarThunderBlkDocument.Parse(Capture);
            var output = document.ReplaceScalar("content/controls/hotkeys/fire/activationType", "4");
            Assert.Equal(Capture.Replace("activationType:i=3", "activationType:i=4", StringComparison.Ordinal), output);
            Assert.Equal(Capture, document.SerializeUnchanged());
        }

        [Fact]
        public void InvalidAndAmbiguousEditsAreBlocked()
        {
            var document = WarThunderBlkDocument.Parse(Capture);
            Assert.Throws<InvalidOperationException>(() => document.ReplaceScalar("missing", "2"));
            Assert.Throws<ArgumentException>(() => document.ReplaceScalar("content/controls/hotkeys/fire/activationType", "not-an-integer"));
            Assert.Throws<InvalidOperationException>(() => WarThunderBlkDocument.Parse("a:i=1;a:i=2;").ReplaceScalar("a", "3"));
        }

        [Fact]
        public void RejectsBrokenStructureAndBinaryContent()
        {
            Assert.Throws<InvalidDataException>(() => WarThunderBlkDocument.Parse("content{"));
            Assert.Throws<InvalidDataException>(() => WarThunderBlkDocument.Parse("}"));
            Assert.Throws<InvalidDataException>(
                () => WarThunderBlkDocument.Parse("abc\0def"));
        }
    }
}

using Heimdall.Application;
using Xunit;

namespace Heimdall.Tests
{
    public sealed class AssignmentInputLabelTests
    {
        [Theory]
        [InlineData("JOY_BTN1", "Button 1 - VIACOM TX1", "Button 1 — VIACOM TX1")]
        [InlineData("JOY_BTN7", "B07 | Hat press", "Button 7 — Hat press")]
        [InlineData("JOY_BTN6", "Button 6 — UNCAGE", "Button 6 — UNCAGE")]
        [InlineData("JOY_BTN2", "Flaps auto", "Button 2 — Flaps auto")]
        [InlineData("JOY_BTN2", "Button 2", "Button 2")]
        [InlineData("JOY_BTN2", "Button 20 - auxiliary", "Button 2 — Button 20 - auxiliary")]
        [InlineData("JOY_BTN2", null, "Button 2")]
        [InlineData("JOY_BTN2", "", "Button 2")]
        [InlineData("JOY_BTN2", " \t ", "Button 2")]
        [InlineData("JOY_BTN7", " b007\t|\tHat press ", "Button 7 — Hat press")]
        [InlineData("JOY_BTN7", "bUtToN\t007\u00a0:\u00a0Hat press", "Button 7 — Hat press")]
        [InlineData("JOY_BTN7", "B7", "Button 7")]
        [InlineData("JOY_BTN7", "B7 | ", "Button 7")]
        [InlineData("JOY_BTN7", "button 7", "Button 7")]
        [InlineData("JOY_BTN2147483647", "B2147483647 - Last", "Button 2147483647 — Last")]
        public void FormatsPhysicalButtonAndCaption(string key, string? caption, string expected)
        {
            Assert.Equal(expected, AssignmentInputLabel.ForPhysicalKey(key, caption));
        }

        /// <summary>Every supported punctuation separator behaves identically for both caption prefixes.</summary>
        [Theory]
        [InlineData("-")]
        [InlineData("–")]
        [InlineData("—")]
        [InlineData("|")]
        [InlineData(":")]
        [InlineData("·")]
        public void AcceptsCaptionSeparators(string separator)
        {
            foreach (var prefix in new[] { "Button 7", "B07" })
            {
                Assert.Equal("Button 7 — Hat", AssignmentInputLabel.ForPhysicalKey("JOY_BTN7", prefix + separator + "Hat"));
                Assert.Equal("Button 7 — Hat", AssignmentInputLabel.ForPhysicalKey("JOY_BTN7", prefix + " \t" + separator + "\t Hat"));
                Assert.Equal("Button 7", AssignmentInputLabel.ForPhysicalKey("JOY_BTN7", prefix + separator));
            }
        }

        /// <summary>Only a complete, matching numeric prefix may be removed from a hardware caption.</summary>
        [Theory]
        [InlineData("B08 | Other")]
        [InlineData("Button7 - Missing space")]
        [InlineData("Button")]
        [InlineData("Button - Missing number")]
        [InlineData("B")]
        [InlineData("B 7 - Space before number")]
        [InlineData("B7 Hat")]
        [InlineData("B7/Unsupported separator")]
        [InlineData("Button 7th")]
        [InlineData("B-7 | Negative")]
        [InlineData("B2147483648 - Overflow")]
        [InlineData("B٧ | Non-ASCII digit")]
        [InlineData("B７ | Fullwidth digit")]
        public void PreservesUnmatchedOrMalformedCaption(string caption)
        {
            Assert.Equal("Button 7 — " + caption, AssignmentInputLabel.ForPhysicalKey("JOY_BTN7", caption));
        }

        /// <summary>Axis, POV, unknown and invalid numbered keys retain their original identity.</summary>
        [Theory]
        [InlineData("JOY_RX", "Roll", "JOY_RX — Roll")]
        [InlineData("JOY_BTN_POV1_U", "Hat up", "JOY_BTN_POV1_U — Hat up")]
        [InlineData("JOY_BTN0", "Button 0 - Reserved", "JOY_BTN0 — Button 0 - Reserved")]
        [InlineData("JOY_BTN-1", null, "JOY_BTN-1")]
        [InlineData("JOY_BTN2147483648", null, "JOY_BTN2147483648")]
        [InlineData("JOY_BTN", null, "JOY_BTN")]
        [InlineData("Custom", "custom", "Custom")]
        [InlineData("JOY_RX", "Button 7 - Roll", "JOY_RX — Button 7 - Roll")]
        public void PreservesOtherInputIdentifiers(string key, string? caption, string expected)
        {
            Assert.Equal(expected, AssignmentInputLabel.ForPhysicalKey(key, caption));
        }

        /// <summary>Caption normalization does not rewrite the caller's physical identifier.</summary>
        [Theory]
        [InlineData("Button 07", "B07 | Hat", "Button 07 — B07 | Hat")]
        [InlineData("Button 0", "B0 | Reserved", "Button 0 — Reserved")]
        [InlineData("button 7", "B7 | Hat", "button 7 — B7 | Hat")]
        public void RetainsExactSuppliedIdentifier(string identifier, string caption, string expected)
        {
            Assert.Equal(expected, AssignmentInputLabel.WithCaption(identifier, caption));
        }
    }
}

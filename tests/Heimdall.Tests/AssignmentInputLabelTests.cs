using Heimdall.Application;

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
        public void FormatsPhysicalButtonAndCaption(string key, string? caption, string expected)
        {
            Assert.Equal(expected, AssignmentInputLabel.ForPhysicalKey(key, caption));
        }
    }
}

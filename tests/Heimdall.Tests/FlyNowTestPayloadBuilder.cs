using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>Supplies controllable native payloads for filesystem and SQLite acceptance tests.</summary>
    public sealed class FlyNowTestPayloadBuilder : IFlyNowPayloadBuilder
    {
        #region BEGIN Public
        /// <summary>Gets or sets the deployable files.</summary>
        public Dictionary<string, byte[]> Files { get; set; } = new()
        {
            ["Config/Input/Test/joystick/device.diff.lua"] = [1, 2, 3],
            ["Config/Input/UiLayer/modifiers.lua"] = [4, 5],
            ["KNEEBOARD/Test/page.png"] = [6, 7]
        };
        /// <inheritdoc/>
        public Task<FlyNowPayload> BuildAsync(Guid moduleId, CancellationToken ct = default) =>
            Task.FromResult(new FlyNowPayload("Test", Files));
        #endregion END Public
    }
}

namespace Heimdall.Application.Interfaces
{
    /// <summary>Builds verified deployment contents from saved database state.</summary>
    public interface IFlyNowPayloadBuilder
    {
        #region BEGIN Public
        /// <summary>Loads, freezes and validates the selected saved module.</summary>
        Task<FlyNowPayload> BuildAsync(Guid moduleId, CancellationToken ct = default);
        #endregion END Public
    }
}

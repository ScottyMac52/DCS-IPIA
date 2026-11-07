namespace Heimdall.Application.Interfaces
{
    /// <summary>Builds verified deployment contents from saved database state.</summary>
    public interface IDcsFlyNowPayloadBuilder
    {
        #region BEGIN Public
        /// <summary>Loads, freezes and validates the selected saved module.</summary>
        Task<DcsFlyNowPayload> BuildAsync(Guid moduleId, CancellationToken ct = default);
        #endregion END Public
    }
}

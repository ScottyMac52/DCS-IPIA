namespace Heimdall.Application.Interfaces
{
    /// <summary>Inspects the existing database without creating, migrating or repairing it.</summary>
    public interface ISupportDatabaseInspector
    {
        #region BEGIN Public
        /// <summary>Reads schema and integrity evidence.</summary>
        /// <param name="ct">Inspection cancellation.</param>
        /// <returns>Non-identifying database status.</returns>
        Task<SupportDatabaseStatus> InspectAsync(CancellationToken ct = default);
        #endregion END Public
    }
}


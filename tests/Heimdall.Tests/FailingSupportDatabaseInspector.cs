using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>Injects sensitive exception content to test diagnostic allow-list failures.</summary>
    /// <param name="failure">Failure that must never be serialized verbatim.</param>
    internal sealed class FailingSupportDatabaseInspector(Exception failure) : ISupportDatabaseInspector
    {
        #region BEGIN Public
        /// <inheritdoc/>
        public Task<SupportDatabaseStatus> InspectAsync(CancellationToken ct = default) =>
            Task.FromException<SupportDatabaseStatus>(failure);
        #endregion END Public
    }
}


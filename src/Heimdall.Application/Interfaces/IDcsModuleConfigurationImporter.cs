using Heimdall.Domain;

using Heimdall.Application;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Defines module configuration importer operations.</summary>
    public interface IDcsModuleConfigurationImporter
    {
        #region BEGIN Public
        /// <summary>Imports the requested configuration.</summary>
        /// <param name="request">The request.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        Task<DcsModuleImportResult> ImportAsync(DcsModuleImportRequest request, CancellationToken ct = default);
        #endregion END Public
    }
}

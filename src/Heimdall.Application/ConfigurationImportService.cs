using Heimdall.Domain;

using Heimdall.Application.Interfaces;

namespace Heimdall.Application
{
    /// <summary>Coordinates import and persistence of module configurations.</summary>
    /// <param name="importer">The importer.</param>
    /// <param name="repository">The repository.</param>
    public sealed class ConfigurationImportService(IModuleConfigurationImporter importer,
                                                   IModuleConfigurationRepository repository)
    {
        #region BEGIN Public
        /// <summary>Imports the requested configuration.</summary>
        /// <param name="request">The request.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        public async Task<ModuleImportResult> ImportAsync(ModuleImportRequest request, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var result = await importer.ImportAsync(request, ct);
            if (result.Succeeded)
                await repository.ReplaceAsync(result.Configuration!, ct);
            return result;
        }
        #endregion END Public
    }
}

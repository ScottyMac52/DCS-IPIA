using Heimdall.Application;

using Heimdall.Application.Interfaces;

using Heimdall.Domain;

namespace Heimdall.Tests.Fakes
{
    /// <summary>Represents stub importer.</summary>
    /// <param name="result">The result.</param>
    internal sealed class StubImporter(DcsModuleImportResult result) : IDcsModuleConfigurationImporter
    {
        #region BEGIN Public
        /// <summary>Gets the result.</summary>
        public DcsModuleImportResult Result { get; set; } = result;
        /// <summary>Imports the requested configuration.</summary>
        /// <param name="request">The request.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        public Task<DcsModuleImportResult> ImportAsync(DcsModuleImportRequest request,
                                                    CancellationToken ct = default) => Task.FromResult(Result);
        #endregion END Public
    }
}

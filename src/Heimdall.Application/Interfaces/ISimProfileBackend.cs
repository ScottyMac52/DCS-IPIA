using Heimdall.Domain.MultiSim;
using Heimdall.Application.Validation;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Translates explicit native sources into portable profiles and reviewable output without deploying files.</summary>
    public interface ISimProfileBackend
    {
        #region BEGIN Public
        /// <summary>Gets the owning backend identifier.</summary>
        string SimId { get; }
        /// <summary>Gets the operations and binding semantics supported by this backend version.</summary>
        SimBackendCapabilities Capabilities { get; }
        /// <summary>Gets the packaged versioned file-backed rules for this adapter.</summary>
        ProfileValidationDefinition ValidationDefinition { get; }
        /// <summary>Checks a loaded configuration against explicit file-backed rules and optional known-good native file hashes.</summary>
        ProfileValidationResult Validate(ProfileDocument profile, ProfileValidationDefinition definition, CancellationToken cancellationToken = default);
        /// <summary>Imports captured native files without consulting active game folders.</summary>
        /// <param name="contextId">Explicit module or vehicle context.</param>
        /// <param name="sources">Relative native paths and their original text.</param>
        /// <param name="cancellationToken">Cancellation before publication of the result.</param>
        /// <returns>An independent versioned portable document.</returns>
        ProfileDocument Import(string contextId, IReadOnlyDictionary<string, string> sources, CancellationToken cancellationToken = default);
        /// <summary>Plans native output or returns blocking diagnostics; never writes to the game or database.</summary>
        /// <param name="profile">Portable document to validate and export.</param>
        /// <param name="cancellationToken">Cancellation before publication of the result.</param>
        /// <returns>Complete output, or no files when any conversion is blocked.</returns>
        SimProfileExportPlan Export(ProfileDocument profile, CancellationToken cancellationToken = default);
        /// <summary>Gets commands scoped to the profile's backend, module and UI context.</summary>
        /// <param name="profile">Document containing pinned catalog evidence.</param>
        /// <returns>The pinned command catalog.</returns>
        ICommandCatalog GetCatalog(ProfileDocument profile);
        /// <summary>Matches an explicitly captured instance without falling back to product or catalog names.</summary>
        /// <param name="identity">Requested physical instance.</param>
        /// <param name="connected">Observed connected instances; this is identity evidence, not entitlement evidence.</param>
        /// <returns>Resolved, disconnected, unresolved or ambiguous status.</returns>
        SimDeviceResolution ResolveDevice(PhysicalDeviceIdentity identity, IReadOnlyList<PhysicalDeviceIdentity> connected);
        #endregion END Public
    }
}

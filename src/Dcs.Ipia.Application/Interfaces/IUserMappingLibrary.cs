namespace Dcs.Ipia.Application.Interfaces
{
    /// <summary>Stores immutable user-authored mappings independently from bundled assets and modules.</summary>
    public interface IUserMappingLibrary
    {
        #region BEGIN Public
        /// <summary>Reads verified revisions in stable revision order.</summary>
        /// <returns>Self-contained mapping revisions.</returns>
        Task<IReadOnlyList<UserMappingRevision>> ListAsync();
        /// <summary>Publishes a verified revision without replacing any existing revision.</summary>
        /// <param name="revision">Reviewed, portable source and derived assets.</param>
        /// <returns>Completion of immutable publication.</returns>
        Task SaveAsync(UserMappingRevision revision);
        #endregion END Public
    }
}

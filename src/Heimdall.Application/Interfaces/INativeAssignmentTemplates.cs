using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Optional native adapter support for recovering authoring metadata without importing active assignments.</summary>
    public interface INativeAssignmentTemplates
    {
        #region BEGIN Public
        /// <summary>Merges captured native templates while retaining the current profile's active bindings and identity.</summary>
        ProfileDocument ImportAssignmentTemplates(ProfileDocument profile, IReadOnlyDictionary<string, string> sources);
        #endregion END Public
    }
}

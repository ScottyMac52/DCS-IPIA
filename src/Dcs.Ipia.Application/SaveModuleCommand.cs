using Dcs.Ipia.Domain;
namespace Dcs.Ipia.Application
{
    /// <summary>Represents save module command.</summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="DcsId">The DCS identifier.</param>
    /// <param name="DisplayName">The display name.</param>
    public sealed record SaveModuleCommand(Guid? Id, string DcsId, string DisplayName);
}

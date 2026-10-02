using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application
{
    /// <summary>Editable metadata for a combined device page or a two-column summary page.</summary>
    /// <param name="Kind">Supported device or summary layout.</param>
    /// <param name="ProfileId">Device profile for a combined page; null for summaries.</param>
    /// <param name="File">Safe output page name without the PNG extension.</param>
    /// <param name="Title">Page heading.</param>
    /// <param name="Subtitle">Page subtitle.</param>
    /// <param name="Order">Page sequence order.</param>
    /// <param name="Cards">Summary cards in display order.</param>
    public sealed record ModuleKneeboardPageSettings(KneeboardPageKind Kind, Guid? ProfileId, string File,
        string Title, string Subtitle, int Order, IReadOnlyList<ModuleSummaryCard> Cards);
}

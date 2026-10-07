using System.Windows.Controls;
using Dcs.Ipia.Application;

namespace DcsIpia.Presentation
{
    /// <summary>Summary-card drag payload scoped to its originating editor list and exact row.</summary>
    /// <param name="Source">List that initiated the drag.</param>
    /// <param name="Card">Exact card instance being moved.</param>
    public sealed record SummaryCardDrag(ListBox Source, ModuleSummaryCard Card);
}

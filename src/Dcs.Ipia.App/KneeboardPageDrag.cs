using System.Windows.Controls;
using Dcs.Ipia.Domain;

namespace DcsIpia.Presentation
{
    /// <summary>A page-row drag restricted to its originating table and exact draft page.</summary>
    /// <param name="Source">Table that initiated the drag.</param>
    /// <param name="Page">Exact current page being moved.</param>
    public sealed record KneeboardPageDrag(DataGrid Source, KneeboardPage Page);
}

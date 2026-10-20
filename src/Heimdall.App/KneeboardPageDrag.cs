using System.Windows.Controls;
using Heimdall.Domain;

namespace Heimdall.App
{
    /// <summary>A page-row drag restricted to its originating table and exact draft page.</summary>
    /// <param name="Source">Table that initiated the drag.</param>
    /// <param name="Page">Exact current page being moved.</param>
    public sealed record KneeboardPageDrag(DataGrid Source, KneeboardPage Page);
}

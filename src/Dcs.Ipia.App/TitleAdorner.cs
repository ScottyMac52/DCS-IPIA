using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DcsIpia.Presentation
{
    /// <summary>Displays a dragged command or page title near the screen cursor.</summary>
    public sealed class TitleAdorner : Adorner
    {
        private readonly string _title;
        private Point _pos;

        #region BEGIN Public
        public TitleAdorner(UIElement adorned, string title) : base(adorned) => _title = title;

        /// <summary>Moves the drag preview and schedules a redraw.</summary>
        public void Update(Point pos)
        {
            _pos = pos;
            InvalidateVisual();
        }
        #endregion END Public

        #region BEGIN private helpers
        protected override void OnRender(DrawingContext drawingContext)
        {
            var text = new FormattedText(
                _title,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                12,
                Brushes.Black,
                1);
            var rect = new Rect(_pos.X + 4, _pos.Y + 4, text.Width + 12, text.Height + 8);
            drawingContext.DrawRoundedRectangle(Brushes.White, new Pen(Brushes.SteelBlue, 1), rect, 3, 3);
            drawingContext.DrawText(text, new Point(rect.X + 6, rect.Y + 4));
        }
        #endregion END private helpers
    }
}

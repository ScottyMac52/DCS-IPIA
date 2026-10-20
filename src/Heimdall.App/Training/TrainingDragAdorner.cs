using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Heimdall.App
{
    /// <summary>Illustrates a palette command travelling to its real drop target.</summary>
    public sealed class TrainingDragAdorner(UIElement element, Point start, Point end) : Adorner(element)
    {
        #region BEGIN Public
        /// <summary>Gets or sets the fraction of the illustrated gesture that has completed.</summary>
        public double Progress { get; set; }
        #endregion END Public
        #region BEGIN private helpers
        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            var pen = new Pen((Brush)FindResource("AccentTextBrush"), 4) { DashStyle = DashStyles.Dash };
            drawingContext.DrawLine(new Pen((Brush)FindResource("WindowBackgroundBrush"), 7), start, end);
            drawingContext.DrawLine(pen, start, end);
            var point = new Point(start.X + (end.X - start.X) * Progress, start.Y + (end.Y - start.Y) * Progress);
            drawingContext.DrawEllipse((Brush)FindResource("AccentTextBrush"), new Pen((Brush)FindResource("WindowBackgroundBrush"), 3), point, 13, 13);
        }
        #endregion END private helpers
    }
}

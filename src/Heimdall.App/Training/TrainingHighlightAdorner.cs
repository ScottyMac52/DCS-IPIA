using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Heimdall.App
{
    /// <summary>Draws a noninteractive, theme-aware target outline that follows WPF layout.</summary>
    public sealed class TrainingHighlightAdorner : Adorner
    {
        /// <summary>Attaches the outline to the actual narrated control.</summary>
        public TrainingHighlightAdorner(UIElement target) : base(target) => IsHitTestVisible = false;
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        /// <summary>Renders contrasting outlines using the target's current layout size.</summary>
        protected override void OnRender(DrawingContext drawingContext)
        {
            var rect = new Rect(AdornedElement.RenderSize);
            drawingContext.DrawRectangle(null, new Pen((Brush)FindResource("WindowBackgroundBrush"), 7), rect);
            drawingContext.DrawRectangle(null, new Pen((Brush)FindResource("AccentTextBrush"), 3), rect);
        }
        #endregion END private helpers
    }
}

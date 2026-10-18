using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DcsIpia.Presentation
{
    /// <summary>Draws a colored outline with white and black edges over light, dark or photographic artwork.</summary>
    public sealed class ContrastOutlineBorder : Border
    {
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        /// <summary>Paints solid contrasting edges while keeping inactive transparent outlines invisible.</summary>
        protected override void OnRender(DrawingContext dc)
        {
            var bounds = new Rect(RenderSize);
            dc.DrawRectangle(Background, null, bounds);
            if (BorderBrush is null || BorderBrush.Opacity == 0 || BorderBrush is SolidColorBrush { Color: { A: 0 } }) return;
            var width = BorderThickness.Left;
            if (width <= 0 || bounds.Width <= 0 || bounds.Height <= 0) return;
            // Keep a clear center even on small callouts; never expand their layout bounds.
            var scale = Math.Min(1, Math.Min(bounds.Width, bounds.Height) / (3 * (width + 4)));
            var outerWidth = (width + 4) * scale;
            bounds.Inflate(-outerWidth / 2, -outerWidth / 2);
            dc.DrawRectangle(null, new Pen((Brush)FindResource("InputOutlineDarkBrush"), outerWidth), bounds);
            dc.DrawRectangle(null, new Pen((Brush)FindResource("InputOutlineLightBrush"), (width + 2) * scale), bounds);
            dc.DrawRectangle(null, new Pen(BorderBrush, width * scale), bounds);
        }
        #endregion END private helpers
    }
}

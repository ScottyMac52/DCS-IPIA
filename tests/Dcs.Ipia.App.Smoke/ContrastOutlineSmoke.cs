using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Checks actual outline pixels on black and white artwork for every live input-state color.</summary>
    public static class ContrastOutlineSmoke
    {
        private static readonly string[] Colors = [KneeboardLiveProjection.BaseHighlightColor, "#dc2626", "#ea580c", "#c026d3",
            "#16a34a", "#9333ea", "#0891b2", "#7c3aed", "#be185d", "#0f766e"];
        #region BEGIN Public
        /// <summary>Verifies color, white and black rings, unobscured centers, invisible inactive outlines, and matching swatches.</summary>
        public static void Verify()
        {
            var canvas = new Canvas { Width = 80, Height = 60 };
            var outline = new ContrastOutlineBorder { Width = 40, Height = 24, BorderThickness = new Thickness(3), Background = Brushes.Transparent };
            var swatch = new ContrastOutlineBorder { Width = 15, Height = 15, BorderThickness = new Thickness(1) };
            Canvas.SetLeft(outline, 10); Canvas.SetTop(outline, 10);
            Canvas.SetLeft(swatch, 60); Canvas.SetTop(swatch, 10);
            canvas.Children.Add(outline); canvas.Children.Add(swatch);
            var window = new Window { Content = canvas, SizeToContent = SizeToContent.WidthAndHeight };
            try
            {
                window.Show();
                foreach (var background in new[] { System.Windows.Media.Colors.White, System.Windows.Media.Colors.Black })
                {
                    canvas.Background = new SolidColorBrush(background);
                    foreach (var hex in Colors)
                    {
                        var color = (Color)ColorConverter.ConvertFromString(hex);
                        outline.BorderBrush = swatch.BorderBrush = swatch.Background = new SolidColorBrush(color);
                        ThemeControlSmoke.Pump(window);
                        ThemeControlSmoke.SaveImage(window, "input-outline-" + hex + "-" + background);
                        var bitmap = Render(canvas);
                        Pixel(bitmap, 30, 10, System.Windows.Media.Colors.Black);
                        Pixel(bitmap, 30, 11, System.Windows.Media.Colors.White);
                        Pixel(bitmap, 30, 13, color);
                        Pixel(bitmap, 30, 15, System.Windows.Media.Colors.White);
                        Pixel(bitmap, 30, 16, System.Windows.Media.Colors.Black);
                        Pixel(bitmap, 30, 22, background);
                        Pixel(bitmap, 67, 10, System.Windows.Media.Colors.Black);
                        Pixel(bitmap, 67, 11, System.Windows.Media.Colors.White);
                        Pixel(bitmap, 67, 17, color);
                    }
                    ThemeControlSmoke.SaveImage(window, "input-outline-contrast-" + background);
                    outline.BorderBrush = Brushes.Transparent;
                    ThemeControlSmoke.Pump(window);
                    var inactive = Render(canvas);
                    for (var y = 10; y < 34; y++)
                        for (var x = 10; x < 50; x++) Pixel(inactive, x, y, background);
                }
            }
            finally { window.Close(); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static BitmapSource Render(FrameworkElement control)
        {
            // Render the actual window, then crop the control. Rendering an attached child directly
            // retains its visual offset, so logical Canvas coordinates do not address the expected pixels.
            var root = (FrameworkElement)PresentationSource.FromVisual(control)!.RootVisual;
            var image = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight),
                96, 96, PixelFormats.Pbgra32);
            image.Render(root);
            var bounds = control.TransformToAncestor(root).TransformBounds(new Rect(control.RenderSize));
            return new CroppedBitmap(image, new Int32Rect((int)Math.Round(bounds.X), (int)Math.Round(bounds.Y),
                (int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height)));
        }
        private static void Pixel(BitmapSource bitmap, int x, int y, Color expected)
        {
            var pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
            if (Math.Abs(pixel[0] - expected.B) > 2 || Math.Abs(pixel[1] - expected.G) > 2 || Math.Abs(pixel[2] - expected.R) > 2 || pixel[3] != 255)
            {
                var column = new List<string>();
                for (var row = 8; row < 35; row++)
                {
                    var sample = new byte[4];
                    bitmap.CopyPixels(new Int32Rect(x, row, 1, 1), sample, 4, 0);
                    column.Add($"{row}:{sample[2]:X2}{sample[1]:X2}{sample[0]:X2}");
                }
                throw new InvalidOperationException($"Contrast outline pixel ({x},{y}) was {pixel[2]:X2}{pixel[1]:X2}{pixel[0]:X2}, expected {expected}. Column: " + string.Join(" ", column));
            }
        }
        #endregion END private helpers
    }
}

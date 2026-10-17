using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;

namespace DcsIpia.Presentation
{
    /// <summary>Fixed, bundled Free Edition artwork; source paths and user preferences are never overwritten.</summary>
    internal static class FreeEditionAppearance
    {
        private static readonly Lazy<BitmapSource> Background = new(() =>
        {
            var image = new BitmapImage(new Uri("pack://application:,,,/Dcs.Ipia.App;component/Branding/FreeEdition/carrier-tomcat.jpg", UriKind.Absolute));
            image.Freeze(); return image;
        });
        #region BEGIN Public
        /// <summary>Gets the immutable Free background preset; additional bundled artwork can be added later.</summary>
        internal static AppearanceSettings Settings { get; } = new(Enabled: true, Opacity: 0.2, Tint: 0.1, PanelOpacity: 0.35, ControlOpacity: 0.65);
        /// <summary>Gets the embedded photograph independently of external files.</summary>
        internal static BitmapSource Image => Background.Value;
        #endregion END Public
    }
}

using System.Windows.Media.Imaging;

namespace DcsIpia.Presentation
{
    /// <summary>Provides immutable, embedded Flight Patch identity assets independently of the UI palette.</summary>
    public static class ApplicationBranding
    {
        #region BEGIN Public
        /// <summary>Multi-resolution Windows icon with a simplified mark for small title bars.</summary>
        public static BitmapFrame Icon { get; } = Load("flight-patch.ico");
        /// <summary>Full winged-controller badge for application headers and About.</summary>
        public static BitmapFrame Badge { get; } = Load("flight-patch.png");
        /// <summary>Landscape artwork for About and offline workflow help.</summary>
        public static BitmapFrame Banner { get; } = Load("flight-patch-banner.png");
        #endregion END Public
        #region BEGIN private helpers
        private static BitmapFrame Load(string filename)
        {
            var frame = BitmapFrame.Create(new Uri("pack://application:,,,/Dcs.Ipia.App;component/Branding/" + filename),
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            frame.Freeze();
            return frame;
        }
        #endregion END private helpers
    }
}

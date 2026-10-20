using System.Windows.Media.Imaging;

namespace DcsIpia.Presentation
{
    /// <summary>Provides immutable, embedded HEIMDALL identity assets independently of the UI palette.</summary>
    public static class ApplicationBranding
    {
        #region BEGIN Public
        /// <summary>Gets the public product name.</summary>
        public const string Name = "HEIMDALL";
        /// <summary>Gets the product descriptor.</summary>
        public const string Descriptor = "Simulation Control Manager";
        /// <summary>Multi-resolution Windows icon with a simplified mark for small title bars.</summary>
        public static BitmapFrame Icon { get; } = Load("heimdall.ico");
        /// <summary>Square tactical display icon for application headers and About.</summary>
        public static BitmapFrame Badge { get; } = Load("heimdall.png");
        /// <summary>Landscape artwork for About and offline workflow help.</summary>
        public static BitmapFrame Banner { get; } = Load("heimdall-banner.png");
        /// <summary>Large product lockup containing the mark, name and descriptor.</summary>
        public static BitmapFrame Logo { get; } = Load("heimdall-logo.png");
        /// <summary>Monochrome application icon for neutral backgrounds.</summary>
        public static BitmapFrame Monochrome { get; } = Load("heimdall-monochrome.png");
        #endregion END Public
        #region BEGIN private helpers
        private static BitmapFrame Load(string filename)
        {
            var frame = BitmapFrame.Create(new Uri("pack://application:,,,/Heimdall;component/Branding/" + filename),
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            frame.Freeze();
            return frame;
        }
        #endregion END private helpers
    }
}

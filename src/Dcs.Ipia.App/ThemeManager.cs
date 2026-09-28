using System.Windows;

namespace DcsIpia.Presentation
{
    /// <summary>Switches presentation palettes; persistence belongs to the settings service.</summary>
    internal static class ThemeManager
    {
        #region BEGIN Public
        /// <summary>Applies the selected appearance palette immediately.</summary>
        /// <param name="theme">Light or Dark.</param>
        internal static void Apply(string theme)
        {
            if (theme is not("Light" or "Dark"))
                throw new ArgumentException("Unknown appearance theme.", nameof(theme));
            var palettes = Application.Current.Resources.MergedDictionaries;
            for (var i = 0; i < palettes.Count; i++)
            {
                var source = palettes[i].Source?.OriginalString;
                if (source is null || (!source.EndsWith("/Light.xaml", StringComparison.OrdinalIgnoreCase) &&
                                       !source.EndsWith("/Dark.xaml", StringComparison.OrdinalIgnoreCase)))
                    continue;
                palettes[i] = new ResourceDictionary {
                    Source =
                        new Uri($"pack://application:,,,/Dcs.Ipia.App;component/Themes/{theme}.xaml", UriKind.Absolute)
                };
                return;
            }
            throw new InvalidOperationException("The application theme dictionary is missing.");
        }
        #endregion END Public
    }
}

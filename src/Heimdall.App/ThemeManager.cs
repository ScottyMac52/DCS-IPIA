using System.Windows;
using Heimdall.Application;

namespace Heimdall.App
{
    /// <summary>Switches presentation palettes; persistence belongs to the settings service.</summary>
    internal static class ThemeManager
    {
        #region BEGIN Public
        /// <summary>Applies the selected appearance palette immediately.</summary>
        /// <param name="theme">A built-in AppearanceTheme name.</param>
        internal static void Apply(string theme)
        {
            if (!Enum.TryParse<AppearanceTheme>(theme, out var selected) || !Enum.IsDefined(selected))
                throw new ArgumentException("Unknown appearance theme.", nameof(theme));
            selected = EditionAccess.Policy.EffectiveTheme(selected);
            var palettes = System.Windows.Application.Current.Resources.MergedDictionaries;
            for (var i = 0; i < palettes.Count; i++)
            {
                var source = palettes[i].Source?.OriginalString;
                if (source is null || !Enum.GetNames<AppearanceTheme>().Any(name =>
                    source.EndsWith($"/{name}.xaml", StringComparison.OrdinalIgnoreCase)))
                    continue;
                palettes[i] = new ResourceDictionary {
                    Source =
                        new Uri($"pack://application:,,,/Heimdall;component/Themes/{selected}.xaml", UriKind.Absolute)
                };
                AppearanceManager.Reapply();
                foreach (var window in System.Windows.Application.Current.Windows.OfType<Window>())
                    WindowThemeChrome.Apply(window);
                return;
            }
            throw new InvalidOperationException("The application theme dictionary is missing.");
        }
        #endregion END Public
    }
}

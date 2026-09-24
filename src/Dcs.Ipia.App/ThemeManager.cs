using System.IO;
using System.Windows;

namespace DcsIpia.Presentation;

/// <summary>Switches presentation palettes and remembers the local appearance preference.</summary>
internal static class ThemeManager {
 #region BEGIN Public
 /// <summary>Reads the saved appearance preference, defaulting to Light.</summary>
 /// <returns>The selected palette name.</returns>
 internal static string LoadPreference() {
  try {
   var value=File.ReadAllText(PreferencePath).Trim();
   return value.Equals("Dark",StringComparison.OrdinalIgnoreCase)?"Dark":"Light";
  }
  catch(IOException) {return "Light";}
  catch(UnauthorizedAccessException) {return "Light";}
 }
 /// <summary>Applies the selected palette immediately and saves the preference when possible.</summary>
 /// <param name="theme">Light or Dark.</param>
 /// <returns>Whether the preference was saved for a future launch.</returns>
 internal static bool Apply(string theme) {
  if(theme is not ("Light" or "Dark")) throw new ArgumentException("Unknown appearance theme.",nameof(theme));
  var palettes=Application.Current.Resources.MergedDictionaries;
  for(var i=0;i<palettes.Count;i++) {
   var source=palettes[i].Source?.OriginalString;
   if(source is null || (!source.EndsWith("/Light.xaml",StringComparison.OrdinalIgnoreCase) && !source.EndsWith("/Dark.xaml",StringComparison.OrdinalIgnoreCase))) continue;
   palettes[i]=new ResourceDictionary {
    Source=new Uri($"pack://application:,,,/Dcs.Ipia.App;component/Themes/{theme}.xaml",UriKind.Absolute)
   };
   return SavePreference(theme);
  }
  throw new InvalidOperationException("The application theme dictionary is missing.");
 }
 #endregion END Public
 #region BEGIN private helpers
 private static string PreferencePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DCS-IPIA","theme.txt");
 private static bool SavePreference(string theme) {
  try {
   Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
   File.WriteAllText(PreferencePath,theme);
   return true;
  }
  catch(IOException) {return false;}
  catch(UnauthorizedAccessException) {return false;}
 }
 #endregion END private helpers
}

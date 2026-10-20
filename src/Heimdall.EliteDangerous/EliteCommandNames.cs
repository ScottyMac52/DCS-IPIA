using System.Text;

namespace Heimdall.EliteDangerous
{
    /// <summary>Readable labels over stable native IDs; localization and semantic conversion remain separate.</summary>
    internal static class EliteCommandNames
    {
        #region BEGIN Public
        /// <summary>Formats native command names without changing command identities.</summary>
        public static string Display(string name)
        {
            if (name == "UseBoostJuice") return "Boost";
            var label = new StringBuilder();
            for (var i = 0; i < name.Length; i++)
            {
                if (name[i] == '_') { label.Append(' '); continue; }
                if (Boundary(name, i)) label.Append(' ');
                label.Append(name[i]);
            }
            return label.ToString();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static bool Boundary(string name, int index) => index > 0 && char.IsUpper(name[index]) &&
            (char.IsLower(name[index - 1]) || char.IsDigit(name[index - 1]) ||
             char.IsUpper(name[index - 1]) && index + 1 < name.Length && char.IsLower(name[index + 1]));
        #endregion END private helpers
    }
}

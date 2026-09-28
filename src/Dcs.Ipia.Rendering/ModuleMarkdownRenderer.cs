using Dcs.Ipia.Application;
namespace Dcs.Ipia.Rendering
{
    /// <summary>Renders module documentation as Markdown.</summary>
    public static class ModuleMarkdownRenderer
    {
        #region BEGIN Public
        /// <summary>Renders an ordered Markdown table.</summary>
        /// <param name="values">The values.</param>
        /// <returns>The result of the operation.</returns>
        public static string Render(IEnumerable<ModuleDto> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            return "# Modules\n\n| DCS ID | Name |\n| --- | --- |\n" +
                   string.Join('\n', values.OrderBy(x => x.DcsId, StringComparer.Ordinal)
                                         .Select(x => $"| {Escape(x.DcsId)} | {Escape(x.DisplayName)} |")) +
                   "\n";
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
        #endregion END private helpers
    }
}

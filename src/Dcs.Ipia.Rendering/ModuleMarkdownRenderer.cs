using Dcs.Ipia.Application;
namespace Dcs.Ipia.Rendering;
public sealed class ModuleMarkdownRenderer { public string Render(IEnumerable<ModuleDto> values){ArgumentNullException.ThrowIfNull(values);return "# Modules\n\n| DCS ID | Name |\n| --- | --- |\n"+string.Join('\n',values.OrderBy(x=>x.DcsId,StringComparer.Ordinal).Select(x=>$"| {Escape(x.DcsId)} | {Escape(x.DisplayName)} |"))+"\n";} private static string Escape(string value)=>value.Replace("|","\\|",StringComparison.Ordinal);}

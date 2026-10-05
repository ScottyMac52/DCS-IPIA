namespace Dcs.Ipia.Application
{
    /// <summary>Windows HTML clipboard data and its plain-text fallback.</summary>
    /// <param name="Html">CF_HTML data with UTF-8 byte offsets.</param>
    /// <param name="Text">Tab-separated headers and event rows.</param>
    public sealed record InputTestLogClipboard(string Html, string Text);
}

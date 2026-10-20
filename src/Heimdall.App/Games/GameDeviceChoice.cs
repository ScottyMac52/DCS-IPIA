namespace Heimdall.App.Games
{
    /// <summary>A captured native reference displayed independently of a Windows device identity.</summary>
    /// <param name="Id">Logical native reference.</param>
    /// <param name="Name">Readable native name and optional reviewed hardware label.</param>
    public sealed record GameDeviceChoice(string Id, string Name);
}

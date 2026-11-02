namespace Heimdall.Application
{
    /// <summary>Counts driver-declared objects; these are neither physical actuator counts nor verified live inputs.</summary>
    public sealed record ReportedInputCounts(int Axes, int Buttons, int Hats, int Other);
}

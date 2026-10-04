namespace Dcs.Ipia.Application
{
    /// <summary>One physical transition and all generated commands that it resolves to.</summary>
    /// <param name="PhysicalGuid">Captured test data.</param>
    /// <param name="InputKey">Captured test data.</param>
    /// <param name="Value">Captured test data.</param>
    /// <param name="Transition">Captured test data.</param>
    /// <param name="Matches">Captured test data.</param>
    public sealed record InputTestResult(Guid PhysicalGuid, string InputKey, double Value, string Transition, IReadOnlyList<InputTestBinding> Matches);
}

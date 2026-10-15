namespace Dcs.Ipia.Application
{
    /// <summary>One approved action and the explanation retained after it completes.</summary>
    public sealed record TrainingStep(string Id, string Action, string Title, string Explanation, string Target, bool Checkpoint);
}

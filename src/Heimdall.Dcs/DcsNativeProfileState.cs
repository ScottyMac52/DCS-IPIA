using Heimdall.Domain.MultiSim;

namespace Heimdall.Dcs
{
    /// <summary>Version-one DCS evidence preserves source text and optionally the complete existing workspace.</summary>
    /// <param name="ContextId">Owning DCS module.</param>
    /// <param name="ProfileId">Stable portable document identity.</param>
    /// <param name="Sources">Original native payload, including keyboard and modifier files.</param>
    /// <param name="Baseline">Independent portable projection used to validate instance and scope ownership.</param>
    /// <param name="Commands">Pinned, explicitly scoped catalog.</param>
    /// <param name="Workspace">Verified legacy editor snapshot with artwork, labels, provenance and recovery state.</param>
    internal sealed record DcsNativeProfileState(string ContextId, string ProfileId, Dictionary<string, string> Sources,
        List<LogicalDevice> Baseline, List<SimCommand> Commands, ImmutableSnapshot? Workspace = null);
}

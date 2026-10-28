using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Games
{
    /// <summary>A supported input filter and its adapter-owned label.</summary>
    public sealed record GameCommandTypeOption(string Label, CommandType? Type);
}

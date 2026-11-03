using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>One verified physical key in a saved device's native local index space.</summary>
    public sealed record WarThunderPhysicalInput(string Key, CommandType Type, int Index);
}

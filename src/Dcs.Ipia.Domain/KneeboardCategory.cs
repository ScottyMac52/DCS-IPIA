namespace Dcs.Ipia.Domain
{
    /// <summary>Represents kneeboard category.</summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="StableKey">The stable key.</param>
    /// <param name="Label">The label.</param>
    /// <param name="Order">The order.</param>
    public sealed record KneeboardCategory(Guid Id, string StableKey, string Label, int Order);
}

namespace Heimdall.Application
{
    /// <summary>One parsed generated binding and its physical scope.</summary>
    /// <param name="PhysicalGuid">Captured test data.</param>
    /// <param name="Layer">Captured test data.</param>
    /// <param name="Binding">Captured test data.</param>
    public sealed record InputTestBinding(Guid PhysicalGuid, AssignmentLayer Layer, Heimdall.Domain.DcsBinding Binding)
    {
        #region BEGIN Public
        /// <summary>Gets a sorted, exact generated chord.</summary>
        public string Chord => string.Join("+", Binding.Reformers.Order(StringComparer.Ordinal));
        #endregion END Public
    }
}

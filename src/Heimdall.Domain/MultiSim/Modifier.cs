namespace Heimdall.Domain.MultiSim
{
    /// <summary>A modifier that must be active for a binding to fire.</summary>
    public sealed class Modifier
    {
        #region BEGIN Public

        /// <summary>Gets or sets the source of the modifier.</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>Gets or sets an optional value or threshold.</summary>
        public string? Value { get; set; }

        #endregion END Public
    }
}

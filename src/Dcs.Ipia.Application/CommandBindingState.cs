namespace Dcs.Ipia.Application
{
    /// <summary>How a command catalog is filtered by assignments in the selected module.</summary>
    public enum CommandBindingState
    {
        /// <summary>Show every command.</summary>
        All,
        /// <summary>Assigned on the selected device and modifier layer.</summary>
        Bound,
        /// <summary>Not assigned on the selected device and modifier layer.</summary>
        Unbound,
        /// <summary>Assigned on every eligible device in this scope, on any modifier layer.</summary>
        BoundByAll,
        /// <summary>Not assigned on any eligible device in this scope.</summary>
        UnboundByAll
    }
}

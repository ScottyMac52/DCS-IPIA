using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>Kinds of context notification seen by one consumer, from cheapest to most expensive to apply.</summary>
    public enum WorkspaceChangeKind
    {
        /// <summary>Nothing this consumer has not already observed.</summary>
        None,
        /// <summary>Only the shared selection moved; native content, identity and gates are unchanged.</summary>
        Selection,
        /// <summary>Gates, name overlay, dirty, operation or activity changed; native content and identity are unchanged.</summary>
        Presentation,
        /// <summary>Native content or mounted identity changed; native views must reload.</summary>
        Content
    }
}

using Dcs.Ipia.Application;

namespace DcsIpia.Presentation
{
    /// <summary>Editable source decision for one reviewed shared default change.</summary>
    /// <param name="change">Compared baseline and module source.</param>
    public sealed class SharedUiLayerAdoptionChoice(SharedUiLayerAdoptionChange change)
    {
        #region BEGIN Public
        /// <summary>Gets the exact source path.</summary>
        public string Path { get; } = change.Path;
        /// <summary>Gets Added, Updated or Removed.</summary>
        public string Change { get; } = change.Change;
        /// <summary>Gets whether taking defaults would replace a module source difference.</summary>
        public bool Conflict { get; } = change.Conflict;
        /// <summary>Gets or sets the explicit source adoption decision; conflicts begin unchecked.</summary>
        public bool TakeDefault { get; set; } = !change.Conflict;
        #endregion END Public
    }
}

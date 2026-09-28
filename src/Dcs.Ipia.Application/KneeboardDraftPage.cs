namespace Dcs.Ipia.Application
{
    /// <summary>One live kneeboard view of a device in an explicit scope and modifier layer.</summary>
    /// <param name="Scope">Module controls or module UI Layer.</param>
    /// <param name="ModifierChord">Modifier names, or empty for Base.</param>
    /// <param name="Captions">Current displayed callout labels by authored graphic control.</param>
    public sealed record KneeboardDraftPage(AssignmentLayer Scope, string ModifierChord,
                                            IReadOnlyDictionary<string, string> Captions)
    {
        #region BEGIN Public
        /// <summary>Gets the layer name displayed above the complete device graphic.</summary>
        public string Title =>
            (Scope == AssignmentLayer.Module ? "Module" : "UI Layer") + " · " +
            (ModifierChord.Length == 0 ? "Base" : ModifierChord.Replace("+", " + ", StringComparison.Ordinal));
        #endregion END Public
    }
}

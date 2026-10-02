namespace Dcs.Ipia.Application
{
    /// <summary>One authored card in the fixed two-column summary layout.</summary>
    /// <param name="Key">Short card heading.</param>
    /// <param name="Text">Card description.</param>
    /// <param name="Accent">Named shared palette color.</param>
    public sealed record ModuleSummaryCard(string Key, string Text, string Accent)
    {
        #region BEGIN Public
        /// <summary>Gets the card's label for page authoring.</summary>
        public string Display => Key + " · " + Text;
        #endregion END Public
    }
}

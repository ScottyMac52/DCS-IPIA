namespace Heimdall.WarThunder
{
    /// <summary>A command block occurrence retains every direct field and any nested native content.</summary>
    public sealed record WarThunderBinding(string Id, string Section, string Command,
        IReadOnlyList<WarThunderBlkScalar> Fields, bool HasChildren)
    {
        #region BEGIN Public
        /// <summary>Gets whether an explicit unassigned block can host a new native input.</summary>
        public bool Empty => Fields.Count == 0 && !HasChildren;
        #endregion END Public
    }
}

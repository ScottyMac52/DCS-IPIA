namespace Dcs.Ipia.Application
{
    /// <summary>A reviewed physical unit and its pinned reusable control definition.</summary>
    /// <param name="Instance">User-owned physical identity and label.</param>
    /// <param name="Definition">Reviewed native capabilities.</param>
    public sealed record InventoryHardwareChoice(HardwareInventoryInstance Instance, HardwareDefinition Definition)
    {
        #region BEGIN Public
        /// <summary>Gets a distinct label for repeated or disconnected physical units.</summary>
        public string Title => Instance.Label + " · " + Instance.NativeName + " · " + Instance.PhysicalGuid.ToString("D");
        /// <summary>Gets the GUID-qualified native DCS filename without copying a template GUID.</summary>
        public string FileName => Instance.NativeName + " {" + Instance.PhysicalGuid.ToString("D") + "}.diff.lua";
        #endregion END Public
    }
}

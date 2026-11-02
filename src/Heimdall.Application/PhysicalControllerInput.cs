namespace Heimdall.Application
{
    /// <summary>Local physical object identity and original enumeration position, independent of drawing aliases and game ordinals.</summary>
    public sealed record PhysicalControllerInput(string Id, int EnumerationIndex, PhysicalInputKind Kind,
        ControllerObject Evidence, bool Ambiguous)
    {
        #region BEGIN Public
        /// <summary>Gets the native instance encoded in the DirectInput object type.</summary>
        public int Instance => (int)((Evidence.NativeType >> 8) & 65535);
        /// <summary>Gets relative/absolute axis evidence, or null when the native type does not specify either.</summary>
        public bool? Relative => (Evidence.NativeType & 255) switch { 1 => true, 2 => false, _ => null };
        #endregion END Public
    }
}

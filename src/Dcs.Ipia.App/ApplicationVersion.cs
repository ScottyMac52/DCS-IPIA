namespace DcsIpia.Presentation
{
    /// <summary>Displays the application's four-part assembly version without generated build metadata.</summary>
    public static class ApplicationVersion
    {
        #region BEGIN Public
        /// <summary>Gets the version label used in application window titles.</summary>
        public static string Label => "version " + (typeof(App).Assembly.GetName().Version ?? new Version(0, 0, 0, 0)).ToString(4);
        #endregion END Public
    }
}

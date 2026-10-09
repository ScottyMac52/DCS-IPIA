using System.Text;

namespace Dcs.Ipia.Infrastructure
{
    /// <summary>Saves reviewed support text locally without replacing an existing file.</summary>
    public static class SupportReportFile
    {
        #region BEGIN Public
        /// <summary>Writes exactly the reviewed text; removes an incomplete new file on failure.</summary>
        /// <param name="path">User-selected new destination.</param>
        /// <param name="preview">Previously captured preview; never recollected during save.</param>
        public static void WriteNew(string path, string preview)
        {
            using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            try
            {
                var bytes = Encoding.UTF8.GetBytes(preview);
                output.Write(bytes);
                output.Flush(true);
            }
            catch
            {
                output.Dispose();
                File.Delete(path);
                throw;
            }
        }
        #endregion END Public
    }
}


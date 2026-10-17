using System.IO;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Infrastructure;

namespace DcsIpia.Presentation
{
    /// <summary>Decodes bounded local JPG/PNG images and normalizes accepted content for portable profile storage.</summary>
    public static class BackgroundImageLoader
    {
        #region BEGIN Public
        /// <summary>Reads a local image with an encoded-size and decoded-pixel budget.</summary>
        public static BitmapSource LoadFile(string path)
        {
            if (!new[] { ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException("Choose a JPG or PNG image.", nameof(path));
            if (new FileInfo(path).Length > FileAppearanceStore.MaximumImageBytes) throw new ArgumentException("Choose an image smaller than 20 MiB.", nameof(path));
            return Decode(File.ReadAllBytes(path));
        }
        /// <summary>Loads image bytes without retaining a lock on the original file.</summary>
        public static BitmapSource Decode(byte[] bytes)
        {
            if (bytes.Length > FileAppearanceStore.MaximumImageBytes) throw new ArgumentException("Image exceeds 20 MiB.", nameof(bytes));
            using var stream = new MemoryStream(bytes, false);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
            if (decoder is not (PngBitmapDecoder or JpegBitmapDecoder) || decoder.Frames.Count != 1)
                throw new ArgumentException("Choose a single-frame JPG or PNG image.", nameof(bytes));
            var frame = decoder.Frames[0];
            if (frame.PixelWidth > 8192 || frame.PixelHeight > 8192 || (long)frame.PixelWidth * frame.PixelHeight > 24000000)
                throw new ArgumentException("Choose an image at most 8192 pixels per side and 24 megapixels.", nameof(bytes));
            stream.Position = 0;
            var bitmap = new BitmapImage(); bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream;
            bitmap.DecodePixelWidth = Math.Min(frame.PixelWidth, 2560); bitmap.EndInit(); bitmap.Freeze();
            return bitmap;
        }
        /// <summary>Encodes a decoded preview as a metadata-free PNG for managed storage.</summary>
        public static byte[] Encode(BitmapSource image)
        {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = new MemoryStream(); encoder.Save(stream);
            if (stream.Length > FileAppearanceStore.MaximumImageBytes) throw new ArgumentException("Normalized image exceeds 20 MiB; choose a smaller image.", nameof(image));
            return stream.ToArray();
        }
        #endregion END Public
    }
}

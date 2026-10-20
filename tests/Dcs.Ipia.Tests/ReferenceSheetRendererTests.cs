using System.Text;
using Dcs.Ipia.Application;
using Dcs.Ipia.Rendering;
using SkiaSharp;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Verifies paper geometry, pagination, complete text and shared PNG/PDF output.</summary>
    public sealed class ReferenceSheetRendererTests
    {
        #region BEGIN Public
        /// <summary>Both paper sizes and orientations produce matching printable previews and PDFs.</summary>
        [Theory]
        [InlineData("A4", false, 1191, 1684)]
        [InlineData("A4", true, 1684, 1191)]
        [InlineData("Letter", false, 1224, 1584)]
        [InlineData("Letter", true, 1584, 1224)]
        public void Paper_geometry_and_exports_agree(string paper, bool landscape, int width, int height)
        {
            var sheet = ReferenceSheetRenderer.Render(Content(), new(paper, landscape));
            using var bitmap = SKBitmap.Decode(Assert.Single(sheet.PngPages));
            Assert.Equal(width, bitmap.Width); Assert.Equal(height, bitmap.Height);
            Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(sheet.Pdf));
            Assert.Equal(1, sheet.PageCount);
        }
        /// <summary>Long commands, native inputs and behaviour details continue onto extra pages without truncation.</summary>
        [Fact]
        public void Long_tables_are_paginated_without_losing_any_text()
        {
            var rows = Enumerable.Range(0, 120).Select(i => new ReferenceSheetRow("Command " + i,
                "Joy_" + i, "Primary · Hold · " + new string('W', 160))).ToArray();
            var content = Content() with { Rows = rows };
            var sheet = ReferenceSheetRenderer.Render(content, new());
            Assert.True(sheet.PageCount > 2);
            Assert.All(sheet.PngPages, png => { using var image = SKBitmap.Decode(png); Assert.NotNull(image); });
            Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(sheet.Pdf));
            Assert.Equal(rows.Length, content.Rows.Count);
        }
        /// <summary>Artwork is followed by a complete table appendix, including inputs with no illustrated callout.</summary>
        [Fact]
        public void Artwork_and_table_share_the_same_document_and_hidden_artwork_is_not_required()
        {
            using var bitmap = new SKBitmap(20, 30); bitmap.Erase(SKColors.Blue);
            using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            var art = ReferenceSheetRenderer.Render(Content(), new(Artwork: true), png.ToArray());
            Assert.Equal(2, art.PageCount);
            Assert.Equal(1, ReferenceSheetRenderer.Render(Content(), new()).PageCount);
            Assert.Throws<InvalidDataException>(() => ReferenceSheetRenderer.Render(Content(), new(Artwork: true)));
            Assert.Throws<InvalidDataException>(() => ReferenceSheetRenderer.Render(Content(), new(Artwork: true), [1, 2, 3]));
        }
        /// <summary>Empty filters still produce a readable sheet; invalid or excessive requests never silently clip.</summary>
        [Fact]
        public void Empty_and_invalid_requests_are_explicit()
        {
            Assert.Single(ReferenceSheetRenderer.Render(Content() with { Rows = [] }, new()).PngPages);
            Assert.Throws<InvalidDataException>(() => ReferenceSheetRenderer.Render(Content(), new("Unknown")));
            Assert.Throws<InvalidDataException>(() => ReferenceSheetRenderer.Render(Content() with { Title = "" }, new()));
            Assert.Throws<InvalidDataException>(() => ReferenceSheetRenderer.Render(Content() with { Rows = Enumerable.Repeat(new ReferenceSheetRow("x", "y", "z"), 2001).ToArray() }, new()));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ReferenceSheetContent Content() => new("My profile", "Test controller", "Ship",
            [new("Primary Fire", "Joy_1", "Primary · Press"), new("Pitch Axis", "Joy_YAxis", "Axis · Inverted · Deadzone 8%")]);
        #endregion END private helpers
    }
}

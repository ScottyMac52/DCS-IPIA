using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects stable occurrence addresses, complete block spans and atomic generic native source edits.</summary>
    public sealed class WarThunderOccurrenceTests
    {
        #region BEGIN Public
        /// <summary>Identical paths retain distinct addresses; editing values never renumbers alternatives.</summary>
        [Fact]
        public void Repeated_blocks_and_fields_have_unique_stable_addresses()
        {
            const string source = "root{item{value:i=1;value:i=2;}item{value:i=3;}}";
            var document = WarThunderBlkDocument.Parse(source);
            Assert.Equal(document.Blocks.Count, document.Blocks.Select(block => block.Address).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(document.Scalars.Count, document.Scalars.Select(field => field.Address).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal("item{value:i=1;value:i=2;}", source[document.Blocks[1].HeaderStart..document.Blocks[1].End]);
            Assert.Equal("1", source[document.Scalars[0].Start..document.Scalars[0].End]);
            var output = document.ReplaceOccurrences(new Dictionary<string, string>
            {
                [document.Scalars[0].Address] = "10", [document.Scalars[2].Address] = "30"
            });
            Assert.Equal("root{item{value:i=10;value:i=2;}item{value:i=30;}}", output);
            var parsed = WarThunderBlkDocument.Parse(output);
            Assert.Equal(document.Scalars.Select(field => field.Address), parsed.Scalars.Select(field => field.Address));
            Assert.Throws<InvalidOperationException>(() => document.ReplaceScalar("root/item/value", "4"));
            Assert.Equal(source, document.SerializeUnchanged());
        }
        /// <summary>All replacement tokens and addresses are validated before any output is returned.</summary>
        [Fact]
        public void Invalid_multi_edits_leave_the_source_unchanged()
        {
            const string source = "root{one:i=1;two:i=2;}";
            var document = WarThunderBlkDocument.Parse(source);
            Assert.Throws<ArgumentException>(() => document.ReplaceOccurrences(new Dictionary<string, string>
            {
                [document.Scalars[0].Address] = "3", [document.Scalars[1].Address] = "invalid"
            }));
            Assert.Throws<InvalidOperationException>(() => document.ReplaceOccurrences(new Dictionary<string, string> { ["unknown"] = "3" }));
            Assert.Equal(source, document.SerializeUnchanged());
        }
        /// <summary>Appending typed members preserves native newline style and validates the entire new fragment.</summary>
        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public void Appends_and_block_operations_preserve_unrelated_text(string newline)
        {
            var source = "root{" + newline + "one:i=1;child{two:i=2;}" + newline + "}";
            var document = WarThunderBlkDocument.Parse(source);
            var root = document.Blocks[0]; var child = document.Blocks[1];
            var appended = document.AppendScalar(child.Address, "flag", 'b', "yes");
            Assert.Contains(newline + "  flag:b=yes;" + newline, appended, StringComparison.Ordinal);
            Assert.Equal("root{" + newline + "one:i=1;" + newline + "}", document.RemoveBlock(child.Address));
            Assert.Equal("root{" + newline + "one:i=1;child{}" + newline + "}", document.EmptyBlock(child.Address));
            var added = document.AppendChild(root.Address, "future", new Dictionary<string, (char Type, string Value)>
                { ["value"] = ('i', "3"), ["name"] = ('t', "\"generic\"") });
            Assert.Contains("future{value:i=3;name:t=\"generic\";}", added, StringComparison.Ordinal);
            Assert.Throws<ArgumentException>(() => document.AppendScalar(root.Address, "bad name", 'i', "1"));
            Assert.Throws<ArgumentException>(() => document.AppendScalar(root.Address, "valid", 'i', "1;other:i=2"));
            Assert.Throws<InvalidOperationException>(() => document.EmptyBlock("missing"));
            Assert.Equal(source, document.SerializeUnchanged());
        }
        #endregion END Public
    }
}

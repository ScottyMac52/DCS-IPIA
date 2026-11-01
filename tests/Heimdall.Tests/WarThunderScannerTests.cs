using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects scanner transitions, nested ownership and repeated native occurrence identities.</summary>
    public sealed class WarThunderScannerTests
    {
        private static readonly string[] ExpectedScalarAddresses = new[] { "s:value[0]", "s:root[0]/value[0]", "s:root[0]/nested[0]/value[0]", "s:root[0]/value[1]", "s:root[1]/value[0]" };
        private static readonly string[] ExpectedBlockAddresses = new[] { "b:root[0]", "b:root[0]/nested[0]", "b:root[1]" };
        #region BEGIN Public
        /// <summary>Root scalars and repeated/nested blocks keep separate occurrence counters and exact source spans.</summary>
        [Fact]
        public void Nested_and_repeated_records_retain_addresses_and_source()
        {
            const string source = "value:i=1; root{value:i=2;nested{value:i=3;};value:i=4;} root{value:i=5;}";
            var document = WarThunderBlkDocument.Parse(source);
            Assert.False(document.HasOpaqueSyntax); Assert.Equal(source, document.SerializeUnchanged());
            Assert.Equal(ExpectedScalarAddresses, document.Scalars.Select(scalar => scalar.Address));
            Assert.Equal(ExpectedBlockAddresses, document.Blocks.Select(block => block.Address));
            var changed = document.ReplaceOccurrences(new Dictionary<string, string> { ["s:root[0]/value[1]"] = "9" });
            Assert.Equal(source.Replace("value:i=4", "value:i=9", StringComparison.Ordinal), changed);
        }
        /// <summary>Discarding unknown fragments marks evidence opaque while retaining recognized records and all original bytes.</summary>
        [Theory]
        [InlineData("root{stray;good:i=1;stray}")][InlineData("root{good:i=1;} trailing")][InlineData("root{/ good:i=1;}")]
        public void Pending_and_unrecognized_fragments_remain_preserved(string source)
        {
            var document = WarThunderBlkDocument.Parse(source);
            Assert.True(document.HasOpaqueSyntax); Assert.Single(document.Scalars); Assert.Equal(source, document.SerializeUnchanged());
            Assert.Throws<InvalidOperationException>(() => document.ReplaceScalar("root/good", "2"));
        }
        /// <summary>Invalid scalar/block transitions fail capture rather than inventing ownership.</summary>
        [Theory]
        [InlineData("{")][InlineData("}")][InlineData("a b{")][InlineData("value=1;")][InlineData("root{")][InlineData("root{a b=1;}")]
        public void Malformed_transitions_are_rejected(string source) => Assert.Throws<InvalidDataException>(() => WarThunderBlkDocument.Parse(source));
        #endregion END Public
    }
}

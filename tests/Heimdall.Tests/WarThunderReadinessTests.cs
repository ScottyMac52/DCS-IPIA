using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises parser/edit contracts needed before a shared game workspace can consume native BLK.</summary>
    public sealed class WarThunderReadinessTests
    {
        #region BEGIN Public
        /// <summary>Both native text spellings require one complete quoted token and preserve escaped content.</summary>
        [Theory]
        [InlineData('s')]
        [InlineData('t')]
        public void Text_replacements_are_quoted_single_tokens(char type)
        {
            var source = "root{label:" + type + "=\"old\";other:i=9;}";
            var document = WarThunderBlkDocument.Parse(source);
            Assert.False(document.HasOpaqueSyntax);
            Assert.Equal(source.Replace("\"old\"", "\"quoted \\\"value\\\"; {text} // retained\"", StringComparison.Ordinal),
                document.ReplaceScalar("root/label", "\"quoted \\\"value\\\"; {text} // retained\""));
            Assert.Throws<ArgumentException>(() => document.ReplaceScalar("root/label", "unquoted"));
            Assert.Throws<ArgumentException>(() => document.ReplaceScalar("root/label", "\"first\" \"second\""));
            Assert.Throws<ArgumentException>(() => document.ReplaceScalar("root/label", "\"unfinished"));
            Assert.Throws<ArgumentException>(() => document.ReplaceScalar("root/label", "\"text\" // suffix"));
            Assert.Equal(source, document.SerializeUnchanged());
        }
        /// <summary>Comment trivia after equals never becomes a scalar value or changes the captured edit span.</summary>
        [Theory]
        [InlineData("/* retained */ ")]
        [InlineData("// retained\r\n ")]
        public void Comment_trivia_does_not_become_the_value(string comment)
        {
            var source = "root{count:i=" + comment + "3;tail:i=9;}";
            var document = WarThunderBlkDocument.Parse(source);
            var scalar = Assert.Single(document.Scalars, value => value.Path == "root/count");
            Assert.Equal("3", scalar.Value);
            Assert.Equal("3", source[scalar.Start..scalar.End]);
            Assert.False(document.HasOpaqueSyntax);
            Assert.Equal(source.Replace("3;", "17;", StringComparison.Ordinal), document.ReplaceScalar("root/count", "17"));
        }
        /// <summary>Invalid numeric tokens, injected syntax and binary content cannot enter a replacement.</summary>
        [Theory]
        [InlineData("")]
        [InlineData(" 2")]
        [InlineData("//comment")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData("1e999")]
        [InlineData("1 2")]
        [InlineData("2//comment")]
        [InlineData("2;next:i=3")]
        [InlineData("\0")]
        public void Non_scalar_or_nonfinite_replacements_are_rejected(string value)
        {
            var document = WarThunderBlkDocument.Parse("root{value:r=1.25;}");
            Assert.Throws<ArgumentException>(() => document.ReplaceScalar("root/value", value));
            Assert.Equal("root{value:r=1.25;}", document.SerializeUnchanged());
        }
        /// <summary>Opaque captures remain lossless and inspectable but cannot be partially rewritten.</summary>
        [Theory]
        [InlineData("root{good:i=1; / }")]
        [InlineData("root{good:i=1;future:q=opaque;}")]
        [InlineData("root{good:i=1;value:r=NaN;}")]
        [InlineData("root{good:i=1;} trailing")]
        [InlineData("root{good:i=1;value:i=invalid;}")]
        public void Unsupported_captures_are_export_only(string source)
        {
            var controls = new WarThunderControls(source);
            Assert.True(controls.Document.HasOpaqueSyntax);
            Assert.Empty(controls.EditableSettings);
            Assert.Throws<InvalidOperationException>(() => controls.ReplaceReviewedSetting("root/good", "2"));
            Assert.Equal(source, controls.Document.SerializeUnchanged());
        }
        /// <summary>Repeated native block/scalar paths cannot accidentally edit the first occurrence.</summary>
        [Fact]
        public void Repeated_blocks_require_a_future_occurrence_address_contract()
        {
            const string source = "root{key{value:i=1;}key{value:i=2;}unique:i=3;}";
            var controls = new WarThunderControls(source);
            Assert.Equal(2, controls.Document.Blocks.Count(block => block.Path == "root/key"));
            Assert.Single(controls.EditableSettings, scalar => scalar.Path == "root/unique");
            Assert.DoesNotContain(controls.EditableSettings, scalar => scalar.Path == "root/key/value");
            Assert.Throws<InvalidOperationException>(() => controls.ReplaceReviewedSetting("root/key/value", "4"));
            Assert.Equal(source.Replace("unique:i=3", "unique:i=4", StringComparison.Ordinal), controls.ReplaceReviewedSetting("root/unique", "4"));
        }
        /// <summary>Callers cannot forge source spans by mutating the parser inventory.</summary>
        [Fact]
        public void Captured_source_span_collections_are_read_only()
        {
            var document = WarThunderBlkDocument.Parse("root{value:i=1;}");
            Assert.Throws<NotSupportedException>(() => ((IList<WarThunderBlkScalar>)document.Scalars).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<WarThunderBlkBlock>)document.Blocks).Clear());
            Assert.Equal("root{value:i=2;}", document.ReplaceScalar("root/value", "2"));
        }
        /// <summary>Invalid captured activation values are retained without crashing native inventory review.</summary>
        [Fact]
        public void Opaque_activation_values_remain_available_for_inspection()
        {
            const string source = "content{controls{hotkeys{fire{activationType:i=invalid;}other{activationType:i=9;}}}}";
            var controls = new WarThunderControls(source);
            Assert.Equal(9, Assert.Single(controls.ActivationCodes()));
            Assert.Contains(controls.Hotkeys, scalar => scalar.Value == "invalid");
            Assert.Empty(controls.EditableSettings);
            Assert.Equal(source, controls.Document.SerializeUnchanged());
        }
        /// <summary>Single-token enforcement retains signed numeric and supported integer-flag edits.</summary>
        [Theory]
        [InlineData("i", "-2147483648", "2147483647")]
        [InlineData("r", "-0.5", "2.5e-3")]
        [InlineData("b", "0", "1")]
        public void Supported_numeric_edits_reparse_without_changing_structure(string type, string before, string after)
        {
            var source = "root{value:" + type + "=" + before + ";}";
            var changed = WarThunderBlkDocument.Parse(source).ReplaceScalar("root/value", after);
            var document = WarThunderBlkDocument.Parse(changed);
            Assert.False(document.HasOpaqueSyntax);
            Assert.Equal(after, Assert.Single(document.Scalars).Value);
        }
        /// <summary>Comments without a following value and structural tokens are rejected.</summary>
        [Theory]
        [InlineData("root{count:i=/* comment */;}")]
        [InlineData("root{count:i=// comment")]
        [InlineData("root{count:i={}")]
        public void Missing_values_cannot_be_replaced_with_trivia(string source) =>
            Assert.Throws<InvalidDataException>(() => WarThunderBlkDocument.Parse(source));
        /// <summary>Capture limits and scalar-edit arguments fail before publishing output.</summary>
        [Fact]
        public void Invalid_capture_and_edit_arguments_are_rejected()
        {
            Assert.Throws<ArgumentNullException>(() => WarThunderBlkDocument.Parse(null!));
            Assert.Throws<InvalidDataException>(() => WarThunderBlkDocument.Parse(""));
            Assert.Throws<InvalidDataException>(() => WarThunderBlkDocument.Parse(new string(' ', 8 * 1024 * 1024 + 1)));
            var document = WarThunderBlkDocument.Parse("value:i=1;");
            Assert.Throws<ArgumentException>(() => document.ReplaceScalar("", "2"));
            Assert.Throws<ArgumentNullException>(() => document.ReplaceScalar("value", null!));
            Assert.Equal("value:i=1;", document.SerializeUnchanged());
        }
        #endregion END Public
    }
}

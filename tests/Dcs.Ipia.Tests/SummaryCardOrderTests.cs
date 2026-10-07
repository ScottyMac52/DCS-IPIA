using Dcs.Ipia.Application;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks insertion boundaries and numbered card identity after a drag move.</summary>
    public sealed class SummaryCardOrderTests
    {
        #region BEGIN Public
        /// <summary>Moves upward, downward and to the end while descriptions and accents follow their card.</summary>
        [Theory]
        [InlineData(0, 3, "B,C,A")]
        [InlineData(2, 0, "C,A,B")]
        [InlineData(0, 2, "B,A,C")]
        [InlineData(2, 1, "A,C,B")]
        public void Moves_and_renumbers(int source, int insertion, string expected)
        {
            ModuleSummaryCard[] cards = [new("07", "A", "red") { SourceIndex = 0 },
                new("07", "B", "green") { SourceIndex = 1 }, new("09", "C", "blue") { SourceIndex = 2 }];
            var moved = SummaryCardOrder.Move(cards, source, insertion);
            Assert.Equal(expected.Split(','), moved.Select(x => x.Text));
            Assert.Equal(new[] { "01", "02", "03" }, moved.Select(x => x.Key));
            Assert.All(moved, card => {
                var original = cards.Single(x => x.Text == card.Text);
                Assert.Equal(original.Accent, card.Accent);
                Assert.Equal(original.SourceIndex, card.SourceIndex);
            });
            Assert.Equal(new[] { "A", "B", "C" }, cards.Select(x => x.Text));
        }

        /// <summary>Drops immediately before/after the dragged card keep the exact original list.</summary>
        [Theory]
        [InlineData(0, 0)]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(1, 2)]
        public void Same_position_does_not_renumber(int source, int insertion)
        {
            ModuleSummaryCard[] cards = [new("Custom", "A", "red"), new("42", "B", "green")];
            Assert.Same(cards, SummaryCardOrder.Move(cards, source, insertion));
        }

        /// <summary>Invalid coordinates do not mutate cards, and a single card can remain in place.</summary>
        [Fact]
        public void Rejects_invalid_indices_and_handles_single_card()
        {
            ModuleSummaryCard[] cards = [new("07", "A", "red")];
            Assert.Same(cards, SummaryCardOrder.Move(cards, 0, 1));
            Assert.Throws<ArgumentNullException>(() => SummaryCardOrder.Move(null!, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SummaryCardOrder.Move([], 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SummaryCardOrder.Move(cards, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SummaryCardOrder.Move(cards, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SummaryCardOrder.Move(cards, 0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => SummaryCardOrder.Move(cards, 0, 2));
        }
        #endregion END Public
    }
}

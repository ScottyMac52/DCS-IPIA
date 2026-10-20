using System.Globalization;

namespace Heimdall.Application
{
    /// <summary>Moves a summary card to an insertion boundary and numbers the resulting list.</summary>
    public static class SummaryCardOrder
    {
        #region BEGIN Public
        /// <summary>Reorders without mutating the input, retaining each card's original metadata identity.</summary>
        /// <param name="cards">Current ordered form cards.</param>
        /// <param name="sourceIndex">Index of the dragged card.</param>
        /// <param name="insertionIndex">Boundary in the original list, from zero through Count.</param>
        /// <returns>Numbered cards, or the original list when the position is unchanged.</returns>
        public static IReadOnlyList<ModuleSummaryCard> Move(IReadOnlyList<ModuleSummaryCard> cards, int sourceIndex, int insertionIndex)
        {
            ArgumentNullException.ThrowIfNull(cards);
            if (sourceIndex < 0 || sourceIndex >= cards.Count)
                throw new ArgumentOutOfRangeException(nameof(sourceIndex));
            if (insertionIndex < 0 || insertionIndex > cards.Count)
                throw new ArgumentOutOfRangeException(nameof(insertionIndex));
            var destination = insertionIndex > sourceIndex ? insertionIndex - 1 : insertionIndex;
            if (destination == sourceIndex) return cards;
            var result = cards.ToList();
            var card = result[sourceIndex];
            result.RemoveAt(sourceIndex);
            result.Insert(destination, card);
            return result.Select((item, index) => item with { Key = (index + 1).ToString("D2", CultureInfo.InvariantCulture) }).ToArray();
        }
        #endregion END Public
    }
}

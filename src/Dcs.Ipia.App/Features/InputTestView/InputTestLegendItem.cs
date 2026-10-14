using System.ComponentModel;
using Dcs.Ipia.Application;

namespace DcsIpia.Features.InputTest
{
    /// <summary>Bindable live state for one colored kneeboard legend entry.</summary>
    public sealed class InputTestLegendItem : INotifyPropertyChanged
    {
        private bool _isActive;

        #region BEGIN Public
        /// <summary>Associates the visual row with its authoritative kneeboard legend entry.</summary>
        public InputTestLegendItem(KneeboardModifierLegend entry) =>
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));

        /// <summary>Source legend entry, including layer and full chord.</summary>
        public KneeboardModifierLegend Entry { get; }

        /// <summary>Identical wording to the exported kneeboard legend.</summary>
        public string Label => Entry.Label;

        /// <summary>Identical color to the exported kneeboard legend.</summary>
        public string Color => Entry.Chord.Length == 0
            ? KneeboardLiveProjection.BaseHighlightColor : Entry.Color;

        /// <summary>True for every polling frame in which this layer/chord is applicable.</summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value) return;
                _isActive = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            }
        }

        /// <summary>Signals visual changes without rebuilding the diagram or legend panel.</summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        #endregion END Public
    }
}

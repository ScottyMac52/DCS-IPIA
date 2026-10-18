using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace DcsIpia.Presentation;

/// <summary>Persists split proportions and disclosure state without involving the module workspace.</summary>
internal static class WorkspaceLayout
{
    #region BEGIN Public
    /// <summary>Controls whether a subtree may read or write user layout preferences.</summary>
    public static readonly DependencyProperty PersistenceEnabledProperty = DependencyProperty.RegisterAttached(
        "PersistenceEnabled", typeof(bool), typeof(WorkspaceLayout),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits));
    /// <summary>Reads the inherited preference isolation flag.</summary>
    public static bool GetPersistenceEnabled(DependencyObject element) => (bool)element.GetValue(PersistenceEnabledProperty);
    /// <summary>Sets preference isolation for a presentation subtree.</summary>
    public static void SetPersistenceEnabled(DependencyObject element, bool value) => element.SetValue(PersistenceEnabledProperty, value);

    /// <summary>Identifies a persisted splitter or disclosure.</summary>
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key", typeof(string), typeof(WorkspaceLayout), new PropertyMetadata(null, KeyChanged));
    /// <summary>Reads the preference key.</summary>
    public static string? GetKey(DependencyObject element) => (string?)element.GetValue(KeyProperty);
    /// <summary>Sets the preference key.</summary>
    public static void SetKey(DependencyObject element, string value) => element.SetValue(KeyProperty, value);

    #endregion END Public

    #region BEGIN private helpers
    private static void KeyChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not FrameworkElement view || e.OldValue is not null) return;
        view.Loaded += Restore;
        view.Unloaded += Save;
        view.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(Save), true);
        view.AddHandler(Keyboard.KeyUpEvent, new KeyEventHandler(Save), true);
        if (view is Expander expander)
        {
            expander.Expanded += Save;
            expander.Collapsed += Save;
        }
    }

    private static void Restore(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement view || !GetPersistenceEnabled(view) || GetKey(view) is not { } key) return;
        if (view is Grid grid && grid.ColumnDefinitions.Count == 3)
        {
            var first = grid.ColumnDefinitions[0];
            var last = grid.ColumnDefinitions[2];
            var available = Math.Max(1, grid.ActualWidth - grid.ColumnDefinitions[1].ActualWidth);
            var fallback = first.Width.IsStar && last.Width.IsStar
                ? first.Width.Value / (first.Width.Value + last.Width.Value) : first.ActualWidth / available;
            var ratio = WorkspaceLayoutStore.Read(key, Math.Clamp(fallback, .15, .7), .15, .7);
            first.Width = new GridLength(ratio, GridUnitType.Star);
            last.Width = new GridLength(1 - ratio, GridUnitType.Star);
        }
        else if (view is Expander expander)
            expander.IsExpanded = WorkspaceLayoutStore.Read(key, expander.IsExpanded ? 1 : 0, 0, 1) == 1;
    }

    private static void Save(object sender, RoutedEventArgs e)
    {
        if (e is KeyEventArgs && e.OriginalSource is not GridSplitter) return;
        if (sender is not FrameworkElement view || !view.IsLoaded || !GetPersistenceEnabled(view) || GetKey(view) is not { } key) return;
        if (view is Grid grid && grid.ColumnDefinitions.Count == 3)
        {
            var first = grid.ColumnDefinitions[0].ActualWidth;
            var last = grid.ColumnDefinitions[2].ActualWidth;
            if (first + last > 0) WorkspaceLayoutStore.Write(key, first / (first + last));
        }
        else if (view is Expander expander) WorkspaceLayoutStore.Write(key, expander.IsExpanded ? 1 : 0);
    }
    #endregion END private helpers
}

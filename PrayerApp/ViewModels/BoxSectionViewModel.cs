using CommunityToolkit.Mvvm.ComponentModel;
using PrayerApp.Helpers;
using PrayerApp.Models;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace PrayerApp.ViewModels;

/// <summary>
/// Represents a collapsible section (box/folder) on the Cards page.
/// Inherits from ObservableCollection&lt;PrayerCardViewModel&gt; so it can serve as
/// a group in a grouped CollectionView (MAUI requires the group to be IEnumerable&lt;T&gt;).
///
/// Collapse is achieved by clearing the observable collection while preserving a
/// backing list. Expand restores the items. This is the standard MAUI pattern since
/// grouped CollectionView has no native collapse support.
///
/// Notification suppression: bulk updates (expand, SetCards) suppress per-item
/// CollectionChanged events and fire a single Reset afterward. This prevents
/// N+1 layout passes in CollectionView.
/// </summary>
public class BoxSectionViewModel : ObservableCollection<PrayerCardViewModel>
{
    private List<PrayerCardViewModel> _backingCards = new();
    private bool _suppressNotifications;

    /// <summary>User's intended expansion state. Survives search/filter auto-expand cycles.</summary>
    private bool _userIsExpanded;

    /// <summary>True when section is expanded due to an active search/filter, not user choice.</summary>
    private bool _filterExpanded;

    /// <summary>Shorthand — ObservableCollection only exposes the EventArgs overload.</summary>
    private void NotifyChanged(string propertyName)
        => OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(propertyName));

    public int BoxId { get; }
    public bool IsSystem { get; }
    public string? SystemKey { get; }

    private string _name;
    public string Name
    {
        get => _name;
        private set
        {
            if (_name != value)
            {
                _name = value;
                NotifyChanged(nameof(Name));
                NotifyChanged(nameof(HeaderText));
            }
        }
    }

    public int CardCount => _backingCards.Count;

    public string HeaderText => CardCount > 0
        ? $"{Name} · {CardCount} {(CardCount == 1 ? "card" : "cards")}"
        : Name;

    private bool _isMultiSelectMode;
    /// <summary>Propagated from PrayerCardsViewModel to dim headers during multi-select.</summary>
    public bool IsMultiSelectMode
    {
        get => _isMultiSelectMode;
        set
        {
            if (_isMultiSelectMode != value)
            {
                _isMultiSelectMode = value;
                NotifyChanged(nameof(IsMultiSelectMode));
            }
        }
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                _userIsExpanded = value;
                ApplyExpansionState();
                NotifyChanged(nameof(IsExpanded));
            }
        }
    }

    public BoxSectionViewModel(CardBox box, bool defaultExpanded)
    {
        BoxId = box.Id;
        IsSystem = box.IsSystem;
        SystemKey = box.SystemKey;
        _name = box.Name;
        _userIsExpanded = defaultExpanded;
        _isExpanded = defaultExpanded;
    }

    /// <summary>
    /// Creates the "Unboxed"/"Unorganized" virtual section (no actual CardBox row).
    /// </summary>
    public BoxSectionViewModel(bool defaultExpanded)
    {
        BoxId = 0;
        IsSystem = false;
        SystemKey = null;
        _name = BoxStrings.Unorganized;
        _userIsExpanded = defaultExpanded;
        _isExpanded = defaultExpanded;
    }

    /// <summary>
    /// Replaces the section's cards. Updates backing list, notifies CardCount,
    /// and syncs the observable collection based on expansion state.
    /// </summary>
    public void SetCards(IEnumerable<PrayerCardViewModel> cards)
    {
        // 6b.2: skip Clear+Add+Reset when the sequence is unchanged — that's the
        // per-section cell cascade surviving 6b's BoxSections-replacement guard.
        // Per-card property changes still surface through each VM's PropertyChanged.
        var newList = cards.ToList();
        if (CardIdSequencesEqual(newList, _backingCards)) return;

        _backingCards = newList;
        NotifyChanged(nameof(CardCount));
        NotifyChanged(nameof(HeaderText));
        ApplyExpansionState();
    }

    private static bool CardIdSequencesEqual(
        IList<PrayerCardViewModel> a, IList<PrayerCardViewModel> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i].Id != b[i].Id) return false;
        return true;
    }

    /// <summary>
    /// Auto-expands during filter. User expansion state is preserved for restore.
    /// </summary>
    public void FilterExpand()
    {
        if (!_isExpanded)
        {
            _filterExpanded = true;
            _isExpanded = true;
            ApplyExpansionState();
            NotifyChanged(nameof(IsExpanded));
        }
    }

    /// <summary>
    /// Restores user expansion state after a filter is cleared.
    /// </summary>
    public void RestoreUserExpansionState()
    {
        if (_filterExpanded)
        {
            _filterExpanded = false;
            _isExpanded = _userIsExpanded;
            ApplyExpansionState();
            NotifyChanged(nameof(IsExpanded));
        }
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotifications)
            base.OnCollectionChanged(e);
    }

    /// <summary>
    /// Syncs the observable collection with the backing list based on expansion state.
    /// Uses notification suppression to fire a single Reset instead of N+1 events.
    /// </summary>
    private void ApplyExpansionState()
    {
        if (_isExpanded)
        {
            _suppressNotifications = true;
            Clear();
            foreach (var card in _backingCards)
                Add(card);
            _suppressNotifications = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
        else
        {
            if (Count > 0)
                Clear();
        }
    }
}

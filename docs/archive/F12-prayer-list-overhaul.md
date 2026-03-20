# F-12 — Prayer List Page UX Overhaul

## Context
The "Prayers" tab shows all prayer requests but has no search, no filter, and no explanation of its purpose. Users find it redundant and confusing. Goal: add live search, a 3-way status toggle, and a tag chip filter to make it genuinely useful for quickly finding and managing prayers.

**Prerequisite:** BUG-21 (tag model migration to prayer-request level) must be complete before the tag chip filter is implemented. Search by tag name is also deferred until BUG-21 is verified stable.

---

## Behavior Spec

| Feature | Detail |
|---------|--------|
| Default state on load | Active (unanswered) prayers only |
| Status toggle | 3 buttons: Active / Answered / All |
| Search scope | Prayer title + card name (tag-name search added post-BUG-21) |
| Tag filter | Horizontal scrolling chip row; tapping a chip toggles it; active chips highlighted with Primary color |
| Empty state | "No prayers match your filters." in MutedText style |
| Search bar | SearchBar control, live-filtering on TextChanged |

---

## ViewModel — `PrayerApp/ViewModels/PrayerListViewModel.cs`

### New types

```csharp
public enum FilterStatus { Active, Answered, All }

public class TagFilterChipViewModel : INotifyPropertyChanged
{
    public PrayerTag Tag { get; }
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    public ICommand ToggleCommand { get; }
    public TagFilterChipViewModel(PrayerTag tag, Action<TagFilterChipViewModel> onToggle)
    {
        Tag = tag;
        ToggleCommand = new RelayCommand(() => { IsSelected = !IsSelected; onToggle(this); });
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

### New properties on `PrayerListViewModel`

```csharp
private string _searchText = string.Empty;
public string SearchText
{
    get => _searchText;
    set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
}

private FilterStatus _statusFilter = FilterStatus.Active;
public FilterStatus StatusFilter
{
    get => _statusFilter;
    set { if (SetProperty(ref _statusFilter, value)) { OnPropertyChanged(nameof(IsActiveSelected)); OnPropertyChanged(nameof(IsAnsweredSelected)); OnPropertyChanged(nameof(IsAllSelected)); ApplyFilter(); } }
}
public bool IsActiveSelected   => StatusFilter == FilterStatus.Active;
public bool IsAnsweredSelected => StatusFilter == FilterStatus.Answered;
public bool IsAllSelected      => StatusFilter == FilterStatus.All;

public ObservableCollection<PrayerRequestDetailViewModel> FilteredPrayers { get; } = new();
public ObservableCollection<TagFilterChipViewModel> AvailableTags { get; } = new();
public bool HasTags => AvailableTags.Count > 0;

public ICommand SetStatusCommand { get; } // parameter: "Active" | "Answered" | "All"
```

### `ApplyFilter()` method

```csharp
private void ApplyFilter()
{
    var source = _allPrayerList; // unfiltered backing list of PrayerRequestDetailViewModel

    // Status filter
    IEnumerable<PrayerRequestDetailViewModel> result = StatusFilter switch
    {
        FilterStatus.Active   => source.Where(p => !p.IsAnswered),
        FilterStatus.Answered => source.Where(p => p.IsAnswered),
        _                     => source
    };

    // Tag filter
    var selectedTagIds = AvailableTags.Where(c => c.IsSelected).Select(c => c.Tag.Id).ToHashSet();
    if (selectedTagIds.Count > 0)
    {
        // Load request IDs matching selected tags (sync wrapper — tags are already cached)
        var matchingRequestIds = Task.Run(async () =>
            (await _tagService.GetRequestIdsByTagIdsAsync(selectedTagIds)).ToHashSet()).Result;
        result = result.Where(p => matchingRequestIds.Contains(p.PrayerId)); // PrayerId = Prayer.Id
    }

    // Text search (title + card name)
    if (!string.IsNullOrWhiteSpace(_searchText))
    {
        var q = _searchText.Trim();
        result = result.Where(p =>
            (p.Title?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (p.CardTitle?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    // Apply sort (existing ApplySorting logic — CardTitle then Title)
    var sorted = result
        .OrderBy(p => p.CardTitle, StringComparer.OrdinalIgnoreCase)
        .ThenBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
        .ToList();

    FilteredPrayers.Clear();
    foreach (var p in sorted)
        FilteredPrayers.Add(p);
}
```

**Note:** `PrayerRequestDetailViewModel` needs a `PrayerId` int property exposing `_prayer.Id` for the tag filter comparison. Add it if not already present.

### Tag loading (add to `LoadPrayersAsync`)

```csharp
var allTags = await _tagService.GetTagsAsync();
AvailableTags.Clear();
foreach (var tag in allTags)
    AvailableTags.Add(new TagFilterChipViewModel(tag, _ => ApplyFilter()));
OnPropertyChanged(nameof(HasTags));
```

---

## XAML — `PrayerApp/Views/Prayer/PrayerListPage.xaml`

Replace the `CollectionView.Header` and add controls above/within it:

```xaml
<CollectionView.Header>
    <VerticalStackLayout Margin="0,16,0,8" Spacing="8">

        <Label Text="Prayer Requests" Style="{StaticResource Headline}" ... />

        <!-- Status toggle -->
        <Grid ColumnDefinitions="*, *, *" ColumnSpacing="4">
            <Button Text="Active"
                    Command="{Binding SetStatusCommand}" CommandParameter="Active"
                    BackgroundColor="{Binding IsActiveSelected, Converter={StaticResource BoolToPrimaryColor}}"
                    TextColor="{Binding IsActiveSelected, Converter={StaticResource BoolToWhiteOrTertiary}}" />
            <Button Grid.Column="1" Text="Answered"
                    Command="{Binding SetStatusCommand}" CommandParameter="Answered"
                    BackgroundColor="{Binding IsAnsweredSelected, Converter={StaticResource BoolToPrimaryColor}}"
                    TextColor="{Binding IsAnsweredSelected, Converter={StaticResource BoolToWhiteOrTertiary}}" />
            <Button Grid.Column="2" Text="All"
                    Command="{Binding SetStatusCommand}" CommandParameter="All"
                    BackgroundColor="{Binding IsAllSelected, Converter={StaticResource BoolToPrimaryColor}}"
                    TextColor="{Binding IsAllSelected, Converter={StaticResource BoolToWhiteOrTertiary}}" />
        </Grid>

        <!-- Search bar -->
        <SearchBar Text="{Binding SearchText}"
                   Placeholder="Search prayers..."
                   BackgroundColor="Transparent" />

        <!-- Tag chips (only if tags exist) -->
        <ScrollView Orientation="Horizontal" IsVisible="{Binding HasTags}">
            <HorizontalStackLayout BindableLayout.ItemsSource="{Binding AvailableTags}" Spacing="6" Padding="0,4">
                <BindableLayout.ItemTemplate>
                    <DataTemplate x:DataType="viewModels:TagFilterChipViewModel">
                        <Border Padding="10,4" StrokeShape="RoundRectangle 12"
                                BackgroundColor="{Binding IsSelected, Converter={StaticResource BoolToPrimaryOrTransparent}}"
                                Stroke="{StaticResource Primary}">
                            <Border.GestureRecognizers>
                                <TapGestureRecognizer Command="{Binding ToggleCommand}" />
                            </Border.GestureRecognizers>
                            <Label Text="{Binding Tag.Name}"
                                   TextColor="{Binding IsSelected, Converter={StaticResource BoolToWhiteOrPrimary}}"
                                   FontSize="12" />
                        </Border>
                    </DataTemplate>
                </BindableLayout.ItemTemplate>
            </HorizontalStackLayout>
        </ScrollView>

    </VerticalStackLayout>
</CollectionView.Header>
```

Bind `ItemsSource` to `FilteredPrayers` (not `AllPrayers`).

Add `EmptyView`:
```xaml
<CollectionView.EmptyView>
    <Label Text="No prayers match your filters."
           Style="{StaticResource MutedText}"
           HorizontalOptions="Center"
           Margin="0,32" />
</CollectionView.EmptyView>
```

### Converters needed
- `BoolToPrimaryColor` — `true` → Primary, `false` → Transparent/Secondary
- `BoolToWhiteOrTertiary` — `true` → White, `false` → Tertiary
- `BoolToWhiteOrPrimary` — `true` → White, `false` → Primary
- `BoolToPrimaryOrTransparent` — `true` → Primary, `false` → Transparent
Check if any of these can reuse existing converters (e.g. `BoolToMutedColor`). Add new ones to `Converters/` if not.

---

## Code-behind — `PrayerListPage.xaml.cs`

No changes needed beyond existing `ContentPage_NavigatedTo`.

---

## Verification
1. Page loads showing Active prayers only
2. Tap "All" → answered prayers appear with strikethrough/muted style
3. Type in search → list live-filters by title and card name
4. Tap a tag chip → chip highlights; list shows only prayers with that tag
5. Combine all three filters → results narrow correctly
6. Clear all filters → full active list returns
7. Empty state message appears when no results match

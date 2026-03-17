# Plan: F-10 Deep-Link Share

## Context

Users want to share individual prayer requests and prayer cards with others via a native deep link. Recipients should have the shared content automatically saved in their app. This requires three sub-features working together: (1) a `DeepLinkService` that builds share payloads and handles incoming URIs, (2) a "Shared with me" system card that acts as a shared inbox for received prayer requests, and (3) a move-to-card action so recipients can reorganize received prayers.

**Decisions recorded during brainstorming:**
- Both cards and individual requests are shareable
- Cards → auto-save silently on receive; requests → auto-save to "Shared with me" system card
- System card: cannot be deleted, can be hidden via Settings, sorts first in the card list
- Request payload: title + notes; Card payload: title + all active request titles/notes
- Share message includes both deep link URI and plain-text fallback (for non-app users)
- URI scheme: `prayercards://`; architecture: centralized `IDeepLinkService`
- Share buttons: requests already have Share; cards get Share on both edit page and list swipe

> **Status:** Deferred — do not implement until app is live in the App Store / Play Store. The `prayercards://` URI scheme must be registered and live before links work for recipients.

---

## Task 1: DB + Model — Add `IsSystem` to `PrayerCard`

**`PrayerApp/Models/PrayerCard.cs`**
Add after `IsFavorite`:
```csharp
[Column("IsSystem")]
public bool IsSystem { get; set; } = false;
```

**`PrayerApp/Services/DBService.cs`** — `EnsurePrayerCardColumnsAsync()`
Add a try/catch migration block (same pattern as existing `IsFavorite` migration):
```csharp
try { await _db!.ExecuteAsync("ALTER TABLE PrayerCard ADD COLUMN IsSystem INTEGER DEFAULT 0"); }
catch { }
```

---

## Task 2: `ICardService` + `CardService` — System card support

**`PrayerApp/Services/ICardService.cs`**
Add:
```csharp
Task<PrayerCard> GetOrCreateSystemCardAsync();
```

**`PrayerApp/Services/CardService.cs`**
Implement:
```csharp
public async Task<PrayerCard> GetOrCreateSystemCardAsync()
{
    var all = await PrayerCard.LoadAllAsync();
    var existing = all.FirstOrDefault(c => c.IsSystem);
    if (existing is not null) return existing;

    var card = new PrayerCard { Title = "Shared with me", IsSystem = true };
    await card.SaveAsync();
    _cache = null;
    return card;
}
```

---

## Task 3: `IDeepLinkService` + `DeepLinkService` (new files)

**`PrayerApp/Services/IDeepLinkService.cs`** (new):
```csharp
public interface IDeepLinkService
{
    string BuildRequestShareText(Prayer prayer);
    string BuildCardShareText(PrayerCard card, IEnumerable<Prayer> prayers);
    Task HandleAsync(Uri uri);
}
```

**`PrayerApp/Services/DeepLinkService.cs`** (new):

Scheme: `prayercards://`

- `BuildRequestShareText(Prayer prayer)`:
  - URI: `prayercards://request?title=<url-encoded>&notes=<url-encoded>`
  - Returns: `"{uri}\n\n{prayer.Title}{notesIfAny}\n\n(Shared via Prayer Cards)"`

- `BuildCardShareText(PrayerCard card, IEnumerable<Prayer> prayers)`:
  - `requests` param = Base64Url-encoded UTF-8 JSON array: `[{"t":"...","n":"..."},...]`
  - URI: `prayercards://card?title=<url-encoded>&requests=<base64url>`
  - Returns: URI + plain-text listing of card title + bullet list of prayer titles

- `HandleAsync(Uri uri)`:
  - `uri.Host == "request"` → decode `title`/`notes` query params → `GetOrCreateSystemCardAsync()` → create `Prayer { PrayerCardId = systemCard.Id, Title, Details }` → save
  - `uri.Host == "card"` → decode `title` + base64 `requests` JSON → create `PrayerCard { Title }` → save → for each request, create `Prayer { PrayerCardId = card.Id, Title, Details }` → save

Constructor injects `ICardService` and `IPrayerService`.

**`PrayerApp/MauiProgram.cs`**
Register:
```csharp
builder.Services.AddSingleton<IDeepLinkService, DeepLinkService>();
```

---

## Task 4: Platform URI scheme registration

**`PrayerApp/Platforms/Android/AndroidManifest.xml`**
Add intent filter inside `<application>` (inside the `<activity>` element):
```xml
<intent-filter>
    <action android:name="android.intent.action.VIEW" />
    <category android:name="android.intent.category.DEFAULT" />
    <category android:name="android.intent.category.BROWSABLE" />
    <data android:scheme="prayercards" />
</intent-filter>
```

**`PrayerApp/Platforms/iOS/Info.plist`**
Add before `</dict>`:
```xml
<key>CFBundleURLTypes</key>
<array>
    <dict>
        <key>CFBundleURLName</key>
        <string>com.multithreadedllc.prayercards</string>
        <key>CFBundleURLSchemes</key>
        <array>
            <string>prayercards</string>
        </array>
    </dict>
</array>
```

---

## Task 5: Platform deep-link handlers

**`PrayerApp/Platforms/Android/MainActivity.cs`**
Add `OnNewIntent` override:
```csharp
protected override void OnNewIntent(Android.Content.Intent? intent)
{
    base.OnNewIntent(intent);
    if (intent?.Data is { } uri)
    {
        var svc = IPlatformApplication.Current!.Services.GetRequiredService<IDeepLinkService>();
        _ = svc.HandleAsync(new Uri(uri.ToString()));
    }
}
```

**`PrayerApp/Platforms/iOS/AppDelegate.cs`**
Add `OpenUrl` override:
```csharp
public override bool OpenUrl(UIKit.UIApplication app, Foundation.NSUrl url, Foundation.NSDictionary options)
{
    var svc = IPlatformApplication.Current!.Services.GetRequiredService<IDeepLinkService>();
    _ = svc.HandleAsync(new Uri(url.AbsoluteString));
    return true;
}
```
Add `using UIKit;` and `using Foundation;` if needed.

---

## Task 6: Update prayer request sharing

**`PrayerApp/ViewModels/PrayerRequestDetailViewModel.cs`**

Inject `IDeepLinkService` (same `IPlatformApplication.Current!.Services.GetRequiredService<>()` pattern already used for `_notificationService`).

Update `ShareAsync()`:
```csharp
private async Task ShareAsync()
{
    var text = _deepLinkService.BuildRequestShareText(_prayer);
    await Share.RequestAsync(new ShareTextRequest { Title = _prayer.Title, Text = text });
}
```

---

## Task 7: Add card sharing

**`PrayerApp/ViewModels/PrayerCardViewModel.cs`**

Add `ShareCommand` and `IsSystem` property:
- `IsSystem`: expose from `_card.IsSystem`
- `ShareCommand`: loads active (non-answered) prayers via `IPrayerService.GetPrayersByCardIdAsync(_card.Id)`, calls `_deepLinkService.BuildCardShareText(card, prayers)`, opens share sheet
- Hide `DeleteCommand` from XAML when `IsSystem` is true (bind button `IsVisible` to `!IsSystem`)
- Inject `IDeepLinkService` (same pattern)

**`PrayerApp/Views/PrayerCard/PrayerCardPage.xaml`**

Change action button grid from 2-column (Delete|Save) to 3-column (Delete|Share|Save):
```xaml
<Grid ColumnDefinitions="*, *, *" ColumnSpacing="4">
    <Button Command="{Binding DeleteCommand}" Text="Delete"
            IsVisible="{Binding IsNotSystem}" />
    <Button Grid.Column="1" Command="{Binding ShareCommand}" Text="Share" />
    <Button Grid.Column="2" Command="{Binding SaveCommand}" Text="Save" />
</Grid>
```
Add `IsNotSystem` computed property to ViewModel (`=> !IsSystem`).

**`PrayerApp/Views/PrayerCard/PrayerCardsPage.xaml`**

Add Share to the left-swipe group alongside Star and Edit:
```xaml
<SwipeItem Text="Share"
           BackgroundColor="{StaticResource Secondary}"
           Command="{Binding ShareCommand}" />
```

---

## Task 8: System card display rules

**`PrayerApp/ViewModels/PrayerCardsViewModel.cs`** — `ApplySorting()`

Update sort order: system cards first, then favorites, then alphabetical:
```csharp
.OrderByDescending(pc => pc.IsSystem)
.ThenByDescending(pc => pc.IsFavorite)
.ThenBy(pc => pc.Title)
```

Also filter out system cards when `Settings.HideSharedWithMe` is true before adding to the sorted list (or in `LoadPrayerCardsAsync`):
```csharp
var viewModels = _prayerCards
    .Where(pc => !pc.IsSystem || !Settings.HideSharedWithMe)
    .Select(pc => new PrayerCardViewModel(pc)).ToList();
```

**`PrayerApp/Views/PrayerCard/PrayerCardsPage.xaml.cs`** — `ContentPage_NavigatedTo`

Fill the currently-empty handler to trigger a reload (ensures Settings change takes effect):
```csharp
private void ContentPage_NavigatedTo(object sender, NavigatedToEventArgs e)
    => (BindingContext as PrayerCardsViewModel)?.Reload();
```

---

## Task 9: Move-to-card

**`PrayerApp/ViewModels/PrayerRequestDetailViewModel.cs`**

Add `MoveToCardCommand`:
```csharp
private async Task MoveToCardAsync()
{
    var cards = (await _cardService.GetCardsAsync())
        .Where(c => !c.IsSystem && c.Id != _prayer.PrayerCardId)
        .ToList();
    var titles = cards.Select(c => c.Title).ToArray();
    var chosen = await Shell.Current.DisplayActionSheetAsync("Move to card", "Cancel", null, titles);
    var target = cards.FirstOrDefault(c => c.Title == chosen);
    if (target is null) return;

    var oldCardId = _prayer.PrayerCardId;
    _prayer.PrayerCardId = target.Id;
    await _prayerService.SavePrayerAsync(_prayer);

    // Signal PrayerCardsViewModel to move the prayer between cards
    await Shell.Current.GoToAsync($"..?prayerMoved=true&prayerId={_prayer.Id}&fromCardId={oldCardId}&toCardId={target.Id}");
}
```
Inject `ICardService` (same pattern as other services).

**`PrayerApp/ViewModels/PrayerCardsViewModel.cs`** — `ApplyQueryAttributes`

Add handler for `prayerMoved`:
```csharp
else if (query.ContainsKey("prayerMoved")
         && int.TryParse(query["prayerId"].ToString(), out int movedId)
         && int.TryParse(query["fromCardId"].ToString(), out int fromId)
         && int.TryParse(query["toCardId"].ToString(), out int toId))
{
    AllPrayerCards.FirstOrDefault(c => c.Id == fromId)?.RemovePrayer(movedId);
    _ = AllPrayerCards.FirstOrDefault(c => c.Id == toId)?.AddOrUpdatePrayerAsync(movedId);
}
```

**`PrayerApp/Views/Prayer/PrayerDetailPage.xaml`** — edit mode action buttons

Add "Move to card…" button alongside Delete|Save (make it a 3-column grid):
```xaml
<Grid ColumnDefinitions="*, *, *" ColumnSpacing="8">
    <Button Grid.Column="0" Text="Delete" Command="{Binding DeleteCommand}" />
    <Button Grid.Column="1" Text="Move to card…" Command="{Binding MoveToCardCommand}" />
    <Button Grid.Column="2" Text="Save" Command="{Binding SaveCommand}" />
</Grid>
```

---

## Task 10: Settings — Hide "Shared with me"

**`PrayerApp/Services/Settings.cs`**
Add:
```csharp
public static bool HideSharedWithMe
{
    get => Preferences.Get(nameof(HideSharedWithMe), false);
    set => Preferences.Set(nameof(HideSharedWithMe), value);
}
```

**`PrayerApp/Views/Settings.xaml`**

Add a new row to the settings grid (increase `RowDefinitions` count and add Label + Switch matching the notifications row pattern):
```xaml
<Label Grid.Row="2" Text="Hide 'Shared with me' card?" />
<Switch x:Name="chkHideSharedWithMe"
        Grid.Row="2" Grid.Column="1"
        OnColor="{StaticResource Secondary}"
        Toggled="chkHideSharedWithMe_Toggled" />
```

**`PrayerApp/Views/Settings.xaml.cs`**
```csharp
protected override void OnAppearing()
{
    base.OnAppearing();
    chkSettingsAllowNotifications.IsToggled = PrayerApp.Services.Settings.AllowNotifications;
    chkHideSharedWithMe.IsToggled = PrayerApp.Services.Settings.HideSharedWithMe;
}

private void chkHideSharedWithMe_Toggled(object sender, ToggledEventArgs e)
    => PrayerApp.Services.Settings.HideSharedWithMe = e.Value;
```

---

## Verification

```bash
dotnet test PrayerApp.Tests/PrayerApp.Tests.csproj --configuration Release
```

**Manual checks:**
1. **Share request**: Open a prayer → tap Share → share sheet opens with deep link + plain text. Copy link and open on same device → "Shared with me" card auto-created, prayer appears inside it.
2. **Share card**: Open card edit page → tap Share (or swipe Share on card list) → share sheet opens with card + all active requests encoded.
3. **Receive card link**: Tap `prayercards://card?...` link → new card created and saved silently with all requests.
4. **System card rules**: "Shared with me" card appears at top of list. Delete button is hidden on its edit page and swipe. Toggle "Hide Shared with me" in Settings → card disappears; toggle off → reappears on navigating back.
5. **Move to card**: On a prayer in edit mode → tap "Move to card…" → action sheet lists available cards → select one → prayer moves; old card no longer shows it, new card does.
6. **No-app fallback**: Share message contains human-readable prayer text — verify in a messaging app preview.

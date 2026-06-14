namespace PrayerApp;

/// <summary>
/// Centralized route name constants. Used by ViewModels for navigation
/// and AppShell for route registration. Avoids nameof() dependency on
/// View types, enabling VM compilation in the test project.
/// </summary>
public static class Routes
{
    public const string PrayerCardPage = "PrayerCardPage";
    public const string PrayerDetailPage = "PrayerDetailPage";
    public const string PrayerTimePage = "PrayerTimePage";
    public const string TagDetailPage = "TagDetailPage";
    public const string AppSettingsPage = "AppSettingsPage";
    public const string BackupPage = "BackupPage";
    public const string AboutPage = "AboutPage";
    public const string HelpPage = "HelpPage";
    public const string ConfirmImportPage = "ConfirmImportPage";

    // Box management routes
    public const string BoxesPage = "BoxesPage";
    public const string BoxDetailPage = "BoxDetailPage";

    // Shell tab routes (absolute navigation)
    public const string PrayerCardsTab = "//CardsPage";
    public const string PrayersTab = "//PrayersPage";
    // Imported route carries the new card's id so PrayerCardsViewModel.ApplyQueryAttributes
    // stages PendingSavedIdentifier — that triggers the existing reveal machinery
    // (IsExpanded, IsHighlighted, EnsureSectionExpandedFor, ScrollToSavedCardAsync).
    public static string PrayerCardsTabImported(int savedCardId)
        => $"{PrayerCardsTab}?{QueryKeys.Saved}={savedCardId}";
    public static string PrayerCardsTabImportedToExisting(int cardId)
        => $"{PrayerCardsTab}?{QueryKeys.ImportedToExisting}={cardId}";

    // Prayer Time scope query parameter values
    public const string ScopeAll = "all";
    public const string ScopeTags = "tags";
    public const string ScopeBox = "box";
    public const string ScopeSelection = "selection";

    /// <summary>
    /// Query-string keys used in Shell navigation URLs and read by ApplyQueryAttributes
    /// receivers. Centralized so producers and consumers share a single source of truth —
    /// a rename here surfaces at every callsite at compile time instead of as a silent
    /// runtime no-op.
    /// </summary>
    public static class QueryKeys
    {
        public const string Saved = "saved";
        public const string Deleted = "deleted";
        public const string PrayerSaved = "prayerSaved";
        public const string PrayerDeleted = "prayerDeleted";
        public const string ParentCardId = "parentCardId";
        public const string OldCardId = "oldCardId";
        public const string ImportedToExisting = "importedToExisting";
    }
}

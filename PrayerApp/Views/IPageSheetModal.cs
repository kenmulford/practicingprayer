namespace PrayerApp.Views;

/// <summary>
/// Marker interface for pages that should present as PageSheet on iPad.
/// Implement on any ContentPage pushed via PushModalAsync that should
/// use the card-style modal instead of full-screen.
/// On iPhone, PageSheet and FullScreen are visually identical — no effect.
/// </summary>
public interface IPageSheetModal { }

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.Views.PrayerCard;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public class PrayerCardsViewModel : ObservableObject, IQueryAttributable
    {
        private List<PrayerCard> _prayerCards;
        private readonly ICardService _cardService;
        private readonly IOnboardingService _onboardingService;
        public ObservableCollection<PrayerCardViewModel> AllPrayerCards { get; }
        public ObservableCollection<PrayerCardViewModel> FilteredPrayerCards { get; } = new();
        private bool _isSorting;
        private bool _suppressFilterAnnounce;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                    SemanticScreenReader.Announce(value ? "Loading" : "Content loaded");
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
        }

        public ICommand NewCommand { get; }

        /// <summary>Raised when a newly created card should be scrolled to and highlighted.</summary>
        public event EventHandler<PrayerCardViewModel>? HighlightCardRequested;

        #region Constructors

        public PrayerCardsViewModel(ICardService cardService, IOnboardingService onboardingService)
        {
            _cardService = cardService;
            _onboardingService = onboardingService;

            _prayerCards = new List<PrayerCard>();
            AllPrayerCards = new ObservableCollection<PrayerCardViewModel>();

            // register commands
            NewCommand = new AsyncRelayCommand(NewPrayerCardAsync);
        }

        public PrayerCardsViewModel() : this(
            IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IOnboardingService>())
        { }

        #endregion

        #region Private Methods

        private async Task NewPrayerCardAsync()
        {
            _onboardingService.Advance(); // CreateCard → NameCard (no-op if not at CreateCard)
            await Shell.Current.GoToAsync(nameof(Views.PrayerCard.PrayerCardPage));
        }

        #endregion

        #region Implemented Contract Methods

        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("deleted"))
            {
                string? PrayerCardString = query["deleted"].ToString();
                PrayerCardViewModel? matched = AllPrayerCards.FirstOrDefault<PrayerCardViewModel>(pc => pc.Identifier == PrayerCardString);

                if (matched != null)
                {
                    AllPrayerCards.Remove(matched);
                    ApplySorting();
                }
            }
            else if (query.ContainsKey("saved"))
            {
                string? PrayerCardString = query["saved"].ToString();
                PrayerCardViewModel? matched = AllPrayerCards.Where((c) => c.Identifier == PrayerCardString).FirstOrDefault();

                // If card is found, update it
                if (matched != null)
                {
                    matched.Reload();
                }
                // If card isn't found, it's new; add it.
                else
                {
                    AddNewCardAsync(PrayerCardString).SafeFireAndForget();
                }
            }
            else if (query.ContainsKey("prayerSaved") && query.ContainsKey("parentCardId"))
            {
                if (int.TryParse(query["prayerSaved"].ToString(), out int prayerId)
                    && int.TryParse(query["parentCardId"].ToString(), out int parentCardId))
                {
                    // If the prayer moved to a different card, remove it from the old card
                    if (query.TryGetValue("oldCardId", out var oldVal)
                        && int.TryParse(oldVal?.ToString(), out int oldCardId))
                    {
                        var oldCard = AllPrayerCards.FirstOrDefault(card => card.Id == oldCardId);
                        oldCard?.RemovePrayer(prayerId);
                    }

                    var matched = AllPrayerCards.FirstOrDefault(card => card.Id == parentCardId);
                    if (matched != null)
                    {
                        matched.AddOrUpdatePrayerAsync(prayerId).SafeFireAndForget();
                    }
                }
            }
            else if (query.ContainsKey("prayerDeleted") && query.ContainsKey("parentCardId"))
            {
                if (int.TryParse(query["prayerDeleted"].ToString(), out int prayerId)
                    && int.TryParse(query["parentCardId"].ToString(), out int parentCardId))
                {
                    var matched = AllPrayerCards.FirstOrDefault(card => card.Id == parentCardId);
                    matched?.RemovePrayer(prayerId);
                }
            }
        }

        #endregion

        #region Helper Methods

        private async Task AddNewCardAsync(string? cardIdString)
        {
            try
            {
                var card = await PrayerCard.LoadAsync(int.Parse(cardIdString ?? "0"));
                if (card is null) return;
                var newCard = new PrayerCardViewModel(card);
                SubscribeToPropertyChanges(newCard);
                AllPrayerCards.Add(newCard);

                // Expand + highlight the new card (accordion handler auto-collapses others)
                newCard.IsExpanded = true;
                newCard.IsHighlighted = true;

                ApplySorting(); // also calls ApplyFilter()

                HighlightCardRequested?.Invoke(this, newCard);
            }
            catch (Exception e)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to add new card: {e.Message}", "OK");
            }
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            _suppressFilterAnnounce = true;
            try
            {
                _cardService.InvalidateCache();
                var cards = await _cardService.GetCardsAsync();
                _prayerCards = cards.ToList();

                var viewModels = _prayerCards.Select(pc => new PrayerCardViewModel(pc)).ToList();
                foreach (var vm in viewModels)
                {
                    SubscribeToPropertyChanges(vm);
                }

                AllPrayerCards.Clear();
                foreach (var vm in viewModels)
                {
                    AllPrayerCards.Add(vm);
                }

                ApplySorting();
            }
            catch (Exception e)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to load card: {e.Message}", "OK");
            }
            finally
            {
                _suppressFilterAnnounce = false;
                IsLoading = false;
            }
        }

        private void ApplySorting()
        {
            if (_isSorting) return;
            _isSorting = true;
            try
            {
                var sorted = AllPrayerCards
                    .OrderByDescending(pc => pc.IsSystem)
                    .ThenByDescending(pc => pc.IsFavorite)
                    .ThenBy(pc => pc.Title)
                    .ToList();

                // Only update if order changed (minimize UI updates)
                bool needsUpdate = false;
                for (int i = 0; i < sorted.Count; i++)
                {
                    if (i >= AllPrayerCards.Count || AllPrayerCards[i] != sorted[i])
                    {
                        needsUpdate = true;
                        break;
                    }
                }

                if (needsUpdate)
                {
                    AllPrayerCards.Clear();
                    foreach (var card in sorted)
                    {
                        AllPrayerCards.Add(card);
                    }
                }
            }
            finally
            {
                _isSorting = false;
            }

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            IEnumerable<PrayerCardViewModel> result = AllPrayerCards;

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var q = _searchText.Trim();
                result = result.Where(c =>
                    c.Title?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
            }

            FilteredPrayerCards.Clear();
            foreach (var card in result)
                FilteredPrayerCards.Add(card);

            if (!_suppressFilterAnnounce)
                SemanticScreenReader.Announce($"Showing {FilteredPrayerCards.Count} cards");
        }

        private void SubscribeToPropertyChanges(PrayerCardViewModel card)
        {
            card.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PrayerCardViewModel.Title)
                    || e.PropertyName == nameof(PrayerCardViewModel.IsFavorite))
                {
                    ApplySorting();
                }
                // Single-open accordion: expanding one card collapses all others
                else if (e.PropertyName == nameof(PrayerCardViewModel.IsExpanded)
                         && card.IsExpanded)
                {
                    foreach (var other in AllPrayerCards)
                        if (other != card && other.IsExpanded)
                            other.IsExpanded = false;

                    SemanticScreenReader.Announce($"Expanded {card.Title}");
                }
            };
        }

        public void Reload()
        {
            LoadAsync().SafeFireAndForget();
        }

        /// <summary>
        /// Lightweight refresh for cross-tab consistency. Detects new/deleted cards
        /// without tearing down the entire ViewModel state (preserving expanded
        /// accordions, loaded prayers, etc.). Called from OnAppearing on subsequent
        /// tab visits.
        /// </summary>
        public async Task RefreshAsync()
        {
            _cardService.InvalidateCache();
            var cards = await _cardService.GetCardsAsync();
            var freshIds = cards.Select(c => c.Id).ToHashSet();
            var currentIds = AllPrayerCards.Select(c => c.Id).ToHashSet();

            // Remove deleted cards
            var toRemove = AllPrayerCards.Where(c => !freshIds.Contains(c.Id)).ToList();
            foreach (var vm in toRemove)
                AllPrayerCards.Remove(vm);

            // Add new cards
            foreach (var card in cards.Where(c => !currentIds.Contains(c.Id)))
            {
                var vm = new PrayerCardViewModel(card);
                SubscribeToPropertyChanges(vm);
                AllPrayerCards.Add(vm);
            }

            // Refresh prayer counts on all existing cards (e.g. Quick Add added a prayer)
            foreach (var vm in AllPrayerCards)
                vm.RefreshActivePrayerCount();

            ApplySorting();
        }

        #endregion
    }
}

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
    internal class PrayerCardsViewModel : ObservableObject, IQueryAttributable
    {
        private List<PrayerCard> _prayerCards;
        private readonly ICardService _cardService;
        private readonly IOnboardingService _onboardingService;
        public ObservableCollection<PrayerCardViewModel> AllPrayerCards { get; }
        private bool _isSorting;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ICommand NewCommand { get; }

        #region Constructors

        public PrayerCardsViewModel()
        {
            _cardService = IPlatformApplication.Current!.Services.GetRequiredService<ICardService>();
            _onboardingService = IPlatformApplication.Current!.Services.GetRequiredService<IOnboardingService>();

            _prayerCards = new List<PrayerCard>();
            AllPrayerCards = new ObservableCollection<PrayerCardViewModel>();

            // register commands
            NewCommand = new AsyncRelayCommand(NewPrayerCardAsync);
        }

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
                ApplySorting();
            }
            catch (Exception e)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to add new card: {e.Message}", "OK");
            }
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
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

            ApplySorting();
        }

        #endregion
    }
}

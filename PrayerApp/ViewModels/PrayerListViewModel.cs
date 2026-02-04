using PrayerApp.Models;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    internal class PrayerListViewModel : IQueryAttributable
    {
        private List<Prayer> _prayerList;
        public ObservableCollection<PrayerRequestDetailViewModel> AllPrayers { get; }

        public ICommand NewCommand { get; }

        public PrayerListViewModel()
        {
            // GET all prayer requests
            _prayerList = Task.Run(async () => await Prayer.LoadAllAsync()).Result;

            // Convert Prayer to PrayerRequestDetailViewModel
            AllPrayers = new ObservableCollection<PrayerRequestDetailViewModel>(
                _prayerList.Select(p => new PrayerRequestDetailViewModel(p))
            );

            // subscribe to collection changes to re-sort when items are added/removed
            AllPrayers.CollectionChanged += (s, e) => ApplySorting();

            // subscribe to property changes on each existing prayer
            foreach (var prayer in AllPrayers)
            {
                SubscribeToPropertyChanges(prayer);
            }

            // sort the prayer list
            ApplySorting();

            // register commands
            NewCommand = new AsyncRelayCommand(NewPrayerAsync);
        }

        #region IQueryAttributable Implementation
        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("deleted"))
            {
                string? PrayerString = query["deleted"].ToString();
                PrayerRequestDetailViewModel matched = AllPrayers.FirstOrDefault(p => p.Identifier == PrayerString);

                if (matched != null)
                {
                    AllPrayers.Remove(matched);
                }
            }
            else if (query.ContainsKey("saved"))
            {
                string? PrayerString = query["saved"].ToString();
                PrayerRequestDetailViewModel matched = AllPrayers.Where((p) => p.Identifier == PrayerString).FirstOrDefault();

                // If prayer is found, update it
                if (matched != null)
                {
                    matched.Reload();
                }
                // If prayer isn't found, it's new; add it.
                else
                {
                    _ = AddNewPrayerAsync(PrayerString);
                }
            }
        }

        #endregion

        #region private methods

        private async Task AddNewPrayerAsync(string? prayerIdString)
        {
            try
            {
                var p = await Prayer.LoadAsync(int.Parse(prayerIdString ?? "0"));
                var newPrayer = new PrayerRequestDetailViewModel(p);
                SubscribeToPropertyChanges(newPrayer);
                AllPrayers.Add(newPrayer);
            }
            catch (Exception e)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to add new prayer: {e.Message}", "OK");
            }
        }

        private async Task NewPrayerAsync()
        {
            await Shell.Current.GoToAsync(nameof(Views.Prayer.PrayerDetailPage));
        }

        private async Task LoadPrayersAsync()
        {
            try
            {
                _prayerList = await Prayer.LoadAllAsync();
            }
            catch (Exception e)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to load prayer: {e.Message}", "OK");
            }
        }

        private void ApplySorting()
        {
            var sorted = AllPrayers
                .OrderByDescending(p => p.Title)
                .ToList();

            // Only update if order changed (minimize UI updates)
            bool needsUpdate = false;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i >= AllPrayers.Count || AllPrayers[i] != sorted[i])
                {
                    needsUpdate = true;
                    break;
                }
            }

            if (needsUpdate)
            {
                AllPrayers.Clear();
                foreach (var p in sorted)
                {
                    AllPrayers.Add(p);
                }
            }
        }

        // Only name properties used for sorting/filtering; not all of them
        private void SubscribeToPropertyChanges(PrayerRequestDetailViewModel prayer)
        {
            prayer.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PrayerRequestDetailViewModel.Title))
                {
                    ApplySorting();
                }
            };
        }


        public void Reload()
        {
            _ = LoadPrayersAsync();
        }

        #endregion
    }
}

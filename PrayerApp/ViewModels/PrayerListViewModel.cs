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
        public ObservableCollection<PrayerDetailViewModel> AllPrayers { get; }

        public ICommand NewCommand { get; }

        public PrayerListViewModel()
        {
            // GET all categories
            _prayerList = Task.Run(async () => await Prayer.LoadAllAsync()).Result;

            // Convert PrayerCategory to PrayerCategoryViewModel
            AllPrayers = new ObservableCollection<PrayerDetailViewModel>(
                _prayerList.Select(p => new PrayerDetailViewModel(p))
            );

            // Subscribe to collection changes to re-sort when items are added/removed
            AllPrayers.CollectionChanged += (s, e) => ApplySorting();

            // Subscribe to property changes on each existing category
            foreach (var p in AllPrayers)
            {
                SubscribeToPropertyChanges(p);
            }

            // sort the category list
            ApplySorting();

            // register commands
            NewCommand = new AsyncRelayCommand(NewPrayerDetailAsync);
        }

        #region IQueryAttributable Implementation
        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("deleted"))
            {
                string? PrayerString = query["deleted"].ToString();
                PrayerDetailViewModel matched = AllPrayers.FirstOrDefault(p => p.Identifier == PrayerString);

                if (matched != null)
                {
                    AllPrayers.Remove(matched);
                }
            }
            else if (query.ContainsKey("saved"))
            {
                string? PrayerString = query["saved"].ToString();
                PrayerDetailViewModel matched = AllPrayers.Where((p) => p.Identifier == PrayerString).FirstOrDefault();

                // If note is found, update it
                if (matched != null)
                {
                    matched.Reload();
                }
                // If note isn't found, it's new; add it.
                else
                {
                    var p = Prayer.LoadAsync(int.Parse(PrayerString ?? "0")).Result;
                    var newPrayer = new PrayerDetailViewModel(p);
                    SubscribeToPropertyChanges(newPrayer);
                    AllPrayers.Add(newPrayer);
                }
            }
        }

        #endregion

        #region private methods

        private async Task NewPrayerDetailAsync()
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
                await Shell.Current.DisplayAlert("Error", $"Failed to load prayer: {e.Message}", "OK");
            }
            finally
            {

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
        private void SubscribeToPropertyChanges(PrayerDetailViewModel prayer)
        {
            prayer.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PrayerDetailViewModel.Title))
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

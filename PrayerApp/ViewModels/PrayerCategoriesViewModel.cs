using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;
using PrayerApp.Views.PrayerCategory;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    internal class PrayerCategoriesViewModel : IQueryAttributable
    {
        private List<PrayerCategory> _prayerCategories;
        public ObservableCollection<PrayerCategoryViewModel> AllPrayerCategories { get; }



        public ICommand NewCommand { get; }

        #region Constructors

        public PrayerCategoriesViewModel()
        {
            // GET all categories
            _prayerCategories = Task.Run(async () => await PrayerCategory.LoadAllAsync()).Result;
            
            // Convert PrayerCategory to PrayerCategoryViewModel
            AllPrayerCategories = new ObservableCollection<PrayerCategoryViewModel>(
                _prayerCategories.Select(pc => new PrayerCategoryViewModel(pc))
            );

            // Subscribe to collection changes to re-sort when items are added/removed
            AllPrayerCategories.CollectionChanged += (s, e) => ApplySorting();

            // Subscribe to property changes on each existing category
            foreach (var category in AllPrayerCategories) {
                SubscribeToPropertyChanges(category);
            }
            
            // sort the category list
            ApplySorting();

            // register commands
            NewCommand = new AsyncRelayCommand(NewPrayerCategoryAsync);
        }

        #endregion

        #region Private Methods

        private async Task NewPrayerCategoryAsync()
        {
            await Shell.Current.GoToAsync(nameof(Views.PrayerCategory.PrayerCategoryPage));
        }

        #endregion

        #region Implemented Contract Methods

        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("deleted"))
            {
                string? PrayerCategoryString = query["deleted"].ToString();
                PrayerCategoryViewModel matched = AllPrayerCategories.FirstOrDefault(pc => pc.Identifier == PrayerCategoryString);

                if(matched != null)
                {
                    AllPrayerCategories.Remove(matched);
                }
            }
            else if (query.ContainsKey("saved"))
            {
                string? PrayerCategoryString = query["saved"].ToString();
                PrayerCategoryViewModel matched = AllPrayerCategories.Where((c) => c.Identifier == PrayerCategoryString).FirstOrDefault();

                // If note is found, update it
                if (matched != null)
                {
                    matched.Reload();
                }
                // If note isn't found, it's new; add it.
                else
                {
                    var cat = PrayerCategory.LoadAsync(int.Parse(PrayerCategoryString ?? "0")).Result;
                    var newCategory = new PrayerCategoryViewModel(cat);
                    SubscribeToPropertyChanges(newCategory);
                    AllPrayerCategories.Add(newCategory);
                }
            }
        }

        #endregion

        #region Helper Methods

        private async Task LoadPrayerCategoriesAsync()
        {
            try
            {
                _prayerCategories = await PrayerCategory.LoadAllAsync();
            }
            catch (Exception e)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to load category: {e.Message}", "OK");
            }
            finally
            {
                
            }
        }

        private void ApplySorting()
        {
            var sorted = AllPrayerCategories
                .OrderByDescending(pc => pc.IsFavorite)
                .ThenBy(pc => pc.Name)
                .ToList();

            // Only update if order changed (minimize UI updates)
            bool needsUpdate = false;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i >= AllPrayerCategories.Count || AllPrayerCategories[i] != sorted[i])
                {
                    needsUpdate = true;
                    break;
                }
            }

            if (needsUpdate)
            {
                AllPrayerCategories.Clear();
                foreach (var category in sorted)
                {
                    AllPrayerCategories.Add(category);
                }
            }
        }

        private void SubscribeToPropertyChanges(PrayerCategoryViewModel category)
        {
            category.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PrayerCategoryViewModel.IsFavorite))
                {
                    ApplySorting();
                }
            };
        }


        public void Reload()
        {
            _ = LoadPrayerCategoriesAsync();
        }

        #endregion

    }
}

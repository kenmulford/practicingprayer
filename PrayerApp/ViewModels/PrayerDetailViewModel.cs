using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;
using PrayerApp.Views.Prayer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;

namespace PrayerApp.ViewModels
{
    internal class PrayerDetailViewModel : ObservableObject, IQueryAttributable
    {
        private Prayer _prayer;
        private string? _categoryName;
        public ICommand SaveCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand SelectPrayerCommand { get; private set; }

        // expose available frequency options for binding to pickers
        public IReadOnlyList<PrayerFrequency> FrequencyOptions { get; } = Enum.GetValues<PrayerFrequency>();

        #region Properties
        public string Identifier => _prayer.Id.ToString();

        public int Id
        {
            get => _prayer.Id;
            set
            {
                if (_prayer.Id != value)
                {
                    _prayer.Id = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Title
        {
            get => _prayer.Title;
            set
            {
                if (_prayer.Title != value)
                {
                    _prayer.Title = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? Details
        {
            get => _prayer.Details;
            set
            {
                if (_prayer.Details != value)
                {
                    _prayer.Details = value;
                    OnPropertyChanged();
                }
            }
        }
        public int PrayerCategoryId
        {
            get => _prayer.PrayerCategoryId;
            set
            {
                if (_prayer.PrayerCategoryId != value)
                {
                    _prayer.PrayerCategoryId = value;
                    OnPropertyChanged();
                    _ = LoadCategoryNameAsync(); // refresh category name when id changes
                }
            }
        }

        // Expose category name for bindings
        public string CategoryName
        {
            get => _categoryName ?? "Uncategorized";
            private set
            {
                if (_categoryName != value)
                {
                    _categoryName = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CanNotify
        {
            get => _prayer.CanNotify;
            set
            {
                if (_prayer.CanNotify != value)
                {
                    _prayer.CanNotify = value;
                    OnPropertyChanged();
                }
            }
        }

        // Enum-backed frequency property for binding
        public PrayerFrequency PrayerFrequency
        {
            get => _prayer.PrayerFrequency;
            set
            {
                if (_prayer.PrayerFrequency != value)
                {
                    _prayer.PrayerFrequency = value;
                    OnPropertyChanged(nameof(PrayerFrequencyDisplay));
                }
            }
        }

        // human-friendly display string
        public string PrayerFrequencyDisplay => PrayerFrequency.ToString();

        public bool IsAnswered
        {
            get => _prayer.IsAnswered;
            set
            {
                if (_prayer.IsAnswered != value)
                {
                    _prayer.IsAnswered = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime CreatedAt => _prayer.CreatedAt;
        public DateTime UpdatedAt => _prayer.UpdatedAt;
        #endregion

        #region Constructors
        public PrayerDetailViewModel()
        {
            _prayer = new Prayer();

            SaveCommand = new AsyncRelayCommand(SaveAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
            SelectPrayerCommand = new AsyncRelayCommand(SelectPrayerAsync);
        }
        public PrayerDetailViewModel(Prayer prayer)
        {
            _prayer = prayer ?? throw new ArgumentNullException(nameof(prayer));

            SaveCommand = new AsyncRelayCommand(SaveAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
            SelectPrayerCommand = new AsyncRelayCommand(SelectPrayerAsync);

            // start loading dependent data (category name)
            _ = LoadCategoryNameAsync();
        }
        #endregion

        #region Command Definitions

        private async Task SaveAsync()
        {
            _prayer.UpdatedAt = DateTime.Now;
            await _prayer.SaveAsync();
            await Shell.Current.GoToAsync($"..?saved={Identifier}");
        }

        private async Task DeleteAsync()
        {
            await _prayer.DeleteAsync();
            await Shell.Current.GoToAsync($"..?deleted={Identifier}");
        }
        private async Task SelectPrayerAsync()
        {
            await Shell.Current.GoToAsync($"{nameof(PrayerDetailPage)}?load={Identifier}");
        }

        #endregion


        #region IQueryAttributable Implementation
        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("load"))
            {
                if (int.TryParse(query["load"].ToString(), out int _id))
                {
                    // fire task and forget
                    _ = LoadPrayerAsync(_id);
                }

                RefreshProperties();
            }
        }
        #endregion

        private async Task LoadPrayerAsync(int id)
        {
            try
            {
                _prayer = await Prayer.LoadAsync(id);
            }
            catch (Exception e)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to load prayer: {e.Message}", "OK");
            }
            finally
            {
                RefreshProperties();
                _ = LoadCategoryNameAsync();
            }
        }

        private async Task LoadCategoryNameAsync()
        {
            try
            {
                if (PrayerCategoryId <= 0)
                {
                    CategoryName = "Uncategorized";
                    return;
                }

                var category = await PrayerCategory.LoadAsync(PrayerCategoryId);
                CategoryName = category?.Name ?? "Uncategorized";
            }
            catch
            {
                // If DB service hasn't been registered yet or load failed
                CategoryName = "Uncategorized";
            }
        }

        public void Reload()
        {
            _ = LoadPrayerAsync(_prayer.Id);
            RefreshProperties();
        }

        private void RefreshProperties()
        {
            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Details));
            OnPropertyChanged(nameof(PrayerCategoryId));
            OnPropertyChanged(nameof(CanNotify));

            OnPropertyChanged(nameof(IsAnswered));
            OnPropertyChanged(nameof(CreatedAt));
            OnPropertyChanged(nameof(UpdatedAt));
            OnPropertyChanged(nameof(Identifier));
            OnPropertyChanged(nameof(CategoryName));
            OnPropertyChanged(nameof(PrayerFrequencyDisplay));
        }
    }
}

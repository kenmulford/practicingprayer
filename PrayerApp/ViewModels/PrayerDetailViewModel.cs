using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;
using PrayerApp.Views.Prayer;
using PrayerApp.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;
using System.Collections.ObjectModel;

namespace PrayerApp.ViewModels
{
    internal class PrayerDetailViewModel : ObservableObject, IQueryAttributable
    {
        private readonly ICategoryService _categoryService;
        private Prayer _prayer;
        private string? _categoryName;
        private PrayerCategory? _category;

        public ICommand SaveCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand SelectPrayerCommand { get; private set; }

        // expose available frequency options for binding to pickers
        public ObservableCollection<PrayerFrequency> FrequencyOptions { get; private set; } = new();

        // categories for picker
        public ObservableCollection<PrayerCategory> Categories { get; } = new();

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
                    _ = LoadCategoryAsync(); // refresh category name when id changes
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
        public PrayerCategory? SelectedCategory
        {
            get { return _category; }
            set
            {
                if (_category != value)
                {
                    _category = value;
                    // update the underlying id and name
                    if (_category != null)
                    {
                        PrayerCategoryId = _category.Id;
                        CategoryName = _category.Name ?? "Uncategorized";
                    }
                    else
                    {
                        PrayerCategoryId = 0;
                        CategoryName = "Uncategorized";
                    }
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
        public PrayerDetailViewModel(ICategoryService categoryService)
        {
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
            _prayer = new Prayer();

            LoadCommonConstructorObjects();
        }
        public PrayerDetailViewModel(Prayer prayer, ICategoryService categoryService) : this(categoryService)
        {
            _prayer = prayer ?? throw new ArgumentNullException(nameof(prayer));

            LoadCommonConstructorObjects();
        }

        // kept for tests or other activations if needed
        public PrayerDetailViewModel() : this(new CategoryService()) { }

        // New overload to preserve existing call sites that pass a Prayer
        public PrayerDetailViewModel(Prayer prayer) : this(prayer, new CategoryService()) { }
        
        private void LoadCommonConstructorObjects()
        {
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
            SelectPrayerCommand = new AsyncRelayCommand(SelectPrayerAsync);

            // start loading categories
            _ = LoadCategoriesAsync();
            _ = LoadPrayerFrequenciesList();
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

        private async Task LoadPrayerFrequenciesList()
        {
            var FrequencyOptions = new ObservableCollection<PrayerFrequency>(
                (PrayerFrequency[])Enum.GetValues<PrayerFrequency>()
            );

        }
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
                _ = LoadCategoryAsync();
            }
        }

        private async Task LoadCategoryAsync()
        {
            try
            {
                if (PrayerCategoryId <= 0)
                {
                    CategoryName = "Uncategorized";
                    return;
                }

                var category = await PrayerCategory.LoadAsync(PrayerCategoryId);
                _category = category;
                CategoryName = category?.Name ?? "Uncategorized";

                // ensure SelectedCategory reflects the loaded category
                if (category != null)
                {
                    // if Categories already loaded, set selected to matching instance
                    var match = Categories.FirstOrDefault(c => c.Id == category.Id);
                    if (match != null)
                        _category = match;
                }
            }
            catch
            {
                // If DB service hasn't been registered yet or load failed
                CategoryName = "Uncategorized";
            }
        }

        public async Task LoadCategoriesAsync()
        {
            try
            {
                var categories = await _categoryService.GetCategoriesAsync();

                // Sort: favorites first, then by name
                var sorted = categories.OrderByDescending(c => c.IsFavorite).ThenBy(c => c.Name).ToList();

                Categories.Clear();
                foreach (var c in sorted)
                    Categories.Add(c);

                // if we have a selected id, set SelectedCategory to matching item
                if (PrayerCategoryId > 0)
                {
                    SelectedCategory = Categories.FirstOrDefault(c => c.Id == PrayerCategoryId);
                }
            }
            catch
            {
                // ignore - UI will show uncategorized
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

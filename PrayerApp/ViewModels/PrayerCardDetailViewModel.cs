using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.Views.Prayer;

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
    internal class PrayerCardDetailViewModel : ObservableObject, IQueryAttributable
    {
        private readonly ITagService _tagService;
        private readonly ICardService _cardService;
        private PrayerCard _prayerCard;

        public ICommand SaveCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand SelectPrayerCommand { get; private set; }

        // expose available frequency options for binding to pickers
        public ObservableCollection<PrayerFrequency> FrequencyOptions { get; private set; } = new();

        // Tag selection view model
        public PrayerTagSelectionViewModel TagSelectionViewModel { get; private set; }

        #region Properties
        public string Identifier => _prayerCard.Id.ToString();

        public int Id
        {
            get => _prayerCard.Id;
            set
            {
                if (_prayerCard.Id != value)
                {
                    _prayerCard.Id = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Title
        {
            get => _prayerCard.Title;
            set
            {
                if (_prayerCard.Title != value)
                {
                    _prayerCard.Title = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CanNotify
        {
            get => _prayerCard.CanNotify;
            set
            {
                if (_prayerCard.CanNotify != value)
                {
                    _prayerCard.CanNotify = value;
                    OnPropertyChanged();
                }
            }
        }

        // Enum-backed frequency property for binding
        public PrayerFrequency PrayerFrequency
        {
            get => _prayerCard.PrayerFrequency;
            set
            {
                if (_prayerCard.PrayerFrequency != value)
                {
                    _prayerCard.PrayerFrequency = value;
                    OnPropertyChanged(nameof(PrayerFrequencyDisplay));
                }
            }
        }

        // human-friendly display string
        public string PrayerFrequencyDisplay => PrayerFrequency.ToString();

        public bool IsAnswered
        {
            get => _prayerCard.IsAnswered;
            set
            {
                if (_prayerCard.IsAnswered != value)
                {
                    _prayerCard.IsAnswered = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime CreatedAt => _prayerCard.CreatedAt;
        public DateTime UpdatedAt => _prayerCard.UpdatedAt;
        #endregion

        #region Constructors
        public PrayerCardDetailViewModel(ITagService tagService, ICardService cardService)
        {
            _tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
            _cardService = cardService ?? throw new ArgumentNullException(nameof(cardService));
            _prayerCard = new PrayerCard();
            TagSelectionViewModel = new PrayerTagSelectionViewModel(tagService);

            LoadCommonConstructorObjects();
        }
        public PrayerCardDetailViewModel(PrayerCard prayerCard, ITagService tagService, ICardService cardService) : this(tagService, cardService)
        {
            _prayerCard = prayerCard ?? throw new ArgumentNullException(nameof(prayerCard));

            LoadCommonConstructorObjects();
        }

        // kept for tests or other activations if needed
        public PrayerCardDetailViewModel() : this(new TagService(new DBService(Path.Combine(FileSystem.AppDataDirectory, "prayer_app.db"))), new CardService()) { }

        // New overload to preserve existing call sites that pass a PrayerCard
        public PrayerCardDetailViewModel(PrayerCard prayerCard) : this(prayerCard, new TagService(new DBService(Path.Combine(FileSystem.AppDataDirectory, "prayer_app.db"))), new CardService()) { }

        private void LoadCommonConstructorObjects()
        {
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
            SelectPrayerCommand = new AsyncRelayCommand(SelectPrayerAsync);

            _ = LoadPrayerFrequenciesList();
        }
        #endregion

        #region Command Definitions

        private async Task SaveAsync()
        {
            _prayerCard.UpdatedAt = DateTime.Now;
            await _cardService.SaveCardAsync(_prayerCard);
            await Shell.Current.GoToAsync($"..?saved={Identifier}");
        }

        private async Task DeleteAsync()
        {
            await _cardService.DeleteCardAsync(_prayerCard);
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
                    _ = LoadPrayerCardAsync(_id);
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

        private async Task LoadPrayerCardAsync(int id)
        {
            try
            {
                _prayerCard = await PrayerCard.LoadAsync(id);
                await TagSelectionViewModel.InitializeForCardAsync(id);
            }
            catch (Exception e)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to load prayer: {e.Message}", "OK");
            }
            finally
            {
                RefreshProperties();
            }
        }

        public void Reload()
        {
            _ = LoadPrayerCardAsync(_prayerCard.Id);
            RefreshProperties();
        }

        private void RefreshProperties()
        {
            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(CanNotify));
            OnPropertyChanged(nameof(IsAnswered));
            OnPropertyChanged(nameof(CreatedAt));
            OnPropertyChanged(nameof(UpdatedAt));
            OnPropertyChanged(nameof(Identifier));
            OnPropertyChanged(nameof(PrayerFrequencyDisplay));
        }
    }
}

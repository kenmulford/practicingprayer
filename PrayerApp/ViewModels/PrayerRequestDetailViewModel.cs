using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.Views.Prayer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public class PrayerRequestDetailViewModel : ObservableObject, IQueryAttributable
    {
        private Prayer _prayer;
        private readonly IPrayerService _prayerService;

        public ICommand SaveCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand SelectPrayerCommand { get; private set; }
        public ICommand EditPrayerCommand { get; private set; }
        public ICommand MarkAnsweredCommand { get; private set; }

        private string _savedQueryKey = "saved";
        private string _deletedQueryKey = "deleted";
        public bool ReturnToCards { get; set; }

        private bool _isReadOnly;
        public bool IsReadOnly
        {
            get => _isReadOnly;
            set
            {
                if (_isReadOnly != value)
                {
                    _isReadOnly = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsEditable));
                }
            }
        }

        public bool IsEditable => !IsReadOnly;

        public bool IsNotAnswered => !IsAnswered;

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

        public int PrayerCardId
        {
            get => _prayer.PrayerCardId;
            set
            {
                if (_prayer.PrayerCardId != value)
                {
                    _prayer.PrayerCardId = value;
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

        public string PrayerFrequencyDisplay => PrayerFrequency.ToString();

        public bool IsAnswered
        {
            get => _prayer.IsAnswered;
            set
            {
                if (_prayer.IsAnswered != value)
                {
                    _prayer.IsAnswered = value;
                    _prayer.AnsweredAt = value ? DateTime.Now : null;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AnsweredAt));
                    OnPropertyChanged(nameof(AnsweredAtDisplay));
                    OnPropertyChanged(nameof(IsNotAnswered));
                }
            }
        }

        public DateTime? AnsweredAt => _prayer.AnsweredAt;

        public string AnsweredAtDisplay =>
            IsAnswered && AnsweredAt.HasValue
                ? $"✓ {AnsweredAt.Value:MMM d}"
                : string.Empty;

        public IReadOnlyList<PrayerFrequency> FrequencyOptions { get; } =
            new ReadOnlyCollection<PrayerFrequency>(Enum.GetValues<PrayerFrequency>().ToList());

        public DateTime CreatedAt => _prayer.CreatedAt;
        public DateTime UpdatedAt => _prayer.UpdatedAt;

        public PrayerRequestDetailViewModel()
        {
            _prayer = new Prayer();
            _prayerService = IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>();
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
            SelectPrayerCommand = new AsyncRelayCommand(SelectPrayerAsync);
            EditPrayerCommand = new AsyncRelayCommand(EditPrayerAsync);
            MarkAnsweredCommand = new AsyncRelayCommand(MarkAnsweredAsync);
        }

        public PrayerRequestDetailViewModel(Prayer prayer) : this()
        {
            _prayer = prayer ?? new Prayer();
            IsReadOnly = false;
        }

        private async Task SaveAsync()
        {
            _prayer.UpdatedAt = DateTime.Now;
            await _prayerService.SavePrayerAsync(_prayer);
            if (ReturnToCards)
            {
                await Shell.Current.GoToAsync($"..?prayerSaved={Identifier}&parentCardId={PrayerCardId}");
            }
            else
            {
                await Shell.Current.GoToAsync($"..?{_savedQueryKey}={Identifier}");
            }
        }

        private async Task DeleteAsync()
        {
            await _prayerService.DeletePrayerAsync(_prayer);
            if (ReturnToCards)
            {
                await Shell.Current.GoToAsync($"..?prayerDeleted={Identifier}&parentCardId={PrayerCardId}");
            }
            else
            {
                await Shell.Current.GoToAsync($"..?{_deletedQueryKey}={Identifier}");
            }
        }

        private async Task SelectPrayerAsync()
        {
            if (ReturnToCards)
            {
                await Shell.Current.GoToAsync($"{nameof(PrayerDetailPage)}?load={Identifier}&viewOnly=true&returnToCards=true&parentCardId={PrayerCardId}");
            }
            else
            {
                await Shell.Current.GoToAsync($"{nameof(PrayerDetailPage)}?load={Identifier}&viewOnly=true");
            }
        }

        private async Task EditPrayerAsync()
        {
            if (ReturnToCards)
            {
                await Shell.Current.GoToAsync($"{nameof(PrayerDetailPage)}?load={Identifier}&edit=true&returnToCards=true&parentCardId={PrayerCardId}");
            }
            else
            {
                await Shell.Current.GoToAsync($"{nameof(PrayerDetailPage)}?load={Identifier}&edit=true");
            }
        }

        private async Task MarkAnsweredAsync()
        {
            IsAnswered = true;
            _prayer.UpdatedAt = DateTime.Now;
            await _prayerService.SavePrayerAsync(_prayer);
            RefreshProperties();
        }

        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("newForCard"))
            {
                if (int.TryParse(query["newForCard"].ToString(), out int cardId))
                {
                    _prayer = new Prayer { PrayerCardId = cardId };
                    ReturnToCards = true;
                    _savedQueryKey = "prayerSaved";
                    _deletedQueryKey = "prayerDeleted";
                    IsReadOnly = false;
                    RefreshProperties();
                }
            }
            else if (query.ContainsKey("load"))
            {
                if (query.ContainsKey("returnToCards"))
                {
                    ReturnToCards = true;
                    _savedQueryKey = "prayerSaved";
                    _deletedQueryKey = "prayerDeleted";
                }

                if (query.ContainsKey("viewOnly"))
                {
                    IsReadOnly = true;
                }
                else if (query.ContainsKey("edit"))
                {
                    IsReadOnly = false;
                }

                if (int.TryParse(query["load"].ToString(), out int _id))
                {
                    _ = LoadPrayerAsync(_id);
                }
                RefreshProperties();
            }
        }

        private async Task LoadPrayerAsync(int id)
        {
            try
            {
                _prayer = await Prayer.LoadAsync(id);
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
            _ = LoadPrayerAsync(_prayer.Id);
            RefreshProperties();
        }

        private void RefreshProperties()
        {
            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Details));
            OnPropertyChanged(nameof(PrayerCardId));
            OnPropertyChanged(nameof(CanNotify));
            OnPropertyChanged(nameof(IsAnswered));
            OnPropertyChanged(nameof(AnsweredAt));
            OnPropertyChanged(nameof(AnsweredAtDisplay));
            OnPropertyChanged(nameof(CreatedAt));
            OnPropertyChanged(nameof(UpdatedAt));
            OnPropertyChanged(nameof(Identifier));
            OnPropertyChanged(nameof(PrayerFrequencyDisplay));
            OnPropertyChanged(nameof(IsReadOnly));
            OnPropertyChanged(nameof(IsEditable));
            OnPropertyChanged(nameof(IsNotAnswered));
        }
    }
}

using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;

namespace PrayerApp.ViewModels;

public class TagFilterChipViewModel : ObservableObject
{
    public PrayerTag Tag { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
                OnPropertyChanged(nameof(AccessibleDescription));
        }
    }

    public string AccessibleDescription =>
        $"{Tag.Name}, {(IsSelected ? "selected" : "not selected")}";

    public ICommand ToggleCommand { get; }

    public TagFilterChipViewModel(PrayerTag tag, Action<TagFilterChipViewModel> onToggle)
    {
        Tag = tag;
        ToggleCommand = new RelayCommand(() =>
        {
            IsSelected = !IsSelected;
            onToggle(this);
        });
    }
}

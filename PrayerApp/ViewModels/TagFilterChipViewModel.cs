using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;

namespace PrayerApp.ViewModels;

public class TagFilterChipViewModel : INotifyPropertyChanged
{
    public PrayerTag Tag { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

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

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

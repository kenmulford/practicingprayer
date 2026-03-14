using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PrayerApp.ViewModels;

public class TagChipViewModel
{
    public int Id { get; }
    public string Name { get; }
    public Color ChipColor { get; }
    public ICommand RemoveCommand { get; }

    public TagChipViewModel(PrayerTag tag, Func<int, Task> onRemove)
    {
        Id = tag.Id;
        Name = tag.Name ?? string.Empty;
        ChipColor = TagColorPalette.Resolve(tag.Color);
        RemoveCommand = new AsyncRelayCommand(() => onRemove(Id));
    }
}

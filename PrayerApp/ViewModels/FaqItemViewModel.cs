using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace PrayerApp.ViewModels;

public class FaqItemViewModel : ObservableObject
{
    public string Question { get; }
    public string Answer { get; }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public ICommand ToggleCommand { get; }

    public FaqItemViewModel(string question, string answer)
    {
        Question = question;
        Answer = answer;
        ToggleCommand = new RelayCommand(() =>
        {
            IsExpanded = !IsExpanded;
            if (IsExpanded)
                SemanticScreenReader.Announce(Answer);
        });
    }
}

using System.Collections.ObjectModel;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private string _bookTitle = "Aon Companion";
    private string _sectionTitle = "Welcome";

    public MainViewModel()
    {
        Blocks = new ObservableCollection<ContentBlockViewModel>
        {
            new("p", "Select a book to begin your adventure.")
        };

        Choices = new ObservableCollection<ChoiceViewModel>
        {
            new("Start", new RelayCommand(() => { }))
        };
    }

    public string BookTitle
    {
        get => _bookTitle;
        set
        {
            if (_bookTitle == value)
            {
                return;
            }

            _bookTitle = value;
            OnPropertyChanged();
        }
    }

    public string SectionTitle
    {
        get => _sectionTitle;
        set
        {
            if (_sectionTitle == value)
            {
                return;
            }

            _sectionTitle = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ContentBlockViewModel> Blocks { get; }
    public ObservableCollection<ChoiceViewModel> Choices { get; }
}

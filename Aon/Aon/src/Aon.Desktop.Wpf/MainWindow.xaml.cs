using Aon.Desktop.Wpf.ViewModels;

namespace Aon.Desktop.Wpf;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}

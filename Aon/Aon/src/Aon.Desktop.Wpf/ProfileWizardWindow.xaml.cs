using System.Windows;
using Aon.Desktop.Wpf.ViewModels;

namespace Aon.Desktop.Wpf;

public partial class ProfileWizardWindow : Window
{
    public ProfileWizardWindow(ProfileWizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public ProfileWizardViewModel? ViewModel => DataContext as ProfileWizardViewModel;

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

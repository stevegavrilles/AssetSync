using System.Windows;
using AssetSync.App.ViewModels;

namespace AssetSync.App.Views.SetupWizard;

public partial class SetupWizardWindow : Window
{
    public SetupWizardWindow(SetupWizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += () =>
        {
            DialogResult = true;
            Close();
        };
    }
}

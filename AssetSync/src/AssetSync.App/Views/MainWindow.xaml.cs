using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AssetSync.App.ViewModels;

namespace AssetSync.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MainViewModel>();
    }
}

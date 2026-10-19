using Microsoft.UI.Xaml.Controls;
using SystemCompass.App.ViewModels;

namespace SystemCompass.App.Views;

public sealed partial class ReportPage : Page
{
    public ReportPage()
    {
        ViewModel = new ReportViewModel(App.Services, App.IsDemo);
        InitializeComponent();
    }

    public ReportViewModel ViewModel { get; }
}

using System.Windows;
using System.Windows.Controls;
using SprayerControl.App.ViewModels;
namespace SprayerControl.App.Views;
public partial class LogView : UserControl
{
    private LogViewModel? _viewModel; public event Action? BackRequested;
    public LogView() => InitializeComponent(); public void Bind(LogViewModel viewModel) { _viewModel = viewModel; Entries.ItemsSource = viewModel.Entries; }
    private void Back_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke();
    private void Clear_Click(object sender, RoutedEventArgs e) { if (_viewModel is not null && MessageBox.Show("确认清空全部操作日志？", "清空确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) _viewModel.Clear(); }
}

using Microsoft.Maui.Controls;

namespace SelfBooru
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            this.Navigating += OnShellNavigating;
        }

        private async void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
        {
            // If the page we're leaving is SettingsPage...
            if (this.CurrentPage is SettingsPage settingsPage)
            {
                // And its VM says a scan is running...
                if (settingsPage.BindingContext is SettingsViewModel vm && vm.IsScanning)
                {
                    // Cancel navigation
                    e.Cancel();

                    // Optional: alert the user
                    await DisplayAlert(
                        "Scan in progress",
                        "Please cancel the scan before leaving Settings.",
                        "OK");
                }
            }
        }
    }
}
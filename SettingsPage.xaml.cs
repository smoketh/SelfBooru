using LiteDB;
using Windows.UI.ApplicationSettings;

namespace SelfBooru;

public partial class SettingsPage : ContentPage
{
    
    public SettingsPage()
	{
		InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, true);
    }
    private async void OnCloseClicked(object sender, EventArgs e)
    {
        if (this.BindingContext is SettingsViewModel vm && vm.IsScanning)
        {
            // Optional: alert the user
            await DisplayAlert(
                "Scan in progress",
                "Please cancel the scan before leaving Settings.",
                "OK");
            return;
        }

        await Navigation.PopModalAsync(animated: true);
    }
    private void ChangeOutputDir(object sender, EventArgs e)
    {
        if (BindingContext is SettingsViewModel vm)
        {
            vm.OutputDir = outputdirentry.Text;
        }

    }
}
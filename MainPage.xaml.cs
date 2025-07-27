using LiteDB;
using System.Text.RegularExpressions;
namespace SelfBooru
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, true);
        }
        async void OnOpenSettings(object sender, EventArgs e)
        {
            var app = (App)Application.Current!;
            await Navigation.PushModalAsync(new NavigationPage(new SettingsPage()), animated: true);
        }

        void OnThumbnailTapped(object sender, EventArgs e)
        {
            if (sender is Image img && img.BindingContext is ImageItem item)
            {
                if (BindingContext is MainPageViewModel vm)
                {
                    //vm.MetaData = item.Metadata;
                    //vm.SelectedImagePath = item.FullPath;
                    vm.SelectedImage = item;
                    vm.IsPreviewVisible = true;
                }
            }
        }

        
        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            if (BindingContext is MainPageViewModel vm)
            {
                if (!vm.suppressSuggestions)
                {
                    vm.UpdateSuggestions();
                }
                else
                {
                    vm.suppressSuggestions = false;
                }
            }    
                
        }

        private void OnSuggestionSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is string selected && BindingContext is MainPageViewModel vm)
            {
                vm.ApplySuggestion(selected);
                TryRefocusSearch();
            }
        }

        private void OnRelatedTagSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is string selected && BindingContext is MainPageViewModel vm)
            {
                vm.AddTag(selected);
                
                //SearchEntry.CursorPosition = SearchEntry.Text.Length;
                TryRefocusSearch(true);
            }
        }

        private async void TryRefocusSearch(bool unfocusAfter=false)
        {
            await Task.Delay(25);
            if (SearchEntry.Focus())
            {
                SearchEntry.Text = SearchEntry.Text + " ";
                SearchEntry.Text = Regex.Replace(SearchEntry.Text, " {2,}", " ");
                SearchEntry.CursorPosition = SearchEntry.Text.Length;
                if (unfocusAfter)
                {
                    await Task.Delay(25);
                    RelatedTagView.Focus(); // SearchEntry.Unfocus();
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Cannot focus search");
            }
        }

        private void OnSearchCompleted(object sender, EventArgs e)
        {
            if (BindingContext is MainPageViewModel vm)
            {
                vm.ExecuteSearch();
                vm.ShowSuggestions = false;
                vm.SCH();
                
            }
        }

        void OnSearchEntryFocused(object sender, FocusEventArgs e)
        {
            if (BindingContext is MainPageViewModel vm)
                vm.ShowSuggestions = true;
        }

        void OnSearchEntryUnfocused(object sender, FocusEventArgs e)
        {
            if (BindingContext is MainPageViewModel vm)
                vm.ShowSuggestions = false;
        }
    }
}
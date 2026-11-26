namespace ModernMozart.App;

public partial class AboutPage : ContentPage
{
    public AboutPage() => InitializeComponent();

    private async void OnClose(object? sender, EventArgs e) => await Navigation.PopModalAsync();
}

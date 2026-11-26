namespace ModernMozart.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    // Take the container, not MainPage: injecting the page would construct it (and its
    // StaticResource lookups) before InitializeComponent has loaded App.xaml's resources.
    public App(IServiceProvider services)
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Dark;
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new(_services.GetRequiredService<MainPage>()) { Title = "Mozart Dice Waltz" };
}

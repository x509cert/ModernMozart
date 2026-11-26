using System.ComponentModel;
using ModernMozart.App.ViewModels;

namespace ModernMozart.App;

public partial class MainPage : ContentPage
{
    private const string FrameAnimation = "playhead";

    private readonly MainViewModel _vm;
    private int _followedGroup = -1;

    public MainPage(MainViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += OnViewModelChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();
    }

    private async void OnHelp(object? sender, EventArgs e) => await Navigation.PushModalAsync(new AboutPage());

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // Drive the playhead from the platform's frame ticker (vsync on Android), so it
            // moves every frame instead of whenever the audio device reports a position.
            case nameof(MainViewModel.IsPlaying) when _vm.IsPlaying:
                this.Animate(FrameAnimation, _ => _vm.UpdatePlayhead(), rate: 16, length: 1000,
                             repeat: () => _vm.IsPlaying);
                break;
            case nameof(MainViewModel.IsPlaying):
                this.AbortAnimation(FrameAnimation);
                _followedGroup = -1;
                break;

            case nameof(MainViewModel.ScorePlayhead):
                Follow(_vm.ScorePlayhead);
                break;
        }
    }

    /// <summary>Keep the four-bar group being played (dice + staff) fully on screen.</summary>
    private void Follow(double playhead)
    {
        if (playhead < 0) return;
        int group = (int)playhead / BarGroup.Size;
        if (group == _followedGroup) return;
        _followedGroup = group;

        if (GroupsHost.Children.ElementAtOrDefault(group) is not VisualElement view) return;

        const double margin = 12;
        double top = OffsetWithin(view, Scroller), bottom = top + view.Height;
        bool visible = top - margin >= Scroller.ScrollY && bottom + margin <= Scroller.ScrollY + Scroller.Height;
        if (!visible)
            _ = Scroller.ScrollToAsync(0, Math.Max(0, top - margin), animated: true);
    }

    /// <summary>Y of <paramref name="element"/> within the scroll view's content.</summary>
    private static double OffsetWithin(VisualElement element, Element ancestor)
    {
        double y = 0;
        for (Element? e = element; e is not null && e != ancestor; e = e.Parent)
            if (e is VisualElement v) y += v.Y;
        return y;
    }
}

using System.ComponentModel;
using ModernMozart.App.ViewModels;

namespace ModernMozart.App;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _vm;
    private double _followedSystemTop = double.NaN;

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

    /// <summary>Keep the line of music being played on screen, scrolling once per new line.</summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.ScorePlayhead)) return;

        double playhead = _vm.ScorePlayhead;
        if (playhead < 0) { _followedSystemTop = double.NaN; return; }

        double top = OffsetWithin(Score, Scroller) + Score.SystemTop((int)playhead);
        if (top == _followedSystemTop) return;           // still on the same line
        _followedSystemTop = top;

        double margin = 12, bottom = top + Score.SystemHeight;
        bool visible = top - margin >= Scroller.ScrollY && bottom <= Scroller.ScrollY + Scroller.Height;
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

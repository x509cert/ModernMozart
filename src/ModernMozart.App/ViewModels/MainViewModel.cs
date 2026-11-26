using System.Collections.ObjectModel;
using System.Windows.Input;
using ModernMozart.App.Audio;
using ModernMozart.Core;

namespace ModernMozart.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly SoundFontStore _soundFonts;
    private readonly Command _rollCommand, _playCommand, _rerollCommand, _shareCommand, _loadSfCommand, _removeSfCommand;

    private WaltzRoll _roll = DiceTable.Roll(Random.Shared);
    private int  _tempo = 138;
    private bool _observeRepeats = true;
    private bool _isPlaying, _isBusy, _initialized;
    private IWaltzPlayer _player = new SystemSynthPlayer();
    private bool _hasImportedSoundFont;
    private CancellationTokenSource? _playCts;
    private PerformancePlan? _plan;
    private int  _currentSlot = -1;
    private MeasureCell? _currentCell;
    private string _nowPlayingTitle = "Ready", _nowPlayingDetail = "Tap Play — or tap any bar to re-throw it.";
    private string _timeLabel = "";
    private RollEntry? _currentEntry;
    private double _scorePlayhead = -1;
    private double _progress;

    public MainViewModel(SoundFontStore soundFonts)
    {
        _soundFonts = soundFonts;

        _rollCommand     = new Command(NewRoll,                              () => !_isPlaying);
        _playCommand     = new Command(async () => await TogglePlayAsync(),  () => !_isBusy);
        _rerollCommand   = new Command<MeasureCell>(Reroll,                  _ => !_isPlaying);
        _shareCommand    = new Command(async () => await ShareMidiAsync(),   () => !_isBusy);
        _loadSfCommand   = new Command(async () => await ImportSoundFontAsync(), () => !_isPlaying && !_isBusy);
        _removeSfCommand = new Command(async () => await RemoveSoundFontAsync(), () => !_isPlaying && !_isBusy);

        Rebuild();
    }

    // ── Bindable state ────────────────────────────────────────────────────
    public ObservableCollection<MeasureCell> SectionA { get; } = [];
    public ObservableCollection<MeasureCell> SectionB { get; } = [];

    public ICommand RollCommand => _rollCommand;
    public ICommand PlayCommand => _playCommand;
    public ICommand RerollCommand => _rerollCommand;
    public ICommand ShareMidiCommand => _shareCommand;
    public ICommand LoadSoundFontCommand => _loadSfCommand;
    public ICommand RemoveSoundFontCommand => _removeSfCommand;

    /// <summary>The sixteen measure numbers, for the score. A new array whenever the roll changes.</summary>
    public IReadOnlyList<int> ScoreMeasures => [.. _roll.Measures];

    public int Tempo
    {
        get => _tempo;
        set { if (Set(ref _tempo, Math.Clamp(value, 60, 200))) Raise(nameof(TempoSlider), nameof(TempoLabel), nameof(LengthLabel)); }
    }
    /// <summary>Slider-friendly view of <see cref="Tempo"/>.</summary>
    public double TempoSlider
    {
        get => _tempo;
        set => Tempo = (int)Math.Round(value);
    }
    public string TempoLabel => $"♩ = {_tempo}";

    public bool ObserveRepeats
    {
        get => _observeRepeats;
        set { if (Set(ref _observeRepeats, value)) Raise(nameof(LengthLabel)); }
    }

    public string LengthLabel => Format(PerformancePlan.Create(_roll, _tempo, Passes).Length);

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (!Set(ref _isPlaying, value)) return;
            Raise(nameof(PlayButtonText), nameof(IsIdle));
            RefreshCommands();
        }
    }
    public bool   IsIdle         => !_isPlaying;
    public string PlayButtonText => _isPlaying ? "■  Stop" : "▶  Play";

    public string EngineName           => _player.Name;
    public bool   HasImportedSoundFont => _hasImportedSoundFont;
    public bool   UsingBuiltInSynth    => _player is SystemSynthPlayer;

    public string NowPlayingTitle  { get => _nowPlayingTitle;  private set => Set(ref _nowPlayingTitle, value); }
    public string NowPlayingDetail { get => _nowPlayingDetail; private set => Set(ref _nowPlayingDetail, value); }
    public string TimeLabel        { get => _timeLabel;        private set => Set(ref _timeLabel, value); }
    public double Progress         { get => _progress;         private set => Set(ref _progress, value); }

    /// <summary>Score position: bar index (0–15) + fraction through the bar; -1 when stopped.</summary>
    public double ScorePlayhead    { get => _scorePlayhead;    private set => Set(ref _scorePlayhead, value); }

    /// <summary>The throw behind the bar being shown in the now-playing card, if any.</summary>
    public bool HasCurrentDice => _currentEntry is not null;
    public int  CurrentDie1    => _currentEntry?.Die1 ?? 1;
    public int  CurrentDie2    => _currentEntry?.Die2 ?? 1;

    private void ShowDice(RollEntry? entry)
    {
        _currentEntry = entry;
        Raise(nameof(HasCurrentDice), nameof(CurrentDie1), nameof(CurrentDie2));
    }

    private int Passes => _observeRepeats ? 2 : 1;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await RunBusyAsync(async () =>
        {
            NowPlayingDetail = "Loading piano…";
            UseSoundFont(await _soundFonts.LoadAsync());
            NowPlayingDetail = "Tap Play — or tap any bar to re-throw it.";
        });
    }

    // ── Dice ──────────────────────────────────────────────────────────────
    private void NewRoll()
    {
        _roll = DiceTable.Roll(Random.Shared);
        Rebuild();
    }

    private void Reroll(MeasureCell? cell)
    {
        if (cell is null || _isPlaying) return;
        _roll = _roll.Reroll(cell.Position, Random.Shared);
        cell.Entry = _roll.Entries[cell.Position - 1];
        Raise(nameof(ScoreMeasures));
        NowPlayingTitle  = $"Bar {cell.Measure}";
        NowPlayingDetail = $"Position {cell.Position} re-thrown — rolled {cell.Entry.Sum}";
        ShowDice(cell.Entry);
    }

    private void Rebuild()
    {
        SectionA.Clear(); SectionB.Clear();
        foreach (var e in _roll.Entries)
            (e.Section == 'A' ? SectionA : SectionB).Add(new MeasureCell(e));
        Raise(nameof(ScoreMeasures), nameof(LengthLabel));
        ResetNowPlaying();
    }

    // ── Playback ──────────────────────────────────────────────────────────
    private async Task TogglePlayAsync()
    {
        if (_isPlaying) { _playCts?.Cancel(); return; }

        _plan    = PerformancePlan.Create(_roll, _tempo, Passes);
        _playCts = new CancellationTokenSource();
        IsPlaying = true;
        DeviceDisplay.Current.KeepScreenOn = true;

        try
        {
            var plan = _plan;
            await _player.PlayAsync(plan, t => MainThread.BeginInvokeOnMainThread(() => ShowPosition(plan, t)), _playCts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await Alert("Playback failed", ex.Message);
        }
        finally
        {
            DeviceDisplay.Current.KeepScreenOn = false;
            _playCts.Dispose();
            _playCts = null;
            IsPlaying = false;
            ResetNowPlaying();
        }
    }

    private void ShowPosition(PerformancePlan plan, TimeSpan t)
    {
        if (!_isPlaying || !ReferenceEquals(plan, _plan)) return;   // stale callback

        Progress  = Math.Clamp(t / plan.Length, 0, 1);
        TimeLabel = $"{Format(t < plan.Length ? t : plan.Length)} / {Format(plan.Length)}";

        int slotIndex = plan.SlotAt(t);
        var slot = plan.Slots[slotIndex];
        var e    = slot.Entry;

        // The score shows the 16 written bars; repeats map back onto them.
        double into = (t - plan.MeasureLength * slotIndex) / plan.MeasureLength;
        ScorePlayhead = e.Position - 1 + Math.Clamp(into, 0, 0.999);

        if (slotIndex == _currentSlot) return;
        _currentSlot = slotIndex;
        Highlight((e.Section == 'A' ? SectionA : SectionB)[(e.Position - 1) % WaltzRoll.HalfLength]);

        string pass = plan.Passes > 1 ? $" · pass {slot.Pass}" : "";
        NowPlayingTitle  = $"Bar {e.Measure}";
        NowPlayingDetail = $"Section {e.Section}{pass} · position {e.Position} · rolled {e.Sum}";
        ShowDice(e);
    }

    private void Highlight(MeasureCell? cell)
    {
        if (_currentCell is not null) _currentCell.IsCurrent = false;
        _currentCell = cell;
        if (cell is not null) cell.IsCurrent = true;
    }

    private void ResetNowPlaying()
    {
        Highlight(null);
        _currentSlot = -1;
        ScorePlayhead = -1;
        Progress = 0;
        TimeLabel = "";
        NowPlayingTitle = "Ready";
        ShowDice(null);
        NowPlayingDetail = "Tap Play — or tap any bar to re-throw it.";
    }

    // ── Export ────────────────────────────────────────────────────────────
    private async Task ShareMidiAsync()
    {
        try
        {
            var plan = PerformancePlan.Create(_roll, _tempo, Passes);
            string path = Path.Combine(FileSystem.CacheDirectory, $"mozart-waltz-{DateTime.Now:yyyyMMdd-HHmmss}.mid");
            await File.WriteAllBytesAsync(path, MidiFileWriter.Build(plan));
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Mozart dice waltz",
                File  = new ShareFile(path, "audio/midi"),
            });
        }
        catch (Exception ex) { await Alert("Couldn't share", ex.Message); }
    }

    // ── SoundFonts ────────────────────────────────────────────────────────
    private Task ImportSoundFontAsync() => RunBusyAsync(async () =>
    {
        try
        {
            NowPlayingDetail = "Loading SoundFont…";
            var loaded = await _soundFonts.ImportAsync();
            if (loaded is not null) UseSoundFont(loaded);
        }
        catch (Exception ex) { await Alert("SoundFont", ex.Message); }
        finally { NowPlayingDetail = "Tap Play — or tap any bar to re-throw it."; }
    });

    private Task RemoveSoundFontAsync() => RunBusyAsync(async () =>
    {
        _soundFonts.RemoveImported();
        UseSoundFont(await _soundFonts.LoadAsync());   // falls back to bundled, then built-in
    });

    private void UseSoundFont(LoadedSoundFont? sf)
    {
        _player = sf is null ? new SystemSynthPlayer() : new SoundFontPlayer(sf.SoundFont, sf.Name);
        _hasImportedSoundFont = sf?.IsImported == true;
        Raise(nameof(EngineName), nameof(HasImportedSoundFont), nameof(UsingBuiltInSynth));
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private async Task RunBusyAsync(Func<Task> work)
    {
        _isBusy = true; RefreshCommands();
        try     { await work(); }
        finally { _isBusy = false; RefreshCommands(); }
    }

    private void RefreshCommands()
    {
        foreach (var c in (Command[])[_rollCommand, _playCommand, _rerollCommand, _shareCommand, _loadSfCommand, _removeSfCommand])
            c.ChangeCanExecute();
    }

    private static Task Alert(string title, string message)
        => Application.Current?.Windows.FirstOrDefault()?.Page?.DisplayAlertAsync(title, message, "OK")
           ?? Task.CompletedTask;

    private static string Format(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";
}

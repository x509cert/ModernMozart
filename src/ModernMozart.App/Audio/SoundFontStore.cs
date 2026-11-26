using MeltySynth;
using ModernMozart.Core;

namespace ModernMozart.App.Audio;

public enum PianoSource { Bundled, Imported, BuiltIn }

/// <summary>One entry in the piano dropdown.</summary>
/// <param name="Id">Stable key saved in preferences.</param>
/// <param name="Asset">Bundled asset file name (Bundled only).</param>
public sealed record PianoChoice(string Id, string Name, PianoSource Source, string? Asset = null)
{
    public override string ToString() => Name;   // Picker fallback display
}

/// <summary>
/// The pianos the app can play with:
///   • every SoundFont bundled in Resources/Raw (packaged under assets/soundfonts/)
///   • one the user imported (stored in app data, survives restarts)
///   • Android's built-in synth, always last
/// and which one the user picked. MeltySynth needs a seekable file, so bundled
/// SoundFonts are copied out of the APK (once per app build) before loading.
/// </summary>
public sealed class SoundFontStore
{
    private const string AssetFolder  = "soundfonts";
    private const string DefaultAsset = "Piano.sf2";
    private const string SelectedKey  = "piano.selected";
    private const string NameKey      = "soundfont.name";
    private const string ImportedId   = "imported";

    public static readonly PianoChoice BuiltIn = new("builtin", "Android built-in synth", PianoSource.BuiltIn);

    private static string ImportedPath => Path.Combine(FileSystem.AppDataDirectory, "imported.sf2");

    private static string CachePath(string asset) =>
        Path.Combine(FileSystem.CacheDirectory, $"bundled-{AppInfo.Current.BuildString}-{asset}");

    // ── Choices ───────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<PianoChoice>> ListAsync()
    {
        var choices = new List<PianoChoice>();

        foreach (string asset in BundledAssets().Order(StringComparer.OrdinalIgnoreCase))
        {
            string? name = null;
            try
            {
                await using var s = await FileSystem.OpenAppPackageFileAsync($"{AssetFolder}/{asset}");
                name = SoundFontHeader.ReadName(s);
            }
            catch (Exception ex) { Console.WriteLine($"ModernMozart: can't read {asset}: {ex.Message}"); }
            choices.Add(new($"bundled:{asset}", name ?? Path.GetFileNameWithoutExtension(asset), PianoSource.Bundled, asset));
        }

        if (File.Exists(ImportedPath))
            choices.Add(new(ImportedId, Preferences.Default.Get(NameKey, "Imported SoundFont"), PianoSource.Imported));

        choices.Add(BuiltIn);
        Console.WriteLine($"ModernMozart: pianos: {string.Join(" | ", choices.Select(c => c.Name))}");
        return choices;
    }

    /// <summary>The saved choice if still available; otherwise the default bundled piano.</summary>
    public PianoChoice Preferred(IReadOnlyList<PianoChoice> choices)
    {
        string saved = Preferences.Default.Get(SelectedKey, "");
        return choices.FirstOrDefault(c => c.Id == saved)
            ?? choices.FirstOrDefault(c => c.Asset is { } a && a.Equals(DefaultAsset, StringComparison.OrdinalIgnoreCase))
            ?? choices.FirstOrDefault(c => c.Source == PianoSource.Bundled)
            ?? BuiltIn;
    }

    public void Remember(PianoChoice choice) => Preferences.Default.Set(SelectedKey, choice.Id);

    // ── Loading ───────────────────────────────────────────────────────────
    /// <summary>Loads the choice's SoundFont; null for the built-in synth.</summary>
    public async Task<SoundFont?> LoadAsync(PianoChoice choice)
    {
        switch (choice.Source)
        {
            case PianoSource.Imported:
                return await Task.Run(() => new SoundFont(ImportedPath));

            case PianoSource.Bundled:
                string asset = choice.Asset!, path = CachePath(asset);
                if (!File.Exists(path))
                {
                    foreach (var stale in Directory.EnumerateFiles(FileSystem.CacheDirectory, $"bundled-*-{asset}"))
                        File.Delete(stale);

                    string tmp = path + ".tmp";
                    await using (var src = await FileSystem.OpenAppPackageFileAsync($"{AssetFolder}/{asset}"))
                    await using (var dst = File.Create(tmp))
                        await src.CopyToAsync(dst);
                    File.Move(tmp, path, overwrite: true);
                }
                var sf = await Task.Run(() => new SoundFont(path));
                Console.WriteLine($"ModernMozart: SoundFont loaded (bundled): {choice.Name}");
                return sf;

            default:
                return null;
        }
    }

    // ── Import / remove ───────────────────────────────────────────────────
    /// <summary>Shows the system file picker and keeps a copy. Returns null if the user cancels.</summary>
    public async Task<PianoChoice?> ImportAsync()
    {
        var picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose a SoundFont (.sf2)" });
        if (picked is null) return null;

        string tmp = ImportedPath + ".tmp";
        try
        {
            await using (var src = await picked.OpenReadAsync())
            await using (var dst = File.Create(tmp))
                await src.CopyToAsync(dst);

            var sf = await Task.Run(() => new SoundFont(tmp));                // parse = validate
            string name = string.IsNullOrWhiteSpace(sf.Info.BankName)
                ? Path.GetFileNameWithoutExtension(picked.FileName)
                : sf.Info.BankName.Trim();

            File.Move(tmp, ImportedPath, overwrite: true);
            Preferences.Default.Set(NameKey, name);
            return new(ImportedId, name, PianoSource.Imported);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            File.Delete(tmp);
            throw new InvalidDataException($"“{picked.FileName}” isn't a usable SoundFont 2 file.", ex);
        }
    }

    public void RemoveImported()
    {
        File.Delete(ImportedPath);
        Preferences.Default.Remove(NameKey);
    }

    // ── Bundled assets ────────────────────────────────────────────────────
    private static IEnumerable<string> BundledAssets()
    {
#if ANDROID
        return (Android.App.Application.Context.Assets?.List(AssetFolder) ?? [])
            .Where(f => f.EndsWith(".sf2", StringComparison.OrdinalIgnoreCase));
#else
        return [];
#endif
    }
}

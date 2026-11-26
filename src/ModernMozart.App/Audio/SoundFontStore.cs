using MeltySynth;

namespace ModernMozart.App.Audio;

public sealed record LoadedSoundFont(SoundFont SoundFont, string Name, bool IsImported);

/// <summary>
/// Finds a SoundFont for the piano, in priority order:
///   1. one the user imported (stored in app data, survives restarts)
///   2. the default bundled with the app (the .sf2 in Resources/Raw, packaged as piano.sf2)
/// MeltySynth needs a seekable stream, so everything is loaded from a real file.
/// </summary>
public sealed class SoundFontStore
{
    private const string BundledAsset = "piano.sf2";
    private const string NameKey      = "soundfont.name";

    private static string ImportedPath => Path.Combine(FileSystem.AppDataDirectory, "imported.sf2");

    // MeltySynth needs a seekable file, so the bundled asset is copied out once per app build
    // (a new build may bundle a different SoundFont).
    private static string BundledCache =>
        Path.Combine(FileSystem.CacheDirectory, $"bundled-{AppInfo.Current.BuildString}.sf2");

    public async Task<LoadedSoundFont?> LoadAsync()
    {
        if (File.Exists(ImportedPath))
        {
            try
            {
                return await Task.Run(() => Load(ImportedPath, Preferences.Default.Get(NameKey, "Imported"), isImported: true));
            }
            catch (Exception) { File.Delete(ImportedPath); }   // corrupt — forget it
        }

        try
        {
            if (!File.Exists(BundledCache))
            {
                foreach (var stale in Directory.EnumerateFiles(FileSystem.CacheDirectory, "bundled-*.sf2"))
                    File.Delete(stale);

                string tmp = BundledCache + ".tmp";
                await using (var src = await FileSystem.OpenAppPackageFileAsync(BundledAsset))
                await using (var dst = File.Create(tmp))
                    await src.CopyToAsync(dst);
                File.Move(tmp, BundledCache, overwrite: true);
            }
            var bundled = await Task.Run(() => Load(BundledCache, "Bundled piano", isImported: false));
            Console.WriteLine($"ModernMozart: SoundFont loaded (bundled): {bundled.Name}");
            return bundled;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ModernMozart: no bundled SoundFont ({ex.GetType().Name}); using built-in synth");
            return null;
        }
    }

    /// <summary>Shows the system file picker. Returns null if the user cancels.</summary>
    public async Task<LoadedSoundFont?> ImportAsync()
    {
        var picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose a SoundFont (.sf2)" });
        if (picked is null) return null;

        string tmp = ImportedPath + ".tmp";
        try
        {
            await using (var src = await picked.OpenReadAsync())
            await using (var dst = File.Create(tmp))
                await src.CopyToAsync(dst);

            string fallback = Path.GetFileNameWithoutExtension(picked.FileName);
            var loaded = await Task.Run(() => Load(tmp, fallback, isImported: true));   // parse = validate

            File.Move(tmp, ImportedPath, overwrite: true);
            Preferences.Default.Set(NameKey, loaded.Name);
            return loaded;
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

    private static LoadedSoundFont Load(string path, string fallbackName, bool isImported)
    {
        var sf   = new SoundFont(path);   // loads all samples into memory
        var name = string.IsNullOrWhiteSpace(sf.Info.BankName) ? fallbackName : sf.Info.BankName.Trim();
        return new(sf, name, isImported);
    }
}

# ModernMozart

Mozart's *Musikalisches Würfelspiel* (K. 516f, 1787) for Android — roll 2d6 sixteen times,
pick sixteen pre-composed bars from Mozart's table, and hear a waltz nobody has heard before
(11¹⁶ ≈ 4.6 × 10¹⁶ possibilities).

.NET 10 · C# 14 · .NET MAUI (Android) · MeltySynth

**Install:** [download the latest APK](https://github.com/x509cert/ModernMozart/releases/latest/download/ModernMozart.apk) — see [Installing on Android](#installing-on-android).

## Features

- Four rows of four bars, each with its dice shown as faces and its line of score engraved underneath; tap any bar to re-throw just that position
- Score: piano grand staff (Bravura glyphs) with Mozart's table number above each bar
- During playback the current bar lights up and a playhead sweeps through it every display frame, following what you **hear** (the audio device's playback head, smoothed by `PlaybackClock`); the page scrolls along
- ⓘ About page: the app, the dice game, and how the staves are engraved
- Tempo 60–200, repeats on/off (AABB)
- Share the performance as a Standard MIDI File
- Two piano engines:

| Engine | Quality | Setup |
|---|---|---|
| **SoundFont** — [MeltySynth](https://github.com/sinshu/meltysynth) renders on-device → 32-bit float `AudioTrack` | As good as the `.sf2` | Every `.sf2` in `Resources/Raw` is bundled and listed in the app's **piano dropdown** (default: *Upright Piano KW*, [FreePats](https://freepats.zenvoid.org/Piano/acoustic-grand-piano.html), CC0). **Load SoundFont…** adds your own |
| **Built-in** — Android's Sonivox EAS GM synth via `MediaPlayer` | Dated, but works everywhere | Last entry in the dropdown; also the fallback if a SoundFont won't load |

The app comes bundled with a couple of SoundFont files (.SF2); you can choose one from the drop down at the bottom of the app. You can also add your own if you wish and load it into the app. 

## Installing on Android

Android 7.0 or later. The app isn't on the Play Store, so you install the APK directly ("sideloading").
**You do not need Developer mode for this** — that's only for deploying from a PC over USB (below).

### Install the APK (recommended)

1. On the phone, open this link in Chrome:
   **https://github.com/x509cert/ModernMozart/releases/latest/download/ModernMozart.apk**
2. When the download finishes, tap **Open** (or open it from **Files → Downloads**).
3. The first time, Android blocks it: *"For your security, your phone is not allowed to install unknown apps from this source."*
   Tap **Settings**, turn on **Allow from this source** for Chrome, then go back.
4. Tap **Install**. If Google Play Protect warns that the app is unrecognised, tap **More details → Install anyway**.
5. Open **Mozart Dice Waltz** from your app drawer.

The setting is called **Install unknown apps** and lives under
**Settings → Apps → Special app access → Install unknown apps** (Samsung: **Settings → Apps → ⋮ → Special access**).
You can switch it back off for Chrome after installing.

**Updating:** open the same link again — it always points at the newest build — and tap **Install**. Your settings and any
loaded SoundFont are kept. If Android says *"App not installed as package conflicts with an existing package"*, the
installed copy was signed with a different key (early builds were); uninstall it once, then install.

**More pianos:** see [Pianos (SoundFonts)](#pianos-soundfonts) to add the brighter upright or another piano.

### Install from a PC over USB (Developer mode, NOT recommended unless you currently do Android development)

Only needed if you're building the app yourself.

**1. Turn on Developer options**

1. Open **Settings → About phone**.
   - Pixel / stock Android: **Settings → About phone → Build number**
   - Samsung: **Settings → About phone → Software information → Build number**
   - Xiaomi: **Settings → About phone → MIUI / HyperOS version**
2. Tap **Build number** seven times. Enter your PIN if asked. You'll see *"You are now a developer!"*

**2. Turn on USB debugging**

1. Open **Settings → System → Developer options** (Samsung: **Settings → Developer options**, at the bottom).
2. Turn on **USB debugging**.
3. Connect the phone to the PC with a USB cable. On the phone, accept **Allow USB debugging?** for this computer
   (tick *Always allow* to skip it next time).
4. Check the PC sees it: `adb devices` should list the phone as `device` (not `unauthorized`).

**3. Deploy**

```bash
dotnet workload install maui-android
dotnet build src/ModernMozart.App -t:Run -f net10.0-android
```

Or install a downloaded APK with `adb install -r ModernMozart.apk`.

When you're done, you can turn **USB debugging** (or all of **Developer options**) off again in the same menu.

## Pianos (SoundFonts)

The piano is played from SoundFont (`.sf2`) files: recordings of a real piano, note by note, rendered on the phone.
The app ships with **Upright Piano KW (small)**, and you pick between pianos in the **PIANO** dropdown.

Upright Piano KW is a Kawai upright recorded by Gonzalo and Roberto, published by
[FreePats](https://freepats.zenvoid.org/Piano/acoustic-grand-piano.html) under CC0 (public domain). It comes in three SF2 banks:

| Bank | Download (`.7z`) | Size of the `.sf2` | |
|---|---|---|---|
| Small | [UprightPianoKW-small-SF2-20190703.7z](https://freepats.zenvoid.org/Piano/UprightPianoKW/UprightPianoKW-small-SF2-20190703.7z) | 10 MB | bundled as `Piano.sf2` |
| Small bright | [UprightPianoKW-small-bright-SF2-20190703.7z](https://freepats.zenvoid.org/Piano/UprightPianoKW/UprightPianoKW-small-bright-SF2-20190703.7z) | 10 MB | a brighter tone |
| Full | [UprightPianoKW-SF2-20220221.7z](https://freepats.zenvoid.org/Piano/UprightPianoKW/UprightPianoKW-SF2-20220221.7z) | 56 MB | best quality, slower to load |

Each download is a `.7z` archive with the `.sf2` inside. Any other piano SoundFont works too; the app plays bank 0, program 0.

### Add a piano on your phone

1. On the phone, tap one of the download links above in Chrome.
2. Extract the `.7z`. If your phone's **Files** app can't open it, install an archive app that handles 7z, such as
   **ZArchiver** or **RAR** (by RARLAB), from the Play Store. Open the downloaded `.7z` in it and extract it to **Download**.
3. In Mozart Dice Waltz, scroll to **PIANO** and tap **Load SoundFont…**, then pick the extracted `.sf2`.
   It's added to the dropdown and selected.

You can keep one loaded piano at a time: loading another replaces it, and **Remove** deletes it.
The bundled pianos are always in the dropdown.

### Bundle a piano in the app

To make a piano part of every build, upload the extracted `.sf2` to
[`src/ModernMozart.App/Resources/Raw/`](src/ModernMozart.App/Resources/Raw) (GitHub's **Add file → Upload files** works from a
phone's browser too), under any name, e.g. `Piano-Bright.sf2`. The next build lists it in the dropdown by its own name.
Every bundled `.sf2` adds its size to the APK. `Piano.sf2` is the default for new installs.

## Layout

```
src/ModernMozart.Core     platform-neutral: dice table, 176 measures, performance plan,
                          MIDI writer, sample-accurate SoundFont renderer,
                          Notation/ score engraver (draws through a 4-method canvas interface)
src/ModernMozart.App      .NET MAUI Android app (MVVM, compiled bindings)
tests/ModernMozart.Core.Tests
```

`PerformancePlan` is the single source of truth: both the live renderer and the MIDI writer
consume the same sorted event list (note-offs before note-ons on the same tick, so repeated notes re-strike).
`WaltzRenderer` splits each render call at event boundaries, so timing doesn't depend on the audio buffer size.

## Build

```bash
dotnet workload install maui-android
dotnet test  tests/ModernMozart.Core.Tests
dotnet build src/ModernMozart.App -t:Run -f net10.0-android          # deploy to a connected phone
dotnet publish src/ModernMozart.App -c Release -f net10.0-android    # sideloadable *-Signed.apk
```

CI (`.github/workflows/build.yml`) runs the tests, builds the APK, launches it on an Android emulator (renders,
opens the About page, survives Play), and only then publishes it as a release on `main`.

Release signing uses the `KEYSTORE_B64` (base64 PKCS#12, alias `modernmozart`) and `KEYSTORE_PASSWORD` repo secrets; without them CI signs with a throwaway debug key and builds won't install over each other.


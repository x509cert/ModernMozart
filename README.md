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
| **SoundFont** — [MeltySynth](https://github.com/sinshu/meltysynth) renders on-device → 32-bit float `AudioTrack` | As good as the `.sf2` you give it | Bundle `Resources/Raw/piano.sf2`, or tap **Load SoundFont…** |
| **Built-in** — Android's Sonivox EAS GM synth via `MediaPlayer` | Dated, but works everywhere | None (automatic fallback) |

For the most authentic sound, use a sampled grand. Mobile-friendly sizes (≈20–150 MB) load fastest;
the whole SoundFont is held in memory.

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

**Better piano sound:** in the app, tap **Load SoundFont…** and pick a piano `.sf2` file you've downloaded to the phone.

### Install from a PC over USB (Developer mode< NOT recommended unless you currently do Android development)

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


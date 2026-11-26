# ModernMozart

Mozart's *Musikalisches Würfelspiel* (K. 516f, 1787) for Android — roll 2d6 sixteen times,
pick sixteen pre-composed bars from Mozart's table, and hear a waltz nobody has heard before
(11¹⁶ ≈ 4.6 × 10¹⁶ possibilities).

.NET 10 · C# 14 · .NET MAUI (Android) · MeltySynth

## Features

- 16-bar grid (parts A and B) with the dice shown as faces; tap any bar to re-throw just that position
- The roll engraved as a piano score (grand staff, Bravura glyphs), Mozart's table numbers above each bar; during playback the current bar lights up, a playhead sweeps through it in time with the audio, and the page follows along
- Live highlight of the bar you're **hearing** (driven by the audio device's playback head, not the renderer)
- Tempo 60–200, repeats on/off (AABB)
- Share the performance as a Standard MIDI File
- Two piano engines:

| Engine | Quality | Setup |
|---|---|---|
| **SoundFont** — [MeltySynth](https://github.com/sinshu/meltysynth) renders on-device → 32-bit float `AudioTrack` | As good as the `.sf2` you give it | Bundle `Resources/Raw/piano.sf2`, or tap **Load SoundFont…** |
| **Built-in** — Android's Sonivox EAS GM synth via `MediaPlayer` | Dated, but works everywhere | None (automatic fallback) |

For the most authentic sound, use a sampled grand. Mobile-friendly sizes (≈20–150 MB) load fastest;
the whole SoundFont is held in memory.

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

CI (`.github/workflows/build.yml`) runs the tests, builds the APK, and on `main` publishes it as a release.

**Install on a phone:** open https://github.com/x509cert/ModernMozart/releases/latest/download/ModernMozart.apk in Chrome → Open → Install.

Release signing uses the `KEYSTORE_B64` (base64 PKCS#12, alias `modernmozart`) and `KEYSTORE_PASSWORD` repo secrets; without them CI signs with a throwaway debug key and builds won't install over each other.

Min Android: 7.0 (API 24).

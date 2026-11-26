# Bundling a piano

Put a General MIDI or piano-only SoundFont here named **`piano.sf2`** and it ships inside the APK.
The app loads it at startup (program 0, bank 0 = Acoustic Grand Piano).

Anything in this folder except `*.md` is packaged. `*.sf2` is git-ignored — SoundFonts are large
and many carry their own licences. Users can also import one at runtime via **Load SoundFont…**.

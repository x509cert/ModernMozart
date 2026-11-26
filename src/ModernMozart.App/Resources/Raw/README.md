# The default piano

The single `.sf2` file in this folder is the app's default piano. Whatever it's called, it is packaged
inside the APK as `piano.sf2` and loaded at startup (bank 0, program 0 = the piano).

Currently: **Piano.sf2** — *Upright Piano KW* (small), a Kawai upright recorded by Gonzalo and Roberto,
from [FreePats](https://freepats.zenvoid.org/Piano/acoustic-grand-piano.html), CC0 1.0 (public domain).

To change it, replace the file — keep exactly one `.sf2` here, or the build fails. A SoundFont the
user loads in the app (**Load SoundFont…**) takes priority; **Remove** returns to this one.

# Orbital Frontier audio

Original audio for this project, baked by `tools/bake_arena_audio.py` into editable
22,050 Hz mono, 16-bit PCM WAV files. No external sample packs or synthesizer
plugins are needed. These generated audio files are dedicated to the public domain
under CC0 1.0 Universal; see `LICENSE.txt` in this directory.

- `orbital_ambient`: a quiet 24-second four-chord loop.
- `engine`: a seamless two-second engine bed, pitched gently with flight speed.
- `cannon`, `impact`, `destruction`, `boost_start`: short flight/combat cues.
- `warp_charge`, `warp_arrival`: three-second charging and arrival cues.

The two looping files contain WAV `smpl` loop metadata; their Godot imports also
store the forward-loop setting. `scenes/arena/ArenaAudio.tscn` holds three fixed
players and eight effect voices. Change the saved streams or volumes there to
reuse the controller with another sound set. Playback observes server state;
sound effects never apply damage or trigger movement.

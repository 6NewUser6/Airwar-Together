# Flight audio

These editable mono PCM WAV assets preserve the original flight synthesizer's
22,050 Hz, 16-bit samples. The effects retain the original tone parameters and
the .NET `Random(123)` noise seed. The game loads these files directly instead
of synthesizing sound when it starts.

`flight_music.wav` contains the complete 16-second arrangement and a forward
loop in its WAV `smpl` metadata. Its committed import settings retain PCM samples
and explicitly loop all 352,800 frames, matching the former synthesizer. The six
effects are one-shots: `hit`, `collect`, `boost`, `pulse`, `near_miss`, `finished`.

`scenes/audio/FlightAudio.tscn` exposes the sound references and the music player
alongside eight reusable effect voices. Replace a sound reference in that scene
to reuse the playback controller with different audio.

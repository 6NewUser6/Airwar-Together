using Godot;
using System;
using Airwar.Core;

namespace Airwar.Audio;

public partial class FlightAudio : Node
{
    private AudioStreamPlayer _music = null!;
    private readonly AudioStreamPlayer[] _voices = new AudioStreamPlayer[8];
    private int _voice;
    private readonly AudioStreamWav[] _sounds = new AudioStreamWav[6];

    public override void _Ready()
    {
        _music = new AudioStreamPlayer { Stream = Music() }; AddChild(_music); _music.Play();
        for (int i = 0; i < _voices.Length; i++) { _voices[i] = new AudioStreamPlayer(); AddChild(_voices[i]); }
        _sounds[(int)FlightEventKind.Hit] = Tone(90, 25, .26, .65, true);
        _sounds[(int)FlightEventKind.Collect] = Tone(660, 1320, .2, .4, false);
        _sounds[(int)FlightEventKind.Boost] = Tone(180, 60, .28, .3, true);
        _sounds[(int)FlightEventKind.Pulse] = Tone(140, 480, .4, .45, false);
        _sounds[(int)FlightEventKind.NearMiss] = Tone(920, 680, .06, .1, false);
        _sounds[(int)FlightEventKind.Finished] = Tone(280, 70, .6, .4, false);
    }

    public void SetVolume(float music, float sound)
    {
        _music.VolumeDb = music > .001f ? Mathf.LinearToDb(music * .5f) : -80;
        foreach (var voice in _voices) voice.VolumeDb = sound > .001f ? Mathf.LinearToDb(sound) : -80;
    }

    public void Play(FlightEventKind kind)
    {
        var voice = _voices[_voice++ % _voices.Length]; voice.Stream = _sounds[(int)kind]; voice.Play();
    }

    private static AudioStreamWav Wave(byte[] data, bool loop = false)
    {
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = 22050, Stereo = false, Data = data,
            LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled, LoopBegin = 0, LoopEnd = data.Length / 2 };
    }

    private static AudioStreamWav Tone(double start, double end, double seconds, double volume, bool noise)
    {
        int length = (int)(22050 * seconds); byte[] bytes = new byte[length * 2];
        var random = new Random(123); double phase = 0;
        for (int i = 0; i < length; i++)
        {
            double t = (double)i / length;
            phase += Math.Tau * (start + (end - start) * t) / 22050;
            double envelope = Math.Min(1, t * 40) * Math.Pow(1 - t, 2);
            double value = (Math.Sin(phase) + (noise ? (random.NextDouble() * 2 - 1) * .4 : 0)) * envelope * volume;
            short sample = (short)(Math.Clamp(value, -1, 1) * 32767);
            bytes[i * 2] = (byte)sample; bytes[i * 2 + 1] = (byte)(sample >> 8);
        }
        return Wave(bytes);
    }

    private static AudioStreamWav Music()
    {
        const int length = 22050 * 16; byte[] bytes = new byte[length * 2];
        double[] notes = { 110, 130.8128, 164.8138, 146.8324, 110, 164.8138, 130.8128, 97.9989 };
        for (int i = 0; i < length; i++)
        {
            double time = i / 22050.0, beat = time * 2;
            double note = notes[(int)(beat / 4) % notes.Length];
            double envelope = Math.Exp(-(beat % 1) * 6);
            double bass = Math.Sin(Math.Tau * note * time) * .1;
            double arp = Math.Sin(Math.Tau * note * ((int)beat % 2 == 0 ? 4 : 6) * time) * .08 * envelope;
            double kick = Math.Sin(65 * Math.Tau * time) * Math.Exp(-(beat % 2) * 16) * .11;
            double pad = Math.Sin(Math.Tau * 55 * time) * .035;
            double fade = Math.Min(1, time * 3) * Math.Min(1, (16 - time) * 3);
            short sample = (short)((bass + arp + kick + pad) * fade * 32767);
            bytes[i * 2] = (byte)sample; bytes[i * 2 + 1] = (byte)(sample >> 8);
        }
        return Wave(bytes, true);
    }
}

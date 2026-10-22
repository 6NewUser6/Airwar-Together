using Godot;
using Airwar.Core;

namespace Airwar.Audio;

public partial class FlightAudio : Node
{
    [Export] public AudioStream HitSound { get; set; } = null!;
    [Export] public AudioStream CollectSound { get; set; } = null!;
    [Export] public AudioStream BoostSound { get; set; } = null!;
    [Export] public AudioStream PulseSound { get; set; } = null!;
    [Export] public AudioStream NearMissSound { get; set; } = null!;
    [Export] public AudioStream FinishedSound { get; set; } = null!;

    private AudioStreamPlayer _music = null!;
    private readonly AudioStreamPlayer[] _voices = new AudioStreamPlayer[8];
    private int _voice;
    private AudioStream[] _sounds = null!;
    private bool _stopped;

    public override void _Ready()
    {
        _music = GetNode<AudioStreamPlayer>("Music");
        for (int i = 0; i < _voices.Length; i++)
            _voices[i] = GetNode<AudioStreamPlayer>($"Voices/Voice{i + 1:00}");
        _sounds = new[] { HitSound, CollectSound, BoostSound, PulseSound, NearMissSound, FinishedSound };
        // Defer until the scene has finished entering the tree. A command-line
        // check can quit from Main.Ready before the audio server has started.
        Callable.From(StartMusic).CallDeferred();
    }

    private void StartMusic()
    {
        if (!_stopped && IsInsideTree()) _music.Play();
    }

    public override void _ExitTree()
    {
        StopAll();
    }

    public void StopAll()
    {
        if (_stopped) return;
        _stopped = true;
        // Call before Quit and allow the audio server a mixing frame to release
        // playback; _ExitTree remains an idempotent fallback for scene removal.
        StopAndRelease(_music);
        foreach (var voice in _voices) StopAndRelease(voice);
        System.Array.Clear(_voices);
        _sounds = System.Array.Empty<AudioStream>();
        _music = null!;
        HitSound = CollectSound = BoostSound = PulseSound = NearMissSound = FinishedSound = null!;
    }

    public async System.Threading.Tasks.Task StopAndDrainAsync()
    {
        StopAll();
        if (!IsInsideTree()) return;
        var tree = GetTree();
        ulong deadline = Time.GetTicksMsec() + 150;
        int frames = 0;
        // AudioServer completes native playback deletion on a later mix/update.
        // Monotonic time prevents a timer from expiring on a long capture frame.
        do
        {
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            frames++;
        }
        while (Time.GetTicksMsec() < deadline || frames < 3);
    }

    private static void StopAndRelease(AudioStreamPlayer? player)
    {
        if (!GodotObject.IsInstanceValid(player)) return;
        player!.Stop();
        player.Stream = null;
    }

    public void SetVolume(float music, float sound)
    {
        if (_stopped) return;
        _music.VolumeDb = music > .001f ? Mathf.LinearToDb(music * .5f) : -80;
        foreach (var voice in _voices) voice.VolumeDb = sound > .001f ? Mathf.LinearToDb(sound) : -80;
    }

    public void Play(FlightEventKind kind)
    {
        if (_stopped) return;
        var voice = _voices[_voice];
        _voice = (_voice + 1) % _voices.Length;
        voice.Stream = _sounds[(int)kind];
        voice.Play();
    }
}

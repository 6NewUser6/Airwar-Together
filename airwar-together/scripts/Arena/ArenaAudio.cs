using Godot;
using System.Collections.Generic;

namespace Airwar.Arena;

/// <summary>Plays saved flight audio from observed authoritative state, with no client combat decisions.</summary>
public partial class ArenaAudio : Node
{
    [Export] public AudioStream CannonSound { get; set; } = null!;
    [Export] public AudioStream ImpactSound { get; set; } = null!;
    [Export] public AudioStream DestructionSound { get; set; } = null!;
    [Export] public AudioStream BoostSound { get; set; } = null!;
    [Export] public AudioStream ArrivalSound { get; set; } = null!;

    private AudioStreamPlayer? _music, _engine, _charge;
    private readonly AudioStreamPlayer?[] _voices = new AudioStreamPlayer?[8];
    private int _voice, _peer, _sector, _highestShot;
    private float _health, _shield, _warp, _soundVolume = .8f, _impactCooldown;
    private bool _stopped, _seeded, _alive, _boosting;

    public override void _Ready()
    {
        _music = GetNode<AudioStreamPlayer>("Music");
        _engine = GetNode<AudioStreamPlayer>("Engine");
        _charge = GetNode<AudioStreamPlayer>("Charge");
        for (int i = 0; i < _voices.Length; i++) _voices[i] = GetNode<AudioStreamPlayer>($"Voices/Voice{i + 1:00}");
        // The owning controller may stop immediately while changing scenes or running a check.
        Callable.From(StartMusic).CallDeferred();
    }

    private void StartMusic()
    {
        if (!_stopped && IsInsideTree()) _music?.Play();
    }

    public void SetVolume(float music, float sound)
    {
        if (_stopped) return;
        _soundVolume = Mathf.Clamp(sound, 0, 1);
        if (_music != null) _music.VolumeDb = Db(Mathf.Clamp(music, 0, 1) * .55f);
        if (_charge != null) _charge.VolumeDb = Db(_soundVolume * .65f);
        foreach (var voice in _voices) if (voice != null) voice.VolumeDb = Db(_soundVolume * .75f);
    }

    private static float Db(float linear) => linear > .0001f ? Mathf.LinearToDb(linear) : -80;

    public void Observe(IReadOnlyDictionary<int, ArenaShip> ships, IReadOnlyList<ArenaShot> shots, int localPeerId, float dt)
    {
        if (_stopped) return;
        _impactCooldown = Mathf.Max(0, _impactCooldown - dt);
        if (!ships.TryGetValue(localPeerId, out var ship))
        {
            _seeded = false; _peer = 0; _highestShot = 0;
            _engine?.Stop(); _charge?.Stop(); return;
        }
        if (!_seeded || _peer != localPeerId)
        {
            _peer = localPeerId; _sector = ship.Sector; _health = ship.Health; _shield = ship.Shield;
            _alive = ship.Alive; _boosting = ship.Boosting; _warp = ship.WarpCharge;
            _highestShot = 0;
            foreach (var shot in shots) _highestShot = System.Math.Max(_highestShot, shot.Id);
            _seeded = true;
        }
        else
        {
            if (_sector != ship.Sector) { _charge?.Stop(); Play(ArrivalSound); }
            else if (ship.WarpCharge > 0 && _warp <= 0) _charge?.Play();
            else if (ship.WarpCharge <= 0 && _warp > 0) _charge?.Stop();
            if (_alive && !ship.Alive) { _charge?.Stop(); Play(DestructionSound); }
            else if (ship.Alive && ship.Health + ship.Shield < _health + _shield - .05f && _impactCooldown <= 0)
            { Play(ImpactSound); _impactCooldown = .12f; }
            if (ship.Alive && ship.Boosting && !_boosting) Play(BoostSound);

            // Server shot ids increase monotonically. Snapshot reappearance never repeats a muzzle sound.
            int highest = _highestShot; bool fired = false;
            foreach (var shot in shots)
            {
                if (shot.Id <= _highestShot) continue;
                highest = System.Math.Max(highest, shot.Id);
                if (shot.Owner == localPeerId && shot.Sector == ship.Sector) fired = true;
            }
            if (fired && ship.Alive) Play(CannonSound);
            _highestShot = highest;
        }
        if (_engine != null)
        {
            if (ship.Alive)
            {
                if (!_engine.Playing) _engine.Play();
                _engine.PitchScale = Mathf.Clamp(.7f + ship.Speed / 450, .7f, 1.6f);
                _engine.VolumeDb = Db(_soundVolume * (ship.Boosting ? .38f : .16f));
            }
            else _engine.Stop();
        }
        _sector = ship.Sector; _health = ship.Health; _shield = ship.Shield;
        _alive = ship.Alive; _boosting = ship.Boosting; _warp = ship.WarpCharge;
    }

    private void Play(AudioStream? stream)
    {
        if (stream == null) return;
        var voice = _voices[_voice]; _voice = (_voice + 1) % _voices.Length;
        if (voice == null) return;
        voice.Stream = stream; voice.Play();
    }

    public void StopAll()
    {
        if (_stopped) return;
        _stopped = true;
        Release(_music); Release(_engine); Release(_charge);
        foreach (var voice in _voices) Release(voice);
        _music = _engine = _charge = null;
        System.Array.Clear(_voices);
        CannonSound = ImpactSound = DestructionSound = BoostSound = ArrivalSound = null!;
        _seeded = false;
    }

    public async System.Threading.Tasks.Task StopAndDrainAsync()
    {
        StopAll();
        if (!IsInsideTree()) return;
        var tree = GetTree();
        ulong deadline = Time.GetTicksMsec() + 150;
        int frames = 0;
        // Stop only requests the audio thread's fade and native-reference release.
        // A SceneTreeTimer created after a slow screenshot can consume that long
        // frame's delta immediately. Require real elapsed time and later frames.
        do
        {
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            frames++;
        }
        while (Time.GetTicksMsec() < deadline || frames < 3);
    }

    private static void Release(AudioStreamPlayer? player)
    {
        if (!GodotObject.IsInstanceValid(player)) return;
        player!.Stop(); player.Stream = null;
    }

    public override void _ExitTree() => StopAll();
}

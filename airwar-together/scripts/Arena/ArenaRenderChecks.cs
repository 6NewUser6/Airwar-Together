using Godot;
using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Airwar.Arena;

/// <summary>Real-renderer stress check. The 32 local actors do not test network capacity.</summary>
public partial class ArenaRenderChecks : Node
{
    private readonly ArenaSimulation _simulation = new();
    private ArenaWorld _world = null!;
    private ArenaAudio _audio = null!;
    private ulong _started;
    private double _duration = 180, _nextMetric;
    private string _metricsPath = "";
    private uint _sequence;
    private int _sector = -1, _deathCycle = -1, _cameraCycle = -1, _visited;
    private bool _stopping;
    private bool _manualDraw;
    private long _drawnFrames;
    private string _captureDirectory = "";
    private int _capturedSectors;

    public override void _Ready()
    {
        Engine.MaxFps = 60;
        _world = GetNode<ArenaWorld>("World");
        _audio = GetNode<ArenaAudio>("Audio");
        GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += Cancel;
        RenderingServer.FramePostDraw += FrameDrawn;
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--arena-duration=") && double.TryParse(arg[17..], NumberStyles.Float,
                CultureInfo.InvariantCulture, out double duration) && double.IsFinite(duration))
                _duration = Math.Clamp(duration, 10, 3600);
            if (arg.StartsWith("--arena-metrics=")) _metricsPath = ProjectSettings.GlobalizePath(arg[16..]);
            if (arg == "--arena-manual-draw") _manualDraw = true;
            if (arg.StartsWith("--arena-capture-directory=")) _captureDirectory = ProjectSettings.GlobalizePath(arg[26..]);
        }
        if (DisplayServer.GetName().Equals("headless", StringComparison.OrdinalIgnoreCase))
        {
            GD.PushError("ARENA_RENDER_FAIL: this check requires an actual rendering display.");
            Stop(2); return;
        }
        if (!string.IsNullOrEmpty(_metricsPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_metricsPath))!);
            File.WriteAllText(_metricsPath, "");
        }
        if (!string.IsNullOrEmpty(_captureDirectory)) Directory.CreateDirectory(_captureDirectory);
        string[] hulls = { "spectre", "ember", "wraith", "stratus", "manta", "raptor", "ion", "flare", "nebula", "arctic", "jade", "gold" };
        for (int i = 1; i <= ArenaSimulation.MaxPlayers; i++)
            _simulation.AddPlayer(i, "Render " + i, hulls[(i - 1) % hulls.Length]);
        _started = Time.GetTicksMsec();
        GD.Print("ARENA_RENDER_START localActors=32 duration=", _duration,
            " display=", DisplayServer.GetName(),
            " renderer=", RenderingServer.GetCurrentRenderingMethod(),
            " driver=", RenderingServer.GetCurrentRenderingDriverName(), " networkCapacityTest=false");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_stopping) return;
        double elapsed = (Time.GetTicksMsec() - _started) / 1000.0;
        _simulation.Step();
        int sector = (int)(elapsed / 18) % SectorCatalog.Count;
        if (sector != _sector)
        {
            _sector = sector; _visited |= 1 << sector;
            foreach (var ship in _simulation.Ships.Values)
            {
                ship.Energy = 100; ship.Sector = sector;
                ship.Position = SectorCatalog.Spawn(sector, ship.PeerId - 1);
                ship.Rotation = Quaternion.Identity;
            }
        }
        int deathCycle = (int)(elapsed / 13);
        if (deathCycle != _deathCycle)
        {
            _deathCycle = deathCycle;
            var local = _simulation.Ships[1];
            local.Health = 0; local.Alive = false; local.RespawnRemaining = 1;
        }
        int cameraCycle = (int)(elapsed / 3);
        if (cameraCycle != _cameraCycle)
        { _cameraCycle = cameraCycle; _world.Cockpit = !_world.Cockpit; }
        _sequence++;
        foreach (var ship in _simulation.Ships.Values)
        {
            _simulation.SetCommand(ship.PeerId, new ArenaCommand(.25f * Mathf.Sin((float)elapsed * .15f),
                .2f, Mathf.Sin((float)elapsed * .25f), 1, true, (int)elapsed % 5 < 2, false, sector), _sequence);
            if (ship.PeerId == 1) continue;
            var local = _simulation.Ships[1];
            ship.Position = local.Position + new Vector3((ship.PeerId % 7 - 3) * 55,
                ((ship.PeerId / 7) % 3 - 1) * 40, -80 - ship.PeerId * 45);
            ship.Sector = local.Sector;
        }
    }

    public override void _Process(double delta)
    {
        if (_stopping) return;
        foreach (var ship in _simulation.Ships.Values)
        {
            if (!ship.Position.IsFinite() || !ship.Velocity.IsFinite() || !ship.Rotation.IsFinite())
            { GD.PushError($"ARENA_RENDER_FAIL: non-finite ship {ship.PeerId}"); Stop(1); return; }
        }
        foreach (var shot in _simulation.Shots)
            if (!shot.Position.IsFinite() || !shot.Velocity.IsFinite())
            { GD.PushError($"ARENA_RENDER_FAIL: non-finite shot {shot.Id}"); Stop(1); return; }
        _world.Render(_simulation.Ships, _simulation.Shots, 1, (float)delta);
        _audio.Observe(_simulation.Ships, _simulation.Shots, 1, (float)delta);
        // A covered desktop window may stop automatic draws while simulation keeps
        // running. The check pairs this with --disable-render-loop and reads back
        // actual pixels so elapsed time alone cannot count as GPU coverage.
        if (_manualDraw) RenderingServer.ForceDraw(true, delta);
        double elapsed = (Time.GetTicksMsec() - _started) / 1000.0;
        if (elapsed >= _nextMetric)
        {
            _nextMetric = elapsed + 5;
            if (!_world.Camera.GlobalTransform.IsFinite())
            { GD.PushError("ARENA_RENDER_FAIL: non-finite camera"); Stop(1); return; }
            string imageHash = "";
            if (_manualDraw)
            {
                using var image = GetViewport().GetTexture().GetImage();
                imageHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image.GetData()));
                int sectorBit = 1 << _world.DisplayedSector;
                if (elapsed >= 5 && !string.IsNullOrEmpty(_captureDirectory) && (_capturedSectors & sectorBit) == 0)
                {
                    var error = image.SavePng(Path.Combine(_captureDirectory, $"sector-{_world.DisplayedSector}.png"));
                    if (error != Error.Ok) { GD.PushError("ARENA_RENDER_FAIL: capture " + error); Stop(1); return; }
                    _capturedSectors |= sectorBit;
                }
            }
            string metric = JsonSerializer.Serialize(new {
                type = "arena_render_metrics", seconds = Math.Round(elapsed, 2),
                localActors = _simulation.Ships.Count, sector = _world.DisplayedSector,
                renderer = RenderingServer.GetCurrentRenderingMethod(),
                driver = RenderingServer.GetCurrentRenderingDriverName(),
                runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                manualDraw = _manualDraw, drawnFrames = System.Threading.Interlocked.Read(ref _drawnFrames), imageHash,
                cullThreadThreshold = ProjectSettings.GetSetting("rendering/limits/spatial_indexer/threaded_cull_minimum_instances").AsInt32(),
                fps = Engine.GetFramesPerSecond(), shots = _simulation.ShotsFired,
                drawCalls = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame),
                primitives = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame),
                gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2)
            });
            GD.Print("ARENA_RENDER_METRICS ", metric);
            if (!string.IsNullOrEmpty(_metricsPath)) File.AppendAllText(_metricsPath, metric + "\n");
        }
        if (elapsed < _duration) return;
        bool coverage = _duration < 60 || _visited == (1 << SectorCatalog.Count) - 1;
        bool passed = coverage && _simulation.Ships.Count == 32 && _simulation.ShotsFired > 0;
        GD.Print(passed ? "ARENA_RENDER_OK " : "ARENA_RENDER_FAIL ",
            "seconds=", elapsed, " localActors=", _simulation.Ships.Count,
            " visitedMask=", _visited, " shots=", _simulation.ShotsFired);
        Stop(passed ? 0 : 1);
    }

    private void Cancel()
    { GD.Print("ARENA_RENDER_CANCELLED"); Stop(2); }

    private void FrameDrawn() => System.Threading.Interlocked.Increment(ref _drawnFrames);

    private async void Stop(int exitCode)
    {
        if (_stopping) return;
        _stopping = true;
        SetProcess(false); SetPhysicsProcess(false);
        await _audio.StopAndDrainAsync();
        GetTree().Quit(exitCode);
    }

    public override void _ExitTree()
    {
        GetWindow().CloseRequested -= Cancel;
        RenderingServer.FramePostDraw -= FrameDrawn;
        _audio?.StopAll();
    }
}

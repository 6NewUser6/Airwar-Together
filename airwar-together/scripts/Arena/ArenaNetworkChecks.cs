using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Airwar.Arena;

/// <summary>Opt-in headless diagnostics using actual ENet endpoints, never the player's saved profile.</summary>
public partial class ArenaNetworkChecks : Node
{
    private string _mode = "", _report = "";
    private int _port = 24567, _index;
    private int _expectedClients = 32;
    private double _duration = 25, _elapsed, _nextReport, _joinedAt = -1;
    private readonly List<ArenaNetwork> _clients = new();
    private readonly HashSet<int> _joined = new(), _sectors = new();
    private ArenaNetwork? _server, _overflow;
    private bool _overflowStarted, _rejected, _passed, _finishing;
    private bool _reconnectStarted, _reconnected;
    private double _reconnectDue = -1;
    private int _previousPeer;
    private int _peak, _snapshots;
    private string _address = "127.0.0.1";

    public static bool TryRun(Node owner, string[] arguments)
    {
        string? modeArg = arguments.FirstOrDefault(a => a.StartsWith("--arena-network-test="));
        if (modeArg == null) return false;
        var check = new ArenaNetworkChecks { _mode = modeArg[21..] };
        foreach (string argument in arguments)
        {
            if (argument.StartsWith("--arena-port=") && int.TryParse(argument[13..], out int port)) check._port = port;
            if (argument.StartsWith("--arena-duration=") && double.TryParse(argument[17..], System.Globalization.CultureInfo.InvariantCulture, out double duration)) check._duration = Math.Clamp(duration, 3, 600);
            if (argument.StartsWith("--arena-report=")) check._report = argument[15..];
            if (argument.StartsWith("--arena-index=") && int.TryParse(argument[14..], out int index)) check._index = index;
            if (argument.StartsWith("--arena-count=") && int.TryParse(argument[14..], out int count)) check._expectedClients = Math.Clamp(count, 1, 32);
            if (argument.StartsWith("--arena-client=")) check._address = argument[15..];
        }
        owner.AddChild(check); return true;
    }

    public override void _Ready()
    {
        if (_mode is "server" or "listen")
        {
            _server = new ArenaNetwork(); AddChild(_server);
            Error result = _server.Host(_port, dedicated: _mode == "server");
            Record(new { type = "listening", port = _port, result = result.ToString() });
            if (result != Error.Ok) Finish(false);
        }
        else if (_mode == "client") AddClient(_index);
        else if (_mode == "overflow") { _overflow = AddClient(_index); }
        else if (_mode != "swarm") { Record(new { type = "error", reason = "Unknown test mode" }); Finish(false); }
    }

    private ArenaNetwork AddClient(int index)
    {
        var client = new ArenaNetwork { Name = "TestPeer" + index }; AddChild(client);
        client.Notice += text =>
        {
            if (text.Contains("房间已满")) { _rejected = true; Record(new { type = "rejected", index, reason = text }); }
        };
        client.Join(_address, _port, "Check" + index.ToString("D2"), "spectre"); _clients.Add(client); return client;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_finishing) return;
        _elapsed += delta;
        if (_mode == "swarm" && _clients.Count < _expectedClients && _elapsed > _clients.Count * .08) AddClient(_clients.Count);
        bool reconnectOwner = _mode == "swarm" || (_mode == "client" && _index == 0);
        if (reconnectOwner && !_reconnectStarted && _elapsed > 19 && _clients.Count > 0 && _clients[0].IsConnected)
        {
            _reconnectStarted = true; _previousPeer = _clients[0].LocalPeerId;
            _clients[0].Disconnect(); _reconnectDue = _elapsed + 1;
            Record(new { type = "disconnect", peer = _previousPeer });
        }
        if (_reconnectDue > 0 && _elapsed >= _reconnectDue)
        {
            _reconnectDue = -1; _clients[0].Join(_address, _port, "Check00-Rejoined", "spectre");
        }
        foreach (var client in _clients)
        {
            if (!client.IsConnected) continue;
            if (_joined.Add(client.LocalPeerId)) Record(new { type = "joined", peer = client.LocalPeerId, count = client.PlayerCount });
            if (_reconnectStarted && client == _clients[0] && client.LocalPeerId != _previousPeer) _reconnected = true;
            if (client.Ships.TryGetValue(client.LocalPeerId, out var ship))
            {
                _sectors.Add(ship.Sector);
                int target = _index % 4;
                if (_mode == "swarm") target = _clients.IndexOf(client) % 4;
                bool warp = _elapsed > 5 && _elapsed % 6 < .12 && target != ship.Sector;
                client.Submit(new ArenaCommand(.015f * Mathf.Sin((float)_elapsed), .1f, 0, .5f, _elapsed > 10, _elapsed % 5 < 1, warp, target));
            }
            _snapshots = Math.Max(_snapshots, (int)client.SnapshotPackets); _peak = Math.Max(_peak, client.PlayerCount);
        }
        if (_mode == "swarm" && _joined.Count >= _expectedClients && !_overflowStarted)
        {
            _overflowStarted = true; _joinedAt = _elapsed; _overflow = AddClient(99);
            Record(new { type = "capacity", accepted = _joined.Count, overflowStarted = true });
        }
        if (_elapsed >= _nextReport)
        {
            _nextReport = _elapsed + 1;
            if (_server != null)
                Record(new { type = "server", seconds = Math.Round(_elapsed, 2), players = _server.PlayerCount, peak = _server.Simulation!.PeakPlayers,
                    shots = _server.Simulation.ShotsFired, hits = _server.Simulation.Hits, warps = _server.Simulation.Warps,
                    sent = _server.BytesSent, received = _server.BytesReceived, invalid = _server.InvalidPackets,
                    sectors = _server.Ships.Values.Select(s => s.Sector).Distinct().Order().ToArray() });
            else Record(new { type = "client", seconds = Math.Round(_elapsed, 2), connected = _clients.Count(c => c.IsConnected), joined = _joined.Count,
                peak = _peak, snapshots = _snapshots, sectors = _sectors.Order().ToArray(), rejected = _rejected,
                latency = _clients.Count == 0 ? 0 : _clients[0].LatencyMs, reconnected = _reconnected });
        }
        if (_elapsed < _duration) return;
        _passed = _mode switch
        {
            "server" or "listen" => _server != null && _server.Simulation!.PeakPlayers >= 32 && _server.Simulation.ShotsFired > 100 && _server.Simulation.Warps >= 20,
            "swarm" => _joined.Count >= _expectedClients && _rejected && _peak == 32 && _snapshots > 20 && _sectors.Count == 4 && (_duration < 23 || _reconnected),
            "client" => _joined.Count >= 1 && _snapshots > 20 && _sectors.Contains(_index % 4) && (_index != 0 || _duration < 23 || _reconnected),
            "overflow" => _rejected && _joined.Count == 0,
            _ => false
        };
        Finish(_passed);
    }

    private void Record(object data)
    {
        string json = JsonSerializer.Serialize(data); GD.Print("ARENA_CHECK " + json);
        if (_report.Length == 0) return;
        string? directory = Path.GetDirectoryName(_report); if (directory != null) Directory.CreateDirectory(directory);
        File.AppendAllText(_report, json + System.Environment.NewLine);
    }
    private async void Finish(bool passed)
    {
        if (_finishing) return; _finishing = true;
        Record(new { type = "result", passed, mode = _mode, peak = _peak, joined = _joined.Count, rejected = _rejected, reconnected = _reconnected, sectors = _sectors.Order().ToArray() });
        foreach (var client in _clients) client.Disconnect(); _server?.Disconnect();
        // The root owns audio shutdown; requesting quit through its public window callback is not deterministic in headless mode.
        GetTree().Root.GetNodeOrNull<Node>("Main/FlightAudio")?.Call("StopAll");
        await ToSignal(GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
        GetTree().Quit(passed ? 0 : 1);
    }

    public static bool RunSelfTests()
    {
        int assertions = 0;
        void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException("Arena check failed: " + name); assertions++; }
        try
        {
            var capacity = new ArenaSimulation();
            for (int i = 1; i <= 32; i++) Check(capacity.AddPlayer(i, "Pilot" + i, "spectre"), "admit player " + i);
            Check(!capacity.AddPlayer(33, "Overflow", "spectre") && capacity.Ships.Count == 32, "33rd player rejected");
            Check(!capacity.AddPlayer(1, "Duplicate", "spectre"), "duplicate peer rejected");
            capacity.RemovePlayer(12); Check(capacity.AddPlayer(33, "Replacement", "spectre"), "vacated slot reusable");
            Check(!capacity.SetCommand(999, ArenaCommand.Neutral, 1), "unowned input ignored");
            Check(!capacity.SetCommand(1, ArenaCommand.Neutral with { Yaw = float.NaN }, 1), "NaN input rejected");
            Check(capacity.SetCommand(1, ArenaCommand.Neutral, 5) && !capacity.SetCommand(1, ArenaCommand.Neutral, 4), "stale input ignored");
            Check(ArenaSimulation.IsNewer(1, uint.MaxValue), "sequence wrap accepted");
            var sim = new ArenaSimulation(); sim.AddPlayer(1, "Attacker", "spectre"); sim.AddPlayer(2, "Target", "spectre");
            for (int i = 0; i < 130; i++) sim.Step();
            sim.Ships[1].Position = new Vector3(0, 1500, 0); sim.Ships[2].Position = new Vector3(0, 1500, -200);
            uint seq = 0;
            for (int i = 0; i < 90 && sim.Hits == 0; i++)
            { sim.SetCommand(1, ArenaCommand.Neutral with { Fire = true, Throttle = .5f }, ++seq); sim.Step(); }
            Check(sim.Hits > 0 && sim.Ships[2].Shield < 50 && sim.Ships[2].Health == 100, "swept shot drains shield first");
            for (int i = 0; i < 240 && sim.Ships[2].Alive; i++)
            { sim.SetCommand(1, ArenaCommand.Neutral with { Fire = true, Throttle = .5f }, ++seq); sim.Step(); }
            Check(!sim.Ships[2].Alive && sim.Ships[1].Kills == 1 && sim.Ships[2].Deaths == 1, "server death and score");
            sim.SetCommand(1, ArenaCommand.Neutral, ++seq);
            for (int i = 0; i < 310; i++) sim.Step();
            Check(sim.Ships[2].Alive && sim.Ships[2].Health == 100, "five-second respawn");
            var warp = new ArenaSimulation(); warp.AddPlayer(1, "Warp", "spectre");
            Check(warp.RequestWarp(1, 3), "valid warp requested");
            for (int i = 0; i < 170; i++) warp.Step();
            Check(warp.Ships[1].Sector == 0, "warp does not teleport before charge");
            for (int i = 0; i < 15; i++) warp.Step();
            Check(warp.Ships[1].Sector == 3 && warp.Warps == 1 && warp.Ships[1].Energy < 60, "three-second warp spends energy");
            warp.RequestWarp(1, 2); for (int i = 0; i < 200; i++) warp.Step();
            Check(warp.Ships[1].Sector == 3, "warp cooldown enforced");
            Check(!warp.RequestWarp(1, 9), "unknown sector rejected");
            var ground = new ArenaSimulation(); ground.AddPlayer(1, "Ground", "spectre"); for (int i = 0; i < 130; i++) ground.Step();
            ground.Ships[1].Position = new Vector3(0, -100, 0); ground.Step(); Check(!ground.Ships[1].Alive, "terrain collision destroys ship");
            var isolation = new ArenaSimulation(); isolation.AddPlayer(1, "Source", "spectre"); isolation.AddPlayer(2, "OtherSector", "spectre");
            for (int i = 0; i < 130; i++) isolation.Step();
            isolation.Ships[1].Position = new Vector3(0, 1500, 0); isolation.Ships[2].Position = new Vector3(0, 1500, -200); isolation.Ships[2].Sector = 3;
            for (uint i = 1; i <= 100; i++) { isolation.SetCommand(1, ArenaCommand.Neutral with { Fire = true, Throttle = .5f }, i); isolation.Step(); }
            Check(isolation.Ships[2].Health == 100 && isolation.Ships[2].Shield == 50, "shots isolated between sectors");
            for (int sector = 0; sector < 3; sector++)
            {
                var planet = new ArenaSimulation(); planet.AddPlayer(1, "Terrain" + sector, "spectre");
                planet.Ships[1].Sector = sector; planet.Ships[1].Position = new Vector3(0, 1800, 0);
                for (int i = 0; i < 130; i++) planet.Step();
                planet.Ships[1].Position = new Vector3(0, -500, 0); planet.Step();
                Check(!planet.Ships[1].Alive, "planet " + sector + " surface collision");
            }
            var fire = new ArenaSimulation(); fire.AddPlayer(1, "Heat", "spectre"); fire.Ships[1].Sector = 2;
            fire.Ships[1].Position = new Vector3(5000, 1200, 5000);
            for (int i = 0; i < 130; i++) fire.Step();
            for (int i = 0; i < 100; i++)
            {
                var ship = fire.Ships[1]; ship.Position = new Vector3(5000, ArenaSimulation.SurfaceHeight(2, 5000, 5000) + 20, 5000);
                fire.Step();
            }
            Check(fire.Ships[1].Shield < 30, "volcanic low altitude heat");
            var timeout = new ArenaSimulation(); timeout.AddPlayer(1, "Timeout", "spectre");
            timeout.SetCommand(1, ArenaCommand.Neutral with { Fire = true, Boost = true }, 1);
            for (int i = 0; i < 120; i++) timeout.Step();
            long stoppedAt = timeout.ShotsFired; for (int i = 0; i < 120; i++) timeout.Step();
            Check(!timeout.Ships[1].Boosting && timeout.ShotsFired == stoppedAt, "stale input stops fire and boost");
            var obstacles = new ArenaObstacles();
            Check(obstacles.Count > 100, "authored static collision data present");
            Check(obstacles.Intersects(3, new Vector3(0, 400, -4000), new Vector3(0, 400, -3000), 0, out _), "space station blocks projectiles");
            Vector3 ridgeFrom = new(-7385, 396.94f, -4750), ridgeTo = new(-7365, 396.94f, -4750);
            Check(ridgeFrom.Y > ArenaSimulation.SurfaceHeight(0, ridgeFrom.X, ridgeFrom.Z) &&
                ridgeTo.Y > ArenaSimulation.SurfaceHeight(0, ridgeTo.X, ridgeTo.Z), "ridge regression has two clear endpoints");
            Check(ArenaSimulation.TerrainHitFraction(0, ridgeFrom, ridgeTo) is > 0 and < 1,
                "terrain ridge occludes swept shot between clear endpoints");
            Check(Math.Abs(ArenaSimulation.SphereHitFraction(Vector3.Zero, new Vector3(20, 0, 0), new Vector3(25, 0, 0), 13) - .6f) < .001f,
                "hit ordering uses sphere entrance rather than projected center");
            byte[] packet = ArenaProtocol.Make(ArenaProtocol.Kind.Ships, w =>
            { w.Write((uint)1); w.Write((byte)12); for (int i = 0; i < 12; i++) ArenaProtocol.Ship(w, capacity.Ships.Values.First()); });
            Check(packet.Length <= ArenaProtocol.MaximumPacket, "snapshot datagram MTU bound");
            using (var reader = ArenaProtocol.Open(packet, out var kind))
            { Check(kind == ArenaProtocol.Kind.Ships, "protocol header"); reader.ReadUInt32(); int count = reader.ReadByte();
                for (int i = 0; i < count; i++) Check(ArenaProtocol.Ship(reader).PeerId > 0, "ship roundtrip"); ArenaProtocol.End(reader); }
            bool invalid = false; try { using var reader = ArenaProtocol.Open(new byte[2000], out _); } catch (InvalidDataException) { invalid = true; }
            Check(invalid, "oversize packet rejected");
            Check(ArenaProtocol.CleanName("[x]\n pilot").Contains('[') == false, "call sign sanitization");
            foreach (var badState in new[] {
                new ArenaShip { PeerId = 1, Position = new Vector3(1e20f, 0, 0) },
                new ArenaShip { PeerId = 1, Rotation = new Quaternion(1e20f, 1e20f, 1e20f, 1e20f) },
                new ArenaShip { PeerId = 1, Health = -1 }
            })
            {
                bool rejected = false;
                try
                {
                    byte[] badPacket = ArenaProtocol.Make(ArenaProtocol.Kind.Ships, w => ArenaProtocol.Ship(w, badState));
                    using var reader = ArenaProtocol.Open(badPacket, out _); ArenaProtocol.Ship(reader);
                }
                catch (InvalidDataException) { rejected = true; }
                Check(rejected, "finite but out-of-range server state rejected");
            }
            var ordering = new ArenaNetwork();
            try
            {
                var receive = typeof(ArenaNetwork).GetMethod("Receive", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                void Deliver(ArenaProtocol.Kind kind, int channel, Action<BinaryWriter> body) =>
                    receive.Invoke(ordering, new object[] { 1, channel, ArenaProtocol.Make(kind, body) });
                void Roster(uint tick, int sector) => Deliver(ArenaProtocol.Kind.Roster, 0, w =>
                { w.Write(tick); w.Write((byte)1); w.Write(2); w.Write((byte)sector); w.Write((ushort)0); w.Write((ushort)0); });
                void State(uint tick, int sector, float x) => Deliver(ArenaProtocol.Kind.Ships, 2, w =>
                { w.Write(tick); w.Write((byte)1); ArenaProtocol.Ship(w, new ArenaShip { PeerId = 2, Sector = sector, Position = new Vector3(x, 1000, 0) }); });
                Roster(100, 0); State(120, 3, 120);
                Check(ordering.Ships[2].Sector == 3, "new warp snapshot is accepted");
                Roster(110, 0);
                Check(ordering.Ships[2].Sector == 3, "delayed control roster cannot undo newer warp snapshot");
                Roster(140, 1); State(130, 3, 130);
                Check(ordering.Ships[2].Sector == 1, "delayed snapshot cannot undo newer roster sector");
                State(140, 1, 140);
                Check(ordering.Ships[2].Position.X == 140, "matching roster tick still accepts positional snapshot");
            }
            finally { ordering.Free(); }
            GD.Print($"ARENA_SELF_TEST PASS {assertions} assertions"); return true;
        }
        catch (Exception error) { GD.PushError(error.ToString()); GD.Print($"ARENA_SELF_TEST FAIL after {assertions} assertions"); return false; }
    }
}

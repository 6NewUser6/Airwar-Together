using Godot;
using System;

namespace Airwar.Arena;

/// <summary>Unattended dedicated room, without client scenes, audio or an automatic time limit.</summary>
public partial class ArenaServer : Node
{
    private ArenaNetwork _network = null!;
    private double _reportDue = 10, _elapsed;
    public override void _Ready()
    {
        int port = 24567;
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--arena-port=") && int.TryParse(arg[13..], out int parsed)) port = parsed;
        _network = GetNode<ArenaNetwork>("Network");
        Error result = _network.Host(port, dedicated: true);
        if (result != Error.Ok) { GD.PushError("ARENA_SERVER_FAILED " + result); GetTree().Quit(1); return; }
        _network.Notice += message => GD.Print("ARENA_ROOM ", message);
        GetWindow().CloseRequested += () => { _network.Disconnect(); GetTree().Quit(); };
        GD.Print($"ARENA_SERVER_READY UDP={port} capacity=32 sectors=4 obstacles={_network.Simulation!.StaticObstacleCount}");
    }
    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed < _reportDue || _network.Simulation == null) return;
        _reportDue = _elapsed + 10;
        GD.Print($"ARENA_SERVER players={_network.PlayerCount} tick={_network.Simulation.Tick} shots={_network.Simulation.ShotsFired} received={_network.BytesReceived} sent={_network.BytesSent}");
    }
}

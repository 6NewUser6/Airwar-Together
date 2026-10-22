using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Airwar.Persistence;

namespace Airwar.Arena;

public partial class ArenaController : Node
{
    private ArenaNetwork _network = null!;
    private ArenaWorld _world = null!;
    private ArenaHud _hud = null!;
    private ArenaFlightDisplay _display = null!;
    private ArenaAudio _audio = null!;
    private readonly ArenaPilotInput _input = new();
    private bool _quitting, _wasConnected, _autoPilot, _review;
    private string _capture = "";
    private int _captureFrame = 320, _frame;
    private float _duration, _elapsed;
    private int _previewSector;
    private Vector2 _viewSize;
    private bool _diagnostics;
    private double _nextMetric = 1;
    private string _metricsPath = "";

    public override void _Ready()
    {
        Engine.MaxFps = 60;
        _network = GetNode<ArenaNetwork>("Network"); _world = GetNode<ArenaWorld>("World");
        _hud = GetNode<ArenaHud>("Interface/Hud"); _display = GetNode<ArenaFlightDisplay>("Interface/Hud/Display");
        var args = OS.GetCmdlineUserArgs();
        _diagnostics = args.Contains("--arena-diagnostics");
        var profile = PilotProfile.Load();
        _hud.Initialize(profile, OS.HasFeature("mobile") || args.Contains("--mobile-preview"));
        _audio = GetNode<ArenaAudio>("Audio"); _audio.SetVolume(profile.MusicVolume, profile.SoundVolume);
        _display.Network = _network; _display.World = _world; _display.PilotInput = _input;
        _hud.HostRequested += Host; _hud.JoinRequested += Join; _hud.BackRequested += Back;
        _hud.LeaveRequested += Leave; _hud.MenuRequested += () => Menu(true); _hud.ResumeRequested += () => Menu(false);
        _hud.CameraRequested += () => _world.Cockpit = !_world.Cockpit;
        _hud.WarpRequested += _input.RequestWarp;
        _hud.PreviewRequested += sector => { _previewSector = sector; _world.SetPreviewSector(sector); };
        _network.Notice += _hud.Notice;
        _network.ConnectionChanged += ConnectionChanged;
        string address = "127.0.0.1"; int port = 24567;
        foreach (string arg in args)
        {
            if (arg.StartsWith("--arena-client=")) address = arg[15..];
            if (arg.StartsWith("--arena-port=") && int.TryParse(arg[13..], out int parsed)) port = parsed;
            if (arg.StartsWith("--arena-sector=") && int.TryParse(arg[15..], out int sector)) _previewSector = Mathf.Clamp(sector, 0, 3);
            if (arg.StartsWith("--capture=")) _capture = arg[10..];
            if (arg.StartsWith("--arena-metrics=")) { _metricsPath = ProjectSettings.GlobalizePath(arg[16..]); _diagnostics = true; }
            if (arg.StartsWith("--capture-frame=") && int.TryParse(arg[16..], out int frame)) _captureFrame = frame;
            if (arg.StartsWith("--arena-duration=") && float.TryParse(arg[17..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float duration)) _duration = duration;
        }
        _hud.SetEndpoint(address, port); _world.SetPreviewSector(_previewSector);
        _review = args.Contains("--arena-flight") || !string.IsNullOrEmpty(_capture);
        if (args.Contains("--arena-workflow-test")) { _review = true; CallDeferred(MethodName.RunWorkflowChecks); }
        _hud.GetNode<Label>("Lobby/Panel/Column/LocalAddress").Text = "本机地址  " + string.Join(" / ", IP.GetLocalAddresses().Where(x => x.Contains('.') && !x.StartsWith("127.")).Take(3));
        GetTree().AutoAcceptQuit = false; GetWindow().CloseRequested += Quit;
        ApplyLayout();
        if (args.Contains("--arena-server") || args.Contains("--arena-flight"))
        {
            bool dedicated = args.Contains("--arena-dedicated");
            var error = _network.Host(port, _hud.Callsign, _hud.SelectedShipId, dedicated);
            if (error != Error.Ok) _hud.Notice("创建房间失败：" + error);
            _autoPilot = args.Contains("--arena-flight");
        }
        if (args.Any(x => x.StartsWith("--arena-client="))) Join();
    }

    private bool ValidEndpoint(bool joining)
    {
        if (_hud.Port < 1024 || _hud.Port > 65535) { _hud.Notice("端口请输入 1024–65535。默认 24567。"); return false; }
        if (joining && string.IsNullOrWhiteSpace(_hud.Address)) { _hud.Notice("请输入房主 IP 地址或域名。"); return false; }
        return true;
    }
    private void Host()
    {
        if (!ValidEndpoint(false)) return;
        var error = _network.Host(_hud.Port, _hud.Callsign, _hud.SelectedShipId);
        if (error != Error.Ok) _hud.Notice("无法创建房间：" + error + "。请检查端口是否占用。");
    }
    private void Join()
    {
        if (!ValidEndpoint(true)) return;
        var error = _network.Join(_hud.Address, _hud.Port, _hud.Callsign, _hud.SelectedShipId);
        if (error != Error.Ok) _hud.Notice("无法连接：" + error);
        else { _hud.SetConnecting(true); _hud.Notice("正在连接房主…"); }
    }
    private void ConnectionChanged()
    {
        if (_quitting) return;
        if (_network.IsConnected && !_network.Connecting)
        {
            if (_wasConnected) return;
            _wasConnected = true; _hud.ShowFlight(); _display.Active = true; _input.Enabled = true;
            _hud.Notice("已进入房间 · 所有战区共享 32 个席位");
        }
        else if (!_network.Connecting)
        {
            _input.Reset(); _input.Enabled = false; _display.Active = false; _hud.ShowLobby();
            if (_wasConnected) _hud.Notice("已离开房间。"); _wasConnected = false;
        }
    }
    private void Leave() { _network.Disconnect(); ConnectionChanged(); _world.SetPreviewSector(_previewSector); }
    private void Menu(bool visible)
    {
        if (_hud.InLobby) return;
        _hud.ShowMenu(visible); _input.Reset(); _input.Enabled = !visible;
    }
    private async void Back()
    {
        if (_quitting) return;
        _quitting = true; _input.Reset(); _network.Disconnect();
        await _audio.StopAndDrainAsync();
        GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
    }
    private async void Quit()
    {
        if (_quitting) return;
        _quitting = true; _input.Reset(); _network.Disconnect();
        await _audio.StopAndDrainAsync(); GetTree().Quit();
    }
    public override void _ExitTree() { _input.Reset(); _network?.Disconnect(); _audio?.StopAll(); }

    private void ApplyLayout()
    {
        Vector2 view = GetViewport().GetVisibleRect().Size; _viewSize = view;
        Vector2 inset = Vector2.Zero, available = view;
        if (OS.HasFeature("mobile"))
        {
            Rect2I safe = DisplayServer.GetDisplaySafeArea(); Vector2I screen = DisplayServer.ScreenGetSize();
            if (safe.Size.X > 0 && safe.Size.Y > 0 && screen.X > 0 && screen.Y > 0)
            { inset = (Vector2)safe.Position * view / (Vector2)screen; available = (Vector2)safe.Size * view / (Vector2)screen; }
        }
        float scale = Mathf.Clamp(available.Y / 760, .6f, 1.5f);
        _hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _hud.Position = inset; _hud.Scale = Vector2.One * scale; _hud.Size = available / scale;
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (_quitting || _hud == null) return;
        if (inputEvent is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.PhysicalKeycode == Key.Escape)
            { if (_hud.InLobby) Back(); else Menu(!_hud.MenuVisible); GetViewport().SetInputAsHandled(); return; }
            if (!_hud.InLobby && !_hud.MenuVisible && key.PhysicalKeycode == Key.V)
            { _world.Cockpit = !_world.Cockpit; GetViewport().SetInputAsHandled(); return; }
        }
        if (inputEvent is InputEventScreenTouch or InputEventScreenDrag && !_hud.InLobby && !_hud.MenuVisible &&
            _input.Handle(inputEvent, GetViewport().GetVisibleRect().Size, _hud.ActionRects(), _hud.ReservedRects()))
            GetViewport().SetInputAsHandled();
    }
    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!_quitting && !_hud.InLobby && !_hud.MenuVisible && _input.Handle(inputEvent,
            GetViewport().GetVisibleRect().Size, _hud.ActionRects(), _hud.ReservedRects())) GetViewport().SetInputAsHandled();
    }
    public override void _PhysicsProcess(double delta)
    {
        if (_quitting || !_network.IsConnected) return;
        _input.Destination = _hud.Destination; _input.Throttle = _hud.Throttle;
        var command = _input.Sample((float)delta, _hud.PointerOnControl()); _hud.Throttle = _input.Throttle;
        if (_autoPilot)
        {
            command = new(0, .1f, 0, .5f, true, false, _elapsed > .3f && _elapsed < .36f && _previewSector != 0, _previewSector);
        }
        _network.Submit(command);
    }
    public override void _Process(double delta)
    {
        if (_quitting || _hud == null) return;
        float dt = Math.Min((float)delta, .1f); _elapsed += (float)delta;
        if (_viewSize != GetViewport().GetVisibleRect().Size) ApplyLayout();
        _hud.UpdateFlight(_network, dt);
        _audio.Observe(_network.Ships, _network.Shots, _network.LocalPeerId, dt);
        if (!_hud.InLobby) _world.Render(_network.Ships, _network.Shots, _network.LocalPeerId, dt);
        _frame++;
        if (_diagnostics && _elapsed >= _nextMetric)
        {
            _nextMetric = _elapsed + 1;
            string metric = JsonSerializer.Serialize(new { type = "arena_metrics", seconds = Math.Round(_elapsed, 2),
                fps = Engine.GetFramesPerSecond(), display = DisplayServer.GetName(), players = _network.PlayerCount,
                sector = _world.DisplayedSector, drawCalls = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame),
                primitives = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame),
                latencyMs = _network.LatencyMs, sent = _network.BytesSent, received = _network.BytesReceived });
            GD.Print("ARENA_METRICS ", metric);
            if (!string.IsNullOrEmpty(_metricsPath))
            { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_metricsPath))!); File.AppendAllText(_metricsPath, metric + "\n"); }
        }
        if (_frame == _captureFrame && !string.IsNullOrEmpty(_capture)) Capture();
        if (_duration > 0 && _elapsed >= _duration && string.IsNullOrEmpty(_capture)) Quit();
    }
    private async void Capture()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderingServer.ForceDraw(false);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_capture))!);
        Error error = GetViewport().GetTexture().GetImage().SavePng(_capture);
        GD.Print("ARENA_CAPTURE ", _capture, " ", error); Quit();
    }
    public override void _Notification(int what)
    {
        if (_hud == null) return;
        if (!_review && (long)what is NotificationApplicationFocusOut or NotificationWMWindowFocusOut)
        { _input.Reset(); if (!_hud.InLobby) Menu(true); }
        if ((long)what == NotificationWMGoBackRequest) { if (_hud.InLobby) Back(); else Menu(!_hud.MenuVisible); }
    }
}

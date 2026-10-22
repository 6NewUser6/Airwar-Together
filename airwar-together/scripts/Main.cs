using Godot;
using System;
using System.IO;
using System.Linq;
using Airwar.Core;
using Airwar.Persistence;
using Airwar.Presentation;
using Airwar.Inputs;
using Airwar.UI;
using Airwar.Audio;

namespace Airwar;

public partial class Main : Node
{
    private CombatWorld _world = null!;
    private FlightHud _hud = null!;
    private FlightAudio _audio = null!;
    private PilotProfile _profile = null!;
    private readonly PilotInput _input = new();
    private GameSession? _session;
    private bool _playing, _paused, _rewarded, _newRecord;
    private float _accumulator;
    private bool _reviewMode;
    private bool _workflowTest;
    private string _reviewDirectory = "";
    private string _scenario = "menu", _capture = "";
    private int _frame, _captureFrame = 320;
    private bool _capturing;
    private Vector2 _lastSize;
    public GameSession? Session => _session;
    public CombatWorld World => _world;

    public override void _Ready()
    {
        Engine.MaxFps = 60;
        var args = OS.GetCmdlineUserArgs();
        _workflowTest = args.Contains("--workflow-test");
        _reviewMode = _workflowTest;
        if (args.Contains("--self-test"))
        {
            bool passed = GameChecks.Run(); GetTree().Quit(passed ? 0 : 1); return;
        }
        foreach (string arg in args)
        {
            if (arg.StartsWith("--scenario=")) { _scenario = arg[11..]; _reviewMode = true; }
            if (arg.StartsWith("--capture=")) _capture = arg[10..];
            if (arg.StartsWith("--capture-frame=") && int.TryParse(arg[16..], out int frame)) _captureFrame = frame;
        }
        if (_reviewMode)
        {
            _reviewDirectory = Path.Combine(Path.GetTempPath(), "airwar-review-" + Guid.NewGuid());
            _profile = PilotProfile.Load(Path.Combine(_reviewDirectory, "pilot.json")); _profile.Credits = 75;
        }
        else _profile = PilotProfile.Load();
        _world = new CombatWorld(); AddChild(_world);
        _audio = new FlightAudio(); AddChild(_audio);
        var layer = new CanvasLayer(); AddChild(layer);
        var marker = new FlightMarker { Game = this }; layer.AddChild(marker); marker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _hud = new FlightHud { TouchLayout = OS.HasFeature("mobile") || args.Contains("--mobile-preview") }; layer.AddChild(_hud); _hud.Initialize(_profile);
        _hud.StartRequested += Start;
        _hud.MenuRequested += ReturnToMenu;
        _hud.PauseRequested += Pause;
        _hud.ResumeRequested += Resume;
        _hud.BoostRequested += _input.Boost;
        _hud.PulseRequested += _input.Pulse;
        _hud.PreviewRequested += ship => _world.Plane.Build(ship);
        _hud.SettingsChanged += ApplyPreferences;
        ApplyPreferences(); GetTree().AutoAcceptQuit = false; GetWindow().CloseRequested += Close;
        _lastSize = GetViewport().GetVisibleRect().Size;
        ApplySafeArea();
        if (_workflowTest) CallDeferred(MethodName.RunWorkflowTest);
        if (_scenario == "hangar") _hud.ShowHangar();
        if (_scenario == "settings") _hud.ShowSettings();
        if (_scenario == "legacy") _hud.ShowLegacyEgg();
        if (_scenario is "combat" or "pause" or "results")
        {
            Start();
            if (_scenario == "pause") Pause();
            if (_scenario == "results")
            {
                for (int i = 0; i < 18000 && !_session!.Finished; i++) _session.Step(new());
                _playing = false; _paused = true; _hud.ShowResults(_session!, true);
            }
        }
    }

    private void ApplyPreferences()
    {
        _input.Sensitivity = _profile.TouchSensitivity;
        _world.ReducedEffects = _profile.ReducedEffects;
        _audio.SetVolume(_profile.MusicVolume, _profile.SoundVolume);
    }

    private void Start()
    {
        RecordRun();
        _world.ClearEntities(); _input.Reset(); _accumulator = 0;
        _session = new(_profile.Difficulty, _reviewMode ? 4321 : Random.Shared.Next());
        _rewarded = false; _playing = true; _paused = false; _world.IsMenu = false;
        _world.Plane.Build(ShipCatalog.Find(_profile.SelectedShip));
        _world.Plane.Position = _session.PlayerPosition;
        _hud.ShowGame(); GetViewport().GuiReleaseFocus();
    }

    private void RecordRun()
    {
        if (_session == null || _rewarded) return;
        _rewarded = true;
        if (!_reviewMode) _newRecord = _profile.Record(_session);
    }

    private void ReturnToMenu()
    {
        RecordRun(); _playing = false; _paused = false; _session = null; _input.Reset();
        _world.ClearEntities(); _world.IsMenu = true; _world.Plane.Build(ShipCatalog.Find(_profile.SelectedShip)); _hud.ShowMenu();
    }

    private void Pause()
    {
        if (!_playing || _session == null || _paused) return;
        _paused = true; _input.Reset(); _hud.ShowPause(_session);
    }

    private void Resume()
    {
        if (!_playing || _session == null) return;
        _paused = false; _input.Reset(); _hud.ShowGame(); GetViewport().GuiReleaseFocus();
    }

    private void Close() { RecordRun(); if (!_reviewMode) _profile.Save(); GetTree().Quit(); }

    public override void _Input(InputEvent inputEvent)
    {
        if (_hud == null) return;
        if (_playing && !_paused && _session != null)
        {
            if (inputEvent is InputEventScreenTouch ownedTouch && _input.OwnsTouch(ownedTouch.Index))
            { _input.Handle(inputEvent, _world, _session); GetViewport().SetInputAsHandled(); return; }
            if (inputEvent is InputEventScreenDrag ownedDrag && _input.OwnsTouch(ownedDrag.Index))
            { _input.Handle(inputEvent, _world, _session); GetViewport().SetInputAsHandled(); return; }
        }
        if (_playing && !_paused && inputEvent is InputEventScreenTouch touch && _hud.HandleActionTouch(touch))
        { GetViewport().SetInputAsHandled(); return; }
        if (inputEvent is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (key.PhysicalKeycode == Key.Escape)
        {
            if (_playing) { if (_paused) Resume(); else Pause(); }
            else if (_hud.Screen != "menu") ReturnToMenu();
            GetViewport().SetInputAsHandled();
        }
        if (_playing && !_paused && key.PhysicalKeycode is Key.Space or Key.Shift)
        {
            _input.Handle(inputEvent, _world, _session!); GetViewport().SetInputAsHandled();
        }
        if (!_playing && _hud.Screen == "menu" && key.PhysicalKeycode == Key.Enter)
        { Start(); GetViewport().SetInputAsHandled(); }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_playing && !_paused && _session != null) _input.Handle(inputEvent, _world, _session);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_playing || _paused || _session == null) return;
        _accumulator += (float)delta;
        var command = _input.Sample();
        while (_accumulator >= GameSession.StepSeconds && !_session.Finished)
        {
            if (_reviewMode && !_workflowTest)
            {
                float time = _session.Elapsed;
                command = new(Vector3.Zero, new Vector3(Mathf.Sin(time * .55f) * 4, GameSession.FlightHeight, Mathf.Cos(time * .4f) * 2), false, false);
            }
            _session.Step(command);
            // Button edges apply once even when catching up multiple simulation ticks.
            command = command with { Boost = false, Pulse = false };
            foreach (var flightEvent in _session.Events)
            {
                _world.OnFlightEvent(flightEvent); _hud.OnFlightEvent(flightEvent); _audio.Play(flightEvent.Kind);
            }
            _accumulator -= GameSession.StepSeconds;
        }
        if (_session.Finished)
        {
            RecordRun(); _playing = false; _hud.ShowResults(_session, _newRecord);
        }
    }

    public override void _Process(double delta)
    {
        if (_hud == null) return;
        float dt = Math.Min((float)delta, .1f);
        _world.Render(_session, _paused ? 0 : dt);
        if (_session != null) _hud.UpdateGame(_session, _paused ? 0 : dt);
        var size = GetViewport().GetVisibleRect().Size;
        if (size != _lastSize)
        {
            _lastSize = size; ApplySafeArea();
            switch (_hud.Screen)
            {
                case "menu": _hud.ShowMenu(); break;
                case "hangar": _hud.ShowHangar(); break;
                case "settings": _hud.ShowSettings(); break;
                case "legacy": _hud.ShowLegacyEgg(); break;
                case "game": _hud.ShowGame(); break;
                case "pause": if (_session != null) _hud.ShowPause(_session); break;
                case "results": if (_session != null) _hud.ShowResults(_session, _newRecord); break;
            }
        }
        if (_capture != "" && !_capturing && ++_frame >= _captureFrame) Capture();
    }

    private void ApplySafeArea()
    {
        _hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        if (!OS.HasFeature("mobile")) return;
        var safe = DisplayServer.GetDisplaySafeArea(); var screen = DisplayServer.ScreenGetSize();
        if (safe.Size.X <= 0 || safe.Size.Y <= 0 || screen.X <= 0 || screen.Y <= 0) return;
        var view = GetViewport().GetVisibleRect().Size;
        _hud.OffsetLeft = safe.Position.X * view.X / screen.X;
        _hud.OffsetTop = safe.Position.Y * view.Y / screen.Y;
        _hud.OffsetRight = (safe.End.X - screen.X) * view.X / screen.X;
        _hud.OffsetBottom = (safe.End.Y - screen.Y) * view.Y / screen.Y;
    }

    private async void Capture()
    {
        _capturing = true;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        // A desktop compositor can suppress normal draws for an obscured window.
        // Force a render without a buffer swap before reading back the review image.
        RenderingServer.ForceDraw(false);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_capture))!);
        var image = GetViewport().GetTexture().GetImage(); var error = image.SavePng(_capture);
        GD.Print("CAPTURE ", _capture, " ", error); GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public override void _Notification(int what)
    {
        if (_hud != null && !_reviewMode && (long)what is NotificationApplicationFocusOut or NotificationWMWindowFocusOut) Pause();
        if (_hud != null && (long)what == NotificationWMGoBackRequest)
        {
            if (_playing) { if (_paused) Resume(); else Pause(); }
            else if (_hud.Screen != "menu") ReturnToMenu();
        }
    }

    public override void _ExitTree()
    {
        if (_reviewDirectory != "" && Directory.Exists(_reviewDirectory)) Directory.Delete(_reviewDirectory, true);
    }

    private async void RunWorkflowTest()
    {
        int checks = 0;
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            checks++; GD.Print("PASS_WORKFLOW ", label);
        }
        async System.Threading.Tasks.Task Frames(int count = 2)
        {
            for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        void InjectTouch(InputEvent touch)
        {
            // Test points are in viewport coordinates. ParseInputEvent accepts
            // window pixels, which differ when content scaling is active.
            Godot.Input.ParseInputEvent(touch.XformedBy(GetViewport().GetFinalTransform()));
        }
        Button FindButton(string caption)
        {
            Button? found = null;
            void Visit(Node node)
            {
                if (node is Button button && button.Text.Contains(caption)) found = button;
                foreach (var child in node.GetChildren()) Visit(child);
            }
            Visit(_hud); return found ?? throw new InvalidOperationException("Button missing: " + caption);
        }
        try
        {
            await Frames();
            Check(_hud.Screen == "menu", "menu loads");
            var title = (Label)_hud.FindChild("LegacyTitle", true, false);
            for (int i = 0; i < 4; i++) title.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            Check(_hud.Screen == "menu", "legacy title stays hidden before fifth tap");
            title.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true }); await Frames();
            Check(_hud.Screen == "legacy", "fifth title tap opens golden legacy egg");
            FindButton("返回飞行系统").EmitSignal(Button.SignalName.Pressed); await Frames();
            _hud.ShowHangar(); await Frames();
            FindButton("余烬").EmitSignal(Button.SignalName.Pressed); await Frames();
            FindButton("解锁战机").EmitSignal(Button.SignalName.Pressed); await Frames();
            Check(_profile.SelectedShip == "ember" && _profile.Credits == 50, "hangar purchase and equipment");
            FindButton("返回主菜单").EmitSignal(Button.SignalName.Pressed); await Frames();
            Godot.Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Enter, Pressed = true }); await Frames();
            Check(_playing && _hud.Screen == "game", "keyboard starts game");
            var start = _session!.PlayerPosition;
            Godot.Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Pressed = true }); await Frames(10);
            Godot.Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Pressed = false });
            Check(_session.PlayerPosition.X > start.X + .2f, "keyboard flight moves plane");
            var beforeOuterDrag = _session.PlayerPosition;
            var outerStart = _world.Camera.UnprojectPosition(new Vector3(GameSession.HalfWidth + 1.25f, GameSession.FlightHeight, 0));
            var outerEnd = _world.Camera.UnprojectPosition(new Vector3(GameSession.HalfWidth + 2.25f, GameSession.FlightHeight, 0));
            InjectTouch(new InputEventScreenTouch { Index = 0, Position = outerStart, Pressed = true }); await Frames();
            InjectTouch(new InputEventScreenDrag { Index = 0, Position = outerEnd, Relative = outerEnd - outerStart }); await Frames(8);
            Check(_session.PlayerPosition.X > beforeOuterDrag.X + .7f, "touch keeps moving outside arena projection");
            var afterOuterDrag = _session.PlayerPosition;
            var outerReverse = _world.Camera.UnprojectPosition(new Vector3(GameSession.HalfWidth + 1.75f, GameSession.FlightHeight, 0));
            InjectTouch(new InputEventScreenDrag { Index = 0, Position = outerReverse, Relative = outerReverse - outerEnd }); await Frames(6);
            Check(_session.PlayerPosition.X < afterOuterDrag.X - .3f, "outside touch reverses without a dead zone");
            InjectTouch(new InputEventScreenTouch { Index = 0, Position = outerReverse, Pressed = false }); await Frames();
            var beforeTopDrag = _session.PlayerPosition;
            var topStart = _world.Camera.UnprojectPosition(new Vector3(5, GameSession.FlightHeight, -GameSession.HalfDepth - 1.25f));
            var topEnd = _world.Camera.UnprojectPosition(new Vector3(5, GameSession.FlightHeight, -GameSession.HalfDepth - 2.25f));
            InjectTouch(new InputEventScreenTouch { Index = 0, Position = topStart, Pressed = true }); await Frames();
            InjectTouch(new InputEventScreenDrag { Index = 0, Position = topEnd, Relative = topEnd - topStart }); await Frames(8);
            Check(_session.PlayerPosition.Z < beforeTopDrag.Z - .7f, "vertical touch keeps moving outside arena projection");
            var afterTopDrag = _session.PlayerPosition;
            var topReverse = _world.Camera.UnprojectPosition(new Vector3(5, GameSession.FlightHeight, -GameSession.HalfDepth - 1.75f));
            InjectTouch(new InputEventScreenDrag { Index = 0, Position = topReverse, Relative = topReverse - topEnd }); await Frames(6);
            Check(_session.PlayerPosition.Z > afterTopDrag.Z + .3f, "vertical outside touch reverses without a dead zone");
            InjectTouch(new InputEventScreenTouch { Index = 0, Position = topReverse, Pressed = false }); await Frames();
            var left = _world.Camera.UnprojectPosition(new Vector3(-8, GameSession.FlightHeight, 0));
            var right = _world.Camera.UnprojectPosition(new Vector3(8, GameSession.FlightHeight, 0));
            InjectTouch(new InputEventScreenTouch { Index = 0, Position = left, Pressed = true }); await Frames();
            InjectTouch(new InputEventScreenDrag { Index = 0, Position = right, Relative = right - left }); await Frames(40);
            Check(Mathf.Abs(_session.PlayerPosition.X - GameSession.HalfWidth) < .001f, "dragging still respects the aircraft boundary");
            var rightOvershoot = _world.Camera.UnprojectPosition(new Vector3(9, GameSession.FlightHeight, 0));
            InjectTouch(new InputEventScreenDrag { Index = 0, Position = rightOvershoot, Relative = rightOvershoot - right }); await Frames();
            var rightReverse = _world.Camera.UnprojectPosition(new Vector3(8.5f, GameSession.FlightHeight, 0));
            InjectTouch(new InputEventScreenDrag { Index = 0, Position = rightReverse, Relative = rightReverse - rightOvershoot }); await Frames(6);
            Check(_session.PlayerPosition.X < GameSession.HalfWidth - .3f, "aircraft reverses immediately after boundary overshoot");
            InjectTouch(new InputEventScreenTouch { Index = 0, Position = rightReverse, Pressed = false }); await Frames();
            var fineStart = _world.Camera.UnprojectPosition(new Vector3(0, GameSession.FlightHeight, 1));
            var fineEnd = _world.Camera.UnprojectPosition(new Vector3(-.01f, GameSession.FlightHeight, 1));
            var beforeFineDrag = _session.PlayerPosition;
            InjectTouch(new InputEventScreenTouch { Index = 0, Position = fineStart, Pressed = true }); await Frames();
            InjectTouch(new InputEventScreenDrag { Index = 0, Position = fineEnd, Relative = fineEnd - fineStart }); await Frames(3);
            Check(_session.PlayerPosition.X < beforeFineDrag.X - .005f, "fine touch movement stays responsive");
            var beforeRelease = _session.PlayerPosition;
            InjectTouch(new InputEventScreenDrag { Index = 0, Position = fineEnd + new Vector2(-200, 0), Relative = new(-200, 0) });
            InjectTouch(new InputEventScreenTouch { Index = 0, Position = fineEnd + new Vector2(-200, 0), Pressed = false }); await Frames(3);
            Check(!_input.TouchActive && _input.Sample().Target == null && _session.PlayerPosition.DistanceTo(beforeRelease) < .001f, "finger release clears pending movement immediately");
            InjectTouch(new InputEventScreenTouch { Index = 0, Position = fineStart, Pressed = true }); await Frames();
            InjectTouch(new InputEventScreenTouch { Index = 0, Position = fineStart, Pressed = false, Canceled = true }); await Frames();
            Check(!_input.TouchActive && _input.Sample().Target == null, "canceled touch clears steering capture");
            var beforeRelativeDrag = _session.PlayerPosition;
            var point = _world.Camera.UnprojectPosition(_session.PlayerPosition);
            InjectTouch(new InputEventScreenTouch { Index = 0, Position = point, Pressed = true }); await Frames();
            InjectTouch(new InputEventScreenDrag { Index = 0, Position = point + new Vector2(-100, -20), Relative = new(-100, -20) }); await Frames(8);
            Check(_input.TouchActive && _session.PlayerPosition.X < beforeRelativeDrag.X - .3f, "relative touch flight");
            var pulsePoint = FindButton("脉冲").GetGlobalRect().GetCenter();
            InjectTouch(new InputEventScreenTouch { Index = 1, Position = pulsePoint, Pressed = true }); await Frames();
            InjectTouch(new InputEventScreenTouch { Index = 1, Position = pulsePoint, Pressed = false }); await Frames();
            Check(_session.PulseCooldown > 10 && _input.TouchActive, "second finger pulses while steering");
            InjectTouch(new InputEventScreenTouch { Index = 0, Position = pulsePoint, Pressed = false }); await Frames();
            Check(!_input.TouchActive, "steering finger release over action button");
            Godot.Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true }); await Frames();
            int tick = _session.Tick; await Frames(6);
            Check(_paused && _hud.Screen == "pause" && _session.Tick == tick, "pause stops simulation");
            FindButton("继续飞行").EmitSignal(Button.SignalName.Pressed); await Frames();
            Check(!_paused && _session.Tick > tick, "resume advances simulation");
            var oldSession = _session;
            Pause(); _hud.ShowPause(_session); await Frames();
            FindButton("重新开始").EmitSignal(Button.SignalName.Pressed); await Frames();
            Check(_playing && !ReferenceEquals(_session, oldSession) && _session!.Elapsed < 1, "restart replaces session");
            _session!.Threats.Add(new() { Id = 10000, Kind = ThreatKind.Projectile, Position = _session.PlayerPosition, Lifetime = 5 });
            // Repeated damage spaced beyond protection deterministically reaches results.
            _paused = true;
            for (int i = 0; i < 5 && !_session.Finished; i++)
            {
                for (int j = 0; j < 122; j++) { _session.Threats.Clear(); _session.Step(new()); }
                _session.Threats.Clear();
                _session.Threats.Add(new() { Id = 12000 + i, Kind = ThreatKind.Projectile, Position = _session.PlayerPosition, Lifetime = 5 });
                _session.Step(new());
            }
            _paused = false; await Frames();
            Check(_session.Finished && !_playing && _hud.Screen == "results", "death opens results");
            FindButton("返回主菜单").EmitSignal(Button.SignalName.Pressed); await Frames();
            Check(_session == null && _hud.Screen == "menu" && _world.IsMenu, "results returns to menu");
            GD.Print("WORKFLOW_TEST_OK ", checks); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("WORKFLOW_TEST_FAILED " + e); GetTree().Quit(1); }
    }
}

public partial class FlightMarker : Control
{
    public Main Game { get; set; } = null!;
    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        var session = Game.Session;
        if (session == null || Game.World.IsMenu) return;
        Vector2 point = Game.World.Camera.UnprojectPosition(session.PlayerPosition);
        var color = new Color("95f1f7");
        DrawCircle(point, 3, color);
        DrawArc(point, 9, 0, Mathf.Tau, 24, new Color(color, .4f), 1, true);
    }
}

using Godot;
using System;
using System.Collections.Generic;
using Airwar.Core;
using Airwar.Persistence;

namespace Airwar.UI;

/// <summary>
/// Binds gameplay data and actions to the authored FlightHud scene. Layout,
/// typography, colors, materials and every screen live in scenes/ui and assets/ui.
/// Switching screens preserves their nodes instead of rebuilding the interface.
/// </summary>
public partial class FlightHud : Control
{
    public event Action? StartRequested, ArenaRequested, MenuRequested, PauseRequested, ResumeRequested, BoostRequested, PulseRequested, SettingsChanged;
    public event Action<ShipDefinition>? PreviewRequested;

    private PilotProfile _profile = null!;
    private readonly Dictionary<string, Control> _pages = new();
    private string _screen = "menu";
    private bool _wired, _settingsLoading;
    private int _preview, _legacyClicks;
    private ulong _lastLegacyClick;
    private float _feedbackTime;
    private Label _score = null!, _time = null!, _health = null!, _wave = null!, _feedback = null!;
    private Button _boost = null!, _pulse = null!, _pauseButton = null!;

    public string Screen => _screen;
    public bool TouchLayout { get; set; } = OS.HasFeature("mobile");

    public override void _Ready() => BindScene();

    public void Initialize(PilotProfile profile)
    {
        BindScene();
        _profile = profile;
        ShowMenu();
    }

    private T View<T>(string path) where T : Node => GetNode<T>(path);

    private void BindScene()
    {
        if (_wired) return;
        foreach (var (key, node) in new[]
        {
            ("menu", "Menu"), ("hangar", "Hangar"), ("settings", "Settings"),
            ("game", "Game"), ("pause", "Pause"), ("results", "Results"), ("legacy", "Legacy")
        }) _pages[key] = View<Control>(node);

        _score = View<Label>("Game/Score"); _time = View<Label>("Game/Time");
        _health = View<Label>("Game/Health"); _wave = View<Label>("Game/Wave");
        _feedback = View<Label>("Game/Feedback");
        _boost = View<Button>("Game/Boost"); _pulse = View<Button>("Game/Pulse");
        _pauseButton = View<Button>("Game/Pause");

        View<Label>("Menu/Column/LegacyTitle").GuiInput += HandleLegacyTitle;
        View<Button>("Menu/Column/Start").Pressed += () => StartRequested?.Invoke();
        View<Button>("Menu/Column/Flight3D").Pressed += () => ArenaRequested?.Invoke();
        View<Button>("Menu/Column/Links/Hangar").Pressed += ShowHangar;
        View<Button>("Menu/Column/Links/Settings").Pressed += ShowSettings;
        foreach (Difficulty difficulty in Enum.GetValues<Difficulty>())
        {
            var selected = difficulty;
            View<Button>("Menu/Column/Difficulty/" + difficulty).Pressed += () =>
            {
                _profile.Difficulty = selected; _profile.Save(); ShowMenu();
            };
        }

        for (int i = 0; i < ShipCatalog.All.Length; i++)
        {
            int index = i;
            View<Button>("Hangar/Column/ShipGrid/" + ShipCatalog.All[i].Id).Pressed += () =>
            {
                _preview = index; ShowHangar();
            };
        }
        View<Button>("Hangar/Column/Equip").Pressed += EquipPreview;
        View<Button>("Hangar/Column/Return").Pressed += () =>
        {
            PreviewRequested?.Invoke(ShipCatalog.Find(_profile.SelectedShip)); ShowMenu();
        };

        View<LineEdit>("Settings/Column/Callsign").TextChanged += value =>
        {
            if (!_settingsLoading) _profile.Callsign = value;
        };
        View<HSlider>("Settings/Column/Music/Slider").ValueChanged += value =>
        {
            if (_settingsLoading) return; _profile.MusicVolume = (float)value; Changed();
        };
        View<HSlider>("Settings/Column/Sound/Slider").ValueChanged += value =>
        {
            if (_settingsLoading) return; _profile.SoundVolume = (float)value; Changed();
        };
        View<HSlider>("Settings/Column/Sensitivity/Slider").ValueChanged += value =>
        {
            if (_settingsLoading) return; _profile.TouchSensitivity = (float)value; Changed();
        };
        View<CheckButton>("Settings/Column/Effects").Toggled += value =>
        {
            if (_settingsLoading) return; _profile.ReducedEffects = value; Changed();
        };
        View<Button>("Settings/Column/Save").Pressed += () => { Changed(); ShowMenu(); };

        _pauseButton.Pressed += () => PauseRequested?.Invoke();
        _boost.Pressed += () => BoostRequested?.Invoke();
        _pulse.Pressed += () => PulseRequested?.Invoke();
        View<Button>("Pause/Center/Panel/Margin/Column/Resume").Pressed += () => ResumeRequested?.Invoke();
        View<Button>("Pause/Center/Panel/Margin/Column/Restart").Pressed += () => StartRequested?.Invoke();
        View<Button>("Pause/Center/Panel/Margin/Column/Return").Pressed += () => MenuRequested?.Invoke();
        View<Button>("Results/Center/Panel/Margin/Column/Retry").Pressed += () => StartRequested?.Invoke();
        View<Button>("Results/Center/Panel/Margin/Column/Return").Pressed += () => MenuRequested?.Invoke();
        View<Button>("Legacy/Center/Panel/Margin/Column/Return").Pressed += ShowMenu;
        _wired = true;
    }

    private void ShowPage(string screen)
    {
        BindScene();
        _screen = screen;
        foreach (var pair in _pages) pair.Value.Visible = pair.Key == screen;
        View<Label>("Notice").Hide();
    }

    private void HandleLegacyTitle(InputEvent input)
    {
        if (input is not InputEventMouseButton click || !click.Pressed || click.ButtonIndex != MouseButton.Left) return;
        ulong now = Time.GetTicksMsec();
        _legacyClicks = now - _lastLegacyClick > 3000 ? 1 : _legacyClicks + 1;
        _lastLegacyClick = now;
        if (_legacyClicks >= 5) { _legacyClicks = 0; ShowLegacyEgg(); }
        AcceptEvent();
    }

    private static void SetSelected(Button button, bool selected) =>
        button.ThemeTypeVariation = selected ? "PrimaryButton" : "";

    public static string DifficultyName(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => "简单", Difficulty.Normal => "普通", Difficulty.Hard => "困难", _ => "地狱"
    };

    public void ShowMenu()
    {
        ShowPage("menu");
        View<Label>("Menu/Status").Text = $"{_profile.Callsign}     /     {_profile.Credits:D3} CR";
        View<Label>("Menu/Column/Record").Text = $"最高纪录  {_profile.BestScores.GetValueOrDefault(_profile.Difficulty.ToString()):N0}     /     {_profile.UnlockedShips.Count:00} 架机体";
        foreach (Difficulty difficulty in Enum.GetValues<Difficulty>())
            SetSelected(View<Button>("Menu/Column/Difficulty/" + difficulty), _profile.Difficulty == difficulty);
        View<Label>("Menu/Column/DifficultyHint").Text = _profile.Difficulty == Difficulty.Inferno
            ? "一命挑战 · 极限节奏" : "躲避激光与弹幕 · 收集补给 · 活得更久";
        var ship = ShipCatalog.Find(_profile.SelectedShip);
        View<Label>("Menu/ShipLabel").Text = $"{ship.Name}\n{ship.Role}";
        View<Label>("Menu/Footer").Text = TouchLayout ? "单指拖动飞行 · 右侧按钮释放技能"
            : "WASD / 方向键 / 鼠标移动    SHIFT 冲刺    SPACE 清弹    ESC 暂停";
        if (!string.IsNullOrEmpty(_profile.RecoveryNotice)) Notice(_profile.RecoveryNotice);
    }

    public void ShowHangar()
    {
        ShowPage("hangar");
        View<Label>("Hangar/Status").Text = $"机库     /     {_profile.Credits:D3} CR";
        _preview = Math.Clamp(_preview, 0, ShipCatalog.All.Length - 1);
        for (int i = 0; i < ShipCatalog.All.Length; i++)
        {
            var ship = ShipCatalog.All[i];
            var button = View<Button>("Hangar/Column/ShipGrid/" + ship.Id);
            button.Text = (_profile.UnlockedShips.Contains(ship.Id) ? "" : "+ ") + ship.Name;
            SetSelected(button, _preview == i);
        }
        var selected = ShipCatalog.All[_preview];
        bool unlocked = _profile.UnlockedShips.Contains(selected.Id);
        var role = View<Label>("Hangar/Column/Role"); role.Text = selected.Role;
        role.AddThemeColorOverride("font_color", selected.Color);
        View<Label>("Hangar/Column/UnlockHint").Text = unlocked ? "已解锁 · 进入战场前选择机体"
            : $"解锁需要 {selected.Cost} CR · 生存与拾取获得金币";
        var equip = View<Button>("Hangar/Column/Equip");
        equip.Text = unlocked ? (selected.Id == _profile.SelectedShip ? "已装备    ✓" : "装备战机") : $"解锁战机    {selected.Cost} CR";
        equip.Disabled = !unlocked && _profile.Credits < selected.Cost;
        PreviewRequested?.Invoke(selected);
    }

    private void EquipPreview()
    {
        var selected = ShipCatalog.All[_preview];
        if (_profile.UnlockedShips.Contains(selected.Id)) { _profile.SelectedShip = selected.Id; _profile.Save(); }
        else _profile.Unlock(selected.Id);
        ShowHangar();
    }

    public void ShowSettings()
    {
        ShowPage("settings");
        _settingsLoading = true;
        try
        {
            View<LineEdit>("Settings/Column/Callsign").Text = _profile.Callsign;
            View<HSlider>("Settings/Column/Music/Slider").SetValueNoSignal(_profile.MusicVolume);
            View<HSlider>("Settings/Column/Sound/Slider").SetValueNoSignal(_profile.SoundVolume);
            View<HSlider>("Settings/Column/Sensitivity/Slider").SetValueNoSignal(_profile.TouchSensitivity);
            View<CheckButton>("Settings/Column/Effects").SetPressedNoSignal(_profile.ReducedEffects);
        }
        finally { _settingsLoading = false; }
    }

    private void Changed() { _profile.Save(); SettingsChanged?.Invoke(); }

    public void ShowGame()
    {
        ShowPage("game");
        View<Label>("Game/Pilot").Text = _profile.Callsign + "  /  " + DifficultyName(_profile.Difficulty);
        float height = TouchLayout ? 86 : 58;
        foreach (var button in new[] { _boost, _pulse })
        {
            button.CustomMinimumSize = new(button.CustomMinimumSize.X, height);
            button.OffsetTop = -height - 29;
        }
        _boost.Text = TouchLayout ? "冲刺" : "冲刺 / SHIFT";
        _pulse.Text = TouchLayout ? "脉冲" : "脉冲 / SPACE";
        View<Label>("Game/Guide").Text = TouchLayout ? "单指相对拖动 · 松手停留" : "躲开橙色光束 · 青色圆心是你的判定点";
        _feedback.Text = "进入裂隙 · 保持移动"; _feedback.Modulate = Colors.White; _feedbackTime = 3;
    }

    public void ShowLegacyEgg() => ShowPage("legacy");

    // Touch actions remain separate from steering so a second finger can trigger them.
    public bool HandleActionTouch(InputEventScreenTouch touch)
    {
        if (_screen != "game") return false;
        foreach (var button in new[] { _boost, _pulse, _pauseButton })
        {
            if (!button.GetGlobalRect().HasPoint(touch.Position)) continue;
            if (touch.Pressed && !touch.Canceled && !button.Disabled) button.EmitSignal(Button.SignalName.Pressed);
            return true;
        }
        return false;
    }

    public void UpdateGame(GameSession session, float delta)
    {
        if (_screen != "game") return;
        _score.Text = session.Score.ToString("D6");
        _time.Text = $"{(int)session.Elapsed / 60:00}:{(int)session.Elapsed % 60:00}   /   +{session.CreditsCollected} CR";
        int health = Math.Clamp(session.Health, 0, 5);
        _health.Text = "HULL   " + new string('■', health) + new string('□', 5 - health);
        _health.Modulate = session.Health <= 2 ? new Color("ff976f") : Colors.White;
        _wave.Text = $"SECTOR {session.Wave:00}  /  {DifficultyName(session.Difficulty)}";
        _boost.Text = session.BoostCooldown > 0 ? $"冲刺  {session.BoostCooldown:F1}s" : TouchLayout ? "冲刺" : "冲刺 / SHIFT";
        _boost.Disabled = session.BoostCooldown > 0;
        _pulse.Text = session.PulseCooldown > 0 ? $"脉冲  {session.PulseCooldown:F1}s" : TouchLayout ? "脉冲" : "脉冲 / SPACE";
        _pulse.Disabled = session.PulseCooldown > 0;
        _feedbackTime -= delta;
        _feedback.Modulate = new Color(1, 1, 1, Mathf.Clamp(_feedbackTime * 2, 0, 1));
    }

    public void OnFlightEvent(FlightEvent flightEvent)
    {
        if (_screen != "game") return;
        string message = flightEvent.Kind switch
        {
            FlightEventKind.Hit => "装甲受损 · 两秒保护", FlightEventKind.Collect => "补给已收集",
            FlightEventKind.NearMiss => "擦身而过  +50", FlightEventKind.Pulse => "脉冲释放 · 清除近身弹幕", _ => ""
        };
        if (message == "") return;
        _feedback.Text = message; _feedbackTime = 1.25f;
    }

    public void ShowPause(GameSession session)
    {
        ShowPage("pause");
        View<Label>("Pause/Center/Panel/Margin/Column/Summary").Text = $"{session.Score:N0} 分  ·  生存 {session.Elapsed:F1} 秒";
    }

    public void ShowResults(GameSession session, bool newRecord)
    {
        ShowPage("results");
        const string column = "Results/Center/Panel/Margin/Column/";
        View<Label>(column + "Title").Text = newRecord ? "新的飞行纪录" : "飞行结束";
        View<Label>(column + "Score").Text = session.Score.ToString("N0") + " 分";
        View<Label>(column + "Summary").Text = $"生存 {session.Elapsed:F1} 秒  /  擦弹 {session.NearMisses} 次";
        View<Label>(column + "Reward").Text = $"获得 +{session.Reward} CR  ·  持有 {_profile.Credits} CR";
    }

    public void Notice(string text)
    {
        var notice = View<Label>("Notice"); notice.Text = text; notice.Visible = !string.IsNullOrEmpty(text);
    }
}

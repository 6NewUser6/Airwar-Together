using Godot;
using System;
using System.Collections.Generic;
using Airwar.Core;
using Airwar.Persistence;

namespace Airwar.UI;

public partial class FlightHud : Control
{
    public event Action? StartRequested, MenuRequested, PauseRequested, ResumeRequested, BoostRequested, PulseRequested, SettingsChanged;
    public event Action<ShipDefinition>? PreviewRequested;
    private PilotProfile _profile = null!;
    private Control _page = null!;
    private Label? _score, _time, _health, _wave, _feedback;
    private Button? _boost, _pulse, _pauseButton;
    private string _screen = "menu";
    private int _preview;
    private int _legacyClicks;
    private ulong _lastLegacyClick;
    private float _feedbackTime;
    private Color _ink = new("eef4f6"), _muted = new("91a7ba"), _cyan = new("70deeb");
    public string Screen => _screen;
    public bool TouchLayout { get; set; } = OS.HasFeature("mobile");

    public void Initialize(PilotProfile profile)
    {
        _profile = profile;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Ignore;
        var theme = new Theme { DefaultFont = GD.Load<FontFile>("res://assets/fonts/NotoSansCJK-Regular.ttc"), DefaultFontSize = 18 };
        Theme = theme;
        ShowMenu();
    }

    private void Clear(string screen, bool backdrop = true)
    {
        foreach (var child in GetChildren()) child.QueueFree();
        _screen = screen; _score = _time = _health = _wave = _feedback = null; _boost = _pulse = _pauseButton = null;
        _page = new Control { MouseFilter = MouseFilterEnum.Ignore }; AddChild(_page);
        _page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (backdrop)
        {
            var gradient = new ColorRect { MouseFilter = MouseFilterEnum.Ignore };
            var shader = new Shader { Code = "shader_type canvas_item; void fragment() { float a = 0.96 * (1.0-smoothstep(0.26, 0.68, UV.x)); COLOR=vec4(0.015,0.04,0.075,a); }" };
            gradient.Material = new ShaderMaterial { Shader = shader }; _page.AddChild(gradient); gradient.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
    }

    private Label Text(string value, int size, Color? color = null)
    {
        var label = new Label { Text = value, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color ?? _ink);
        return label;
    }

    private static StyleBoxFlat Style(Color fill, Color border, int width = 1)
    {
        var style = new StyleBoxFlat { BgColor = fill, BorderColor = border, BorderWidthLeft = width, BorderWidthRight = width,
            BorderWidthTop = width, BorderWidthBottom = width, ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 8, ContentMarginBottom = 8 };
        style.CornerRadiusTopLeft = style.CornerRadiusBottomRight = 3; return style;
    }

    private Button Button(string caption, Action action, bool primary = false, float height = 46)
    {
        var button = new Button { Text = caption, CustomMinimumSize = new(0, height), MouseDefaultCursorShape = CursorShape.PointingHand };
        button.AddThemeFontSizeOverride("font_size", primary ? 22 : 17);
        button.AddThemeColorOverride("font_color", primary ? new Color("071925") : _ink);
        button.AddThemeColorOverride("font_hover_color", primary ? new Color("071925") : _cyan);
        button.AddThemeColorOverride("font_pressed_color", primary ? new Color("071925") : _cyan);
        button.AddThemeStyleboxOverride("normal", Style(primary ? _cyan : new Color("0c1e30"), primary ? _cyan : new Color("284252")));
        button.AddThemeStyleboxOverride("hover", Style(primary ? new Color("a1edf4") : new Color("173749"), _cyan));
        button.AddThemeStyleboxOverride("pressed", Style(primary ? new Color("49b8c8") : new Color("1e4658"), _cyan));
        button.AddThemeStyleboxOverride("focus", Style(new Color(0, 0, 0, 0), _cyan, 2));
        button.AddThemeStyleboxOverride("disabled", Style(new Color("0a1522"), new Color("1c303c")));
        button.Pressed += action; return button;
    }

    private VBoxContainer Column(float width = 470)
    {
        var column = new VBoxContainer { Position = new(64, 100), Size = new(width, 500) };
        column.AddThemeConstantOverride("separation", 13); _page.AddChild(column); return column;
    }

    private void Header(string right)
    {
        var header = Text("A / T     FLIGHT SYSTEMS", 15, _cyan); _page.AddChild(header); header.Position = new(64, 34);
        var status = Text(right, 14, _muted); _page.AddChild(status);
        status.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight); status.Position = new(Size.X - 430, 35); status.Size = new(366, 26); status.HorizontalAlignment = HorizontalAlignment.Right;
    }

    public static string DifficultyName(Difficulty difficulty) => difficulty switch { Difficulty.Easy => "简单", Difficulty.Normal => "普通", Difficulty.Hard => "困难", _ => "地狱" };

    public void ShowMenu()
    {
        Clear("menu"); Header($"{_profile.Callsign}     /     {_profile.Credits:D3} CR");
        var column = Column();
        column.AddChild(Text("01 / SURVIVAL", 15, _cyan));
        var title = Text("AIRWAR\nTOGETHER", 76); title.Name = "LegacyTitle"; title.AddThemeConstantOverride("line_spacing", -14);
        title.MouseFilter = MouseFilterEnum.Stop;
        title.GuiInput += input =>
        {
            if (input is not InputEventMouseButton click || !click.Pressed || click.ButtonIndex != MouseButton.Left) return;
            ulong now = Time.GetTicksMsec();
            _legacyClicks = now - _lastLegacyClick > 3000 ? 1 : _legacyClicks + 1; _lastLegacyClick = now;
            if (_legacyClicks >= 5) { _legacyClicks = 0; ShowLegacyEgg(); }
            AcceptEvent();
        };
        column.AddChild(title);
        column.AddChild(Text("在光束落下之前，寻找下一条生路。", 20, _muted));
        var record = Text($"最高纪录  {_profile.BestScores.GetValueOrDefault(_profile.Difficulty.ToString()):N0}     /     {_profile.UnlockedShips.Count:00} 架机体", 15, _muted);
        column.AddChild(record);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); column.AddChild(row);
        foreach (Difficulty difficulty in Enum.GetValues<Difficulty>())
        {
            var chosen = difficulty;
            var button = Button(DifficultyName(difficulty), () => { _profile.Difficulty = chosen; _profile.Save(); ShowMenu(); }, _profile.Difficulty == difficulty, 42);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill; button.AddThemeFontSizeOverride("font_size", 17); row.AddChild(button);
        }
        column.AddChild(Text(_profile.Difficulty == Difficulty.Inferno ? "一命挑战 · 极限节奏" : "躲避激光与弹幕 · 收集补给 · 活得更久", 14, _muted));
        column.AddChild(Button("开始生存    →", () => StartRequested?.Invoke(), true, 60));
        var links = new HBoxContainer(); links.AddThemeConstantOverride("separation", 10); column.AddChild(links);
        var hangar = Button("机库 / 12 架战机", ShowHangar); hangar.SizeFlagsHorizontal = SizeFlags.ExpandFill; links.AddChild(hangar);
        var settings = Button("设置", ShowSettings); settings.CustomMinimumSize = new(110, 46); links.AddChild(settings);
        var ship = ShipCatalog.Find(_profile.SelectedShip);
        var shipLabel = Text($"{ship.Name}\n{ship.Role}", 20, _cyan); _page.AddChild(shipLabel); shipLabel.Position = new(Size.X * .71f, Size.Y * .78f);
        Footer(TouchLayout ? "单指拖动飞行 · 右侧按钮释放技能" : "WASD / 方向键 / 鼠标移动    SHIFT 冲刺    SPACE 清弹    ESC 暂停");
        if (!string.IsNullOrEmpty(_profile.RecoveryNotice)) Notice(_profile.RecoveryNotice);
    }

    public void ShowLegacyEgg()
    {
        var column = Modal("legacy", "来自最初的信号");
        var texture = new TextureRect { Texture = GD.Load<Texture2D>("res://assets/legacy/golden_title.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new(390, 138), MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(texture);
        column.AddChild(Text("最初，我们只是想躲开那一道射线。", 18, new Color("e2c889")));
        column.AddChild(Text("旧时的鎏金招牌，依然在这片星空里。", 15, _muted));
        column.AddChild(Button("返回飞行系统    →", ShowMenu, true, 54));
    }

    private void Footer(string text)
    {
        var footer = Text(text, 14, _muted); _page.AddChild(footer);
        footer.Position = new(64, Size.Y - 47);
    }

    public void ShowHangar()
    {
        Clear("hangar"); Header($"机库     /     {_profile.Credits:D3} CR");
        var column = Column(510);
        column.AddChild(Text("02 / HANGAR", 15, _cyan));
        column.AddChild(Text("选择你的战机", 40));
        column.AddChild(Text("12 架机体，统一性能。外观由你决定。", 17, _muted));
        var grid = new GridContainer { Columns = 3 }; grid.AddThemeConstantOverride("h_separation", 8); grid.AddThemeConstantOverride("v_separation", 8); column.AddChild(grid);
        for (int i = 0; i < ShipCatalog.All.Length; i++)
        {
            int index = i; var ship = ShipCatalog.All[i];
            bool owned = _profile.UnlockedShips.Contains(ship.Id);
            var button = Button((owned ? "" : "+ ") + ship.Name, () => { _preview = index; ShowHangar(); }, _preview == i, 46);
            button.CustomMinimumSize = new(155, 46); button.AddThemeFontSizeOverride("font_size", 17); grid.AddChild(button);
        }
        var selected = ShipCatalog.All[_preview]; bool unlocked = _profile.UnlockedShips.Contains(selected.Id);
        column.AddChild(Text(selected.Role, 20, selected.Color));
        column.AddChild(Text(unlocked ? "已解锁 · 进入战场前选择机体" : $"解锁需要 {selected.Cost} CR · 生存与拾取获得金币", 15, _muted));
        var select = Button(unlocked ? (selected.Id == _profile.SelectedShip ? "已装备    ✓" : "装备战机") : $"解锁战机    {selected.Cost} CR", () =>
        {
            if (unlocked) { _profile.SelectedShip = selected.Id; _profile.Save(); }
            else _profile.Unlock(selected.Id);
            ShowHangar();
        }, true, 54);
        select.Disabled = !unlocked && _profile.Credits < selected.Cost; column.AddChild(select);
        column.AddChild(Button("← 返回主菜单", () => { PreviewRequested?.Invoke(ShipCatalog.Find(_profile.SelectedShip)); ShowMenu(); }));
        PreviewRequested?.Invoke(selected);
        Footer("选择属于你的机体，准备进入裂隙。");
    }

    public void ShowSettings()
    {
        Clear("settings"); Header("PREFERENCES / 本地保存");
        var column = Column(460); column.AddChild(Text("03 / SETTINGS", 15, _cyan)); column.AddChild(Text("飞行偏好", 44));
        column.AddChild(Text("飞行员代号", 16, _muted));
        var callsign = new LineEdit { Text = _profile.Callsign, MaxLength = 16, CustomMinimumSize = new(0, 44) }; column.AddChild(callsign);
        callsign.TextChanged += value => { _profile.Callsign = value; };
        AddSlider(column, "音乐", _profile.MusicVolume, 0, 1, value => _profile.MusicVolume = value);
        AddSlider(column, "音效", _profile.SoundVolume, 0, 1, value => _profile.SoundVolume = value);
        AddSlider(column, "触控灵敏度", _profile.TouchSensitivity, .6f, 1.8f, value => _profile.TouchSensitivity = value);
        var effects = new CheckButton { Text = "精简特效（减少粒子与震动）", ButtonPressed = _profile.ReducedEffects, CustomMinimumSize = new(0, 40) }; column.AddChild(effects);
        effects.Toggled += value => { _profile.ReducedEffects = value; Changed(); };
        column.AddChild(Button("保存并返回    →", () => { Changed(); ShowMenu(); }, true, 54));
        Footer("本地档案保存金币、最高纪录、机体与偏好。");
    }

    private void AddSlider(VBoxContainer parent, string name, float value, float min, float max, Action<float> update)
    {
        var row = new HBoxContainer(); parent.AddChild(row); var label = Text(name, 16, _muted); label.CustomMinimumSize = new(120, 30); row.AddChild(label);
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = .05, Value = value, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new(230, 32) }; row.AddChild(slider);
        slider.ValueChanged += next => { update((float)next); Changed(); };
    }

    private void Changed() { _profile.Save(); SettingsChanged?.Invoke(); }

    public void ShowGame()
    {
        Clear("game", false);
        _health = Text("HULL   ■ ■ ■ ■ ■", 20, _cyan); _health.Position = new(38, 28); _page.AddChild(_health);
        var small = Text(_profile.Callsign + "  /  " + DifficultyName(_profile.Difficulty), 13, _muted); small.Position = new(38, 60); _page.AddChild(small);
        _score = Text("000000", 36); _score.Position = new(Size.X - 230, 18); _score.Size = new(180, 52); _score.HorizontalAlignment = HorizontalAlignment.Right; _page.AddChild(_score);
        _time = Text("00:00", 15, _muted); _time.Position = new(Size.X - 230, 68); _time.Size = new(180, 30); _time.HorizontalAlignment = HorizontalAlignment.Right; _page.AddChild(_time);
        _wave = Text("SECTOR 01", 16, _muted); _wave.Position = new(Size.X / 2 - 95, 33); _wave.Size = new(190, 30); _wave.HorizontalAlignment = HorizontalAlignment.Center; _page.AddChild(_wave);
        _pauseButton = Button("Ⅱ", () => PauseRequested?.Invoke()); _pauseButton.Position = new(Size.X / 2 - 28, 71); _pauseButton.Size = new(56, 42); _pauseButton.FocusMode = FocusModeEnum.None; _page.AddChild(_pauseButton);
        float actionHeight = TouchLayout ? 86 : 58;
        _boost = Button(TouchLayout ? "冲刺" : "冲刺 / SHIFT", () => BoostRequested?.Invoke()); _boost.Position = new(Size.X - 378, Size.Y - actionHeight - 29); _boost.Size = new(158, actionHeight); _page.AddChild(_boost);
        _pulse = Button(TouchLayout ? "脉冲" : "脉冲 / SPACE", () => PulseRequested?.Invoke()); _pulse.Position = new(Size.X - 204, Size.Y - actionHeight - 29); _pulse.Size = new(166, actionHeight); _page.AddChild(_pulse);
        _boost.FocusMode = _pulse.FocusMode = FocusModeEnum.None;
        var guide = Text(TouchLayout ? "单指相对拖动 · 松手停留" : "躲开橙色光束 · 青色圆心是你的判定点", 14, _muted); guide.Position = new(38, Size.Y - 58); _page.AddChild(guide);
        _feedback = Text("进入裂隙 · 保持移动", 23, _cyan); _feedback.Size = new(500, 45); _feedback.Position = new(Size.X / 2 - 250, Size.Y - 147); _feedback.HorizontalAlignment = HorizontalAlignment.Center; _page.AddChild(_feedback); _feedbackTime = 3;
    }

    // TouchScreenButton semantics for the two in-flight actions: any finger can
    // trigger these while the first finger is still steering. Menus use normal GUI.
    public bool HandleActionTouch(InputEventScreenTouch touch)
    {
        if (_screen != "game") return false;
        foreach (var button in new[] { _boost, _pulse, _pauseButton })
        {
            if (button == null || !button.GetGlobalRect().HasPoint(touch.Position)) continue;
            if (touch.Pressed && !touch.Canceled && !button.Disabled) button.EmitSignal(Godot.Button.SignalName.Pressed);
            return true;
        }
        return false;
    }

    public void UpdateGame(GameSession session, float delta)
    {
        if (_screen != "game") return;
        if (_score != null) _score.Text = session.Score.ToString("D6");
        if (_time != null) _time.Text = $"{(int)session.Elapsed / 60:00}:{(int)session.Elapsed % 60:00}   /   +{session.CreditsCollected} CR";
        if (_health != null) { _health.Text = "HULL   " + new string('■', Math.Max(0, session.Health)) + new string('□', 5 - Math.Max(0, session.Health)); _health.Modulate = session.Health <= 2 ? new Color("ff976f") : Colors.White; }
        if (_wave != null) _wave.Text = $"SECTOR {session.Wave:00}  /  {DifficultyName(session.Difficulty)}";
        if (_boost != null) { _boost.Text = session.BoostCooldown > 0 ? $"冲刺  {session.BoostCooldown:F1}s" : TouchLayout ? "冲刺" : "冲刺 / SHIFT"; _boost.Disabled = session.BoostCooldown > 0; }
        if (_pulse != null) { _pulse.Text = session.PulseCooldown > 0 ? $"脉冲  {session.PulseCooldown:F1}s" : TouchLayout ? "脉冲" : "脉冲 / SPACE"; _pulse.Disabled = session.PulseCooldown > 0; }
        _feedbackTime -= delta;
        if (_feedback != null) _feedback.Modulate = new Color(1, 1, 1, Mathf.Clamp(_feedbackTime * 2, 0, 1));
    }

    public void OnFlightEvent(FlightEvent flightEvent)
    {
        if (_feedback == null) return;
        string text = flightEvent.Kind switch { FlightEventKind.Hit => "装甲受损 · 两秒保护", FlightEventKind.Collect => "补给已收集", FlightEventKind.NearMiss => "擦身而过  +50", FlightEventKind.Pulse => "脉冲释放 · 清除近身弹幕", _ => "" };
        if (text == "") return; _feedback.Text = text; _feedbackTime = 1.25f;
    }

    private VBoxContainer Modal(string section, string title)
    {
        Clear(section, false);
        var dim = new ColorRect { Color = new(0.01f, .035f, .065f, .8f), MouseFilter = MouseFilterEnum.Stop }; _page.AddChild(dim); dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer { Position = new(Size.X / 2 - 240, Size.Y / 2 - 245), Size = new(480, 480) };
        panel.AddThemeStyleboxOverride("panel", Style(new Color("0b1e30"), new Color("345263")));
        var margin = new MarginContainer(); foreach (string side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 32); panel.AddChild(margin);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 14); margin.AddChild(column); _page.AddChild(panel);
        column.AddChild(Text(section.ToUpper() + " / FLIGHT RECORD", 14, _cyan)); column.AddChild(Text(title, 38)); return column;
    }

    public void ShowPause(GameSession session)
    {
        var column = Modal("pause", "飞行暂停");
        column.AddChild(Text($"{session.Score:N0} 分  ·  生存 {session.Elapsed:F1} 秒", 18, _muted));
        column.AddChild(Button("继续飞行    →", () => ResumeRequested?.Invoke(), true, 56));
        column.AddChild(Button("重新开始", () => StartRequested?.Invoke()));
        column.AddChild(Button("结束本局并返回", () => MenuRequested?.Invoke()));
        column.AddChild(Text("退出本局会保存已获得的金币与成绩。", 13, _muted));
    }

    public void ShowResults(GameSession session, bool newRecord)
    {
        var column = Modal("results", newRecord ? "新的飞行纪录" : "飞行结束");
        column.AddChild(Text(session.Score.ToString("N0") + " 分", 48, _cyan));
        column.AddChild(Text($"生存 {session.Elapsed:F1} 秒  /  擦弹 {session.NearMisses} 次", 17, _muted));
        column.AddChild(Text($"获得 +{session.Reward} CR  ·  持有 {_profile.Credits} CR", 18));
        column.AddChild(Button("再次出击    →", () => StartRequested?.Invoke(), true, 56));
        column.AddChild(Button("返回主菜单", () => MenuRequested?.Invoke()));
    }

    public void Notice(string text)
    {
        var notice = Text(text, 15, new Color("ffbd89")); _page.AddChild(notice); notice.Position = new(64, Size.Y - 78);
    }
}

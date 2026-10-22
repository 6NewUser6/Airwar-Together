using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Airwar.Core;
using Airwar.Persistence;

namespace Airwar.Arena;

public partial class ArenaHud : Control
{
    public event Action? HostRequested, JoinRequested, BackRequested, LeaveRequested, MenuRequested, ResumeRequested, WarpRequested, CameraRequested;
    public event Action<int>? PreviewRequested;
    private Control _lobby = null!, _flight = null!, _menu = null!;
    private Label _notice = null!;
    private OptionButton _destination = null!;
    private HSlider _throttle = null!;
    private readonly Dictionary<string, Button> _actions = new();
    private bool _wired, _scoresPinned;
    private float _noticeTime;
    public bool TouchLayout { get; set; }
    public string Address => GetNode<LineEdit>("Lobby/Panel/Column/Address").Text.Trim();
    public int Port => int.TryParse(GetNode<LineEdit>("Lobby/Panel/Column/Port").Text, out int port) ? port : 0;
    public string Callsign => GetNode<LineEdit>("Lobby/Panel/Column/Callsign").Text.Trim();
    public string SelectedShipId => ShipCatalog.All[Mathf.Clamp(GetNode<OptionButton>("Lobby/Panel/Column/Ship").Selected, 0, ShipCatalog.All.Length - 1)].Id;
    public int Destination => _destination.Selected;
    public float Throttle { get => (float)_throttle.Value; set => _throttle.SetValueNoSignal(value); }
    public bool MenuVisible => _menu.Visible;
    public bool InLobby => _lobby.Visible;

    public override void _Ready() => Bind();
    private void Bind()
    {
        if (_wired) return; _wired = true;
        _lobby = GetNode<Control>("Lobby"); _flight = GetNode<Control>("Flight"); _menu = GetNode<Control>("Menu");
        _notice = GetNode<Label>("Notice"); _destination = GetNode<OptionButton>("Flight/Navigation/Destination");
        _throttle = GetNode<HSlider>("Flight/Throttle/Slider");
        foreach (string name in new[] { "Fire", "Boost", "RollLeft", "RollRight" }) _actions[name] = GetNode<Button>("Flight/" + name);
        GetNode<Button>("Lobby/Panel/Column/Host").Pressed += () => HostRequested?.Invoke();
        GetNode<Button>("Lobby/Panel/Column/Join").Pressed += () => JoinRequested?.Invoke();
        GetNode<Button>("Lobby/Panel/Column/Back").Pressed += () => BackRequested?.Invoke();
        GetNode<Button>("Flight/Top/Menu").Pressed += () => MenuRequested?.Invoke();
        GetNode<Button>("Flight/Navigation/Warp").Pressed += () => WarpRequested?.Invoke();
        GetNode<Button>("Flight/Camera").Pressed += () => CameraRequested?.Invoke();
        GetNode<Button>("Flight/Scores").Pressed += () => _scoresPinned = !_scoresPinned;
        GetNode<Button>("Menu/Center/Panel/Column/Resume").Pressed += () => ResumeRequested?.Invoke();
        GetNode<Button>("Menu/Center/Panel/Column/Leave").Pressed += () => LeaveRequested?.Invoke();
        GetNode<Button>("Menu/Center/Panel/Column/Back").Pressed += () => BackRequested?.Invoke();
        var preview = GetNode<OptionButton>("Lobby/Panel/Column/Sector");
        for (int i = 0; i < SectorCatalog.Count; i++) { preview.AddItem(SectorCatalog.Names[i]); _destination.AddItem(SectorCatalog.Names[i]); }
        preview.ItemSelected += value => PreviewRequested?.Invoke((int)value);
        _destination.Select(3);
        foreach (var ship in ShipCatalog.All) GetNode<OptionButton>("Lobby/Panel/Column/Ship").AddItem(ship.Name + " / " + ship.Role);
    }

    public void Initialize(PilotProfile profile, bool mobile)
    {
        Bind(); TouchLayout = mobile;
        GetNode<LineEdit>("Lobby/Panel/Column/Callsign").Text = profile.Callsign;
        GetNode<OptionButton>("Lobby/Panel/Column/Ship").Select(Math.Max(0, Array.FindIndex(ShipCatalog.All, x => x.Id == profile.SelectedShip)));
        GetNode<Label>("Flight/Help").Text = mobile
            ? "左侧拖动转向 · 多指同时射击/推进 · 选择目的地后跃迁"
            : "右键按住转向 · W/S 油门 · A/D 滚转 · Q/E 偏航 · ↑↓俯仰 · 左键/空格射击 · Shift 推进 · J 跃迁 · V 视角";
        foreach (var button in _actions.Values) button.Visible = mobile;
        ShowLobby();
    }

    public void SetEndpoint(string address, int port)
    {
        GetNode<LineEdit>("Lobby/Panel/Column/Address").Text = address;
        GetNode<LineEdit>("Lobby/Panel/Column/Port").Text = port.ToString();
    }

    public void ShowLobby() { _lobby.Show(); _flight.Hide(); _menu.Hide(); SetConnecting(false); }
    public void ShowFlight() { _lobby.Hide(); _flight.Show(); _menu.Hide(); }
    public void ShowMenu(bool visible) { _menu.Visible = visible; }
    public void SetConnecting(bool connecting)
    {
        GetNode<Button>("Lobby/Panel/Column/Host").Disabled = connecting;
        GetNode<Button>("Lobby/Panel/Column/Join").Disabled = connecting;
    }
    public void Notice(string message)
    {
        _notice.Text = message; _noticeTime = 7; _notice.Show();
        GetNode<Label>("Lobby/Panel/Column/Status").Text = message;
    }

    public Dictionary<string, Rect2> ActionRects() => _actions.Where(x => x.Value.IsVisibleInTree()).ToDictionary(x => x.Key, x => x.Value.GetGlobalRect());
    public List<Rect2> ReservedRects() => new[] { "Flight/Navigation", "Flight/Throttle", "Flight/Top/Menu", "Flight/Camera", "Flight/Scores" }
        .Select(path => GetNode<Control>(path).GetGlobalRect()).ToList();
    public bool PointerOnControl() => ReservedRects().Concat(ActionRects().Values).Any(rect => rect.HasPoint(GetGlobalMousePosition()));

    public void UpdateFlight(ArenaNetwork network, float dt)
    {
        if (_noticeTime > 0) { _noticeTime -= dt; if (_noticeTime <= 0) _notice.Hide(); }
        GetNode<Label>("Flight/Top/Room").Text = $"ORBITAL FRONTIER    /    {network.PlayerCount:00}/32    {network.LatencyMs:0} ms";
        if (!network.Ships.TryGetValue(network.LocalPeerId, out var ship)) return;
        GetNode<Label>("Flight/Top/Sector").Text = SectorCatalog.Name(ship.Sector);
        string altitude = ship.Sector == 3 ? "轨道自由飞行" : $"高度 {ship.Position.Y - ArenaSimulation.SurfaceHeight(ship.Sector, ship.Position.X, ship.Position.Z):0000} m";
        GetNode<Label>("Flight/Telemetry").Text = $"{ship.Speed * 3.6f:0000} km/h\n{altitude}\nX {ship.Position.X / 1000:0.0}  Z {ship.Position.Z / 1000:0.0} km";
        GetNode<Label>("Flight/Vitals").Text = $"机体  {Mathf.CeilToInt(ship.Health):000}    护盾  {Mathf.CeilToInt(ship.Shield):00}\n能量  {Mathf.CeilToInt(ship.Energy):000}    击落  {ship.Kills} / 损失 {ship.Deaths}";
        GetNode<ProgressBar>("Flight/Health").Value = ship.Health;
        GetNode<ProgressBar>("Flight/Energy").Value = ship.Energy;
        GetNode<Label>("Flight/Respawn").Text = !ship.Alive ? $"机体损毁\n{ship.RespawnRemaining:0.0} 秒后重新出击" :
            ship.WarpCharge > 0 ? $"跃迁充能  {ship.WarpCharge / 3 * 100:00}%" : "";
        GetNode<Label>("Flight/Navigation/Hint").Text = $"目的地  /  {SectorCatalog.Descriptions[_destination.Selected]}";
        GetNode<Label>("Flight/Scoreboard").Text = "呼号                         击落 / 损失\n" + string.Join("\n", network.Ships.Values
            .OrderByDescending(x => x.Kills).ThenBy(x => x.Deaths).Take(32).Select(x => $"{x.Callsign,-16}  {x.Kills,3} / {x.Deaths,-3}"));
        GetNode<Label>("Flight/Scoreboard").Visible = _scoresPinned || (Input.IsPhysicalKeyPressed(Key.Tab) && !TouchLayout);
    }
}

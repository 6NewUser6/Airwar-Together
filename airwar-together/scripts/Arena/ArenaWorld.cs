using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Airwar.Core;

namespace Airwar.Arena;

/// <summary>Displays authored sector scenes and server-owned actors, in metres.</summary>
public partial class ArenaWorld : Node3D
{
    [Export] public Mesh ShotMesh { get; set; } = null!;
    [Export] public Material ShotMaterial { get; set; } = null!;
    public Camera3D Camera { get; private set; } = null!;
    public bool Cockpit { get; set; }
    public int DisplayedSector { get; private set; }

    private Node3D _sector = null!, _actors = null!, _projectiles = null!, _previewShip = null!;
    private readonly Dictionary<int, ShipView> _ships = new();
    private readonly Dictionary<int, MeshInstance3D> _shots = new();
    private readonly Stack<MeshInstance3D> _idleShots = new();
    private readonly Dictionary<string, PackedScene> _shipScenes = new();
    private readonly PackedScene?[] _sectorScenes = new PackedScene?[SectorCatalog.Count];
    private Font _font = null!;
    private int _cameraPeer;
    private float _time;
    private bool _hasCameraTarget;

    private sealed class ShipView
    {
        public Node3D Root = null!, Model = null!;
        public Node3D? LeftExhaust, RightExhaust;
        public Vector3 LeftScale, RightScale;
        public Label3D Nameplate = null!;
        public string ShipId = "";
        public int Sector;
    }

    public override void _Ready()
    {
        Camera = GetNode<Camera3D>("Camera");
        _sector = GetNode<Node3D>("Sector");
        _actors = GetNode<Node3D>("Actors");
        _projectiles = GetNode<Node3D>("Projectiles");
        _previewShip = GetNode<Node3D>("PreviewShip");
        _font = GD.Load<Font>("res://assets/fonts/NotoSansCJK-Regular.ttc");
        _sectorScenes[0] = GD.Load<PackedScene>(SectorCatalog.ScenePaths[0]);
        ApplyPreviewCamera();
    }

    public void SetPreviewSector(int sector)
    {
        if (!IsNodeReady()) return;
        LoadSector(Mathf.Clamp(sector, 0, SectorCatalog.Count - 1));
        _previewShip.Visible = true;
        foreach (var ship in _ships.Values) ship.Root.Visible = false;
        foreach (var shot in _shots.Values) shot.Visible = false;
        _hasCameraTarget = false;
        ApplyPreviewCamera();
    }

    private void ApplyPreviewCamera()
    {
        var floor = DisplayedSector == 3 ? 0 : SectorCatalog.FlightFloor(DisplayedSector, 0, 180);
        _previewShip.Position = new Vector3(0, floor + 320, 180);
        _previewShip.Scale = Vector3.One * 4;
        Camera.Position = _previewShip.Position + new Vector3(38, 18, 72);
        Camera.LookAt(_previewShip.Position + new Vector3(0, 0, -220));
        Camera.Fov = 76;
    }

    private void LoadSector(int sector)
    {
        if (_sector != null && DisplayedSector == sector) return;
        _sectorScenes[sector] ??= GD.Load<PackedScene>(SectorCatalog.ScenePaths[sector]);
        var scene = _sectorScenes[sector]!.Instantiate<Node3D>();
        scene.Name = "Sector";
        if (_sector != null)
        {
            RemoveChild(_sector);
            _sector.QueueFree();
        }
        _sector = scene;
        AddChild(_sector);
        MoveChild(_sector, 0);
        DisplayedSector = sector;
        _hasCameraTarget = false;
    }

    public void Render(IReadOnlyDictionary<int, ArenaShip> ships, IReadOnlyList<ArenaShot> shots, int localPeerId, float dt)
    {
        if (!IsNodeReady()) return;
        _time += dt;
        bool hasLocal = ships.TryGetValue(localPeerId, out var local);
        if (hasLocal) LoadSector(local!.Sector);
        _previewShip.Visible = !hasLocal;
        float blend = 1 - Mathf.Exp(-Mathf.Max(0, dt) * 15);
        foreach (var state in ships.Values)
        {
            if (!_ships.TryGetValue(state.PeerId, out var view))
            {
                view = CreateShip(state);
                _ships.Add(state.PeerId, view);
            }
            else if (view.ShipId != state.ShipId) ReplaceModel(view, state.ShipId);
            bool sameSector = state.Sector == DisplayedSector;
            view.Root.Visible = sameSector && state.Alive;
            if (!sameSector) continue;
            bool teleport = view.Sector != state.Sector || view.Root.Position.DistanceSquaredTo(state.Position) > 2250000;
            view.Sector = state.Sector;
            var predicted = state.Position + state.Velocity * .025f;
            view.Root.Position = teleport ? state.Position : view.Root.Position.Lerp(predicted, blend);
            var rotation = state.Rotation.Normalized();
            view.Root.Quaternion = teleport ? rotation : view.Root.Quaternion.Slerp(rotation, blend);
            view.Model.Visible = !(state.PeerId == localPeerId && Cockpit);
            view.Nameplate.Visible = state.PeerId != localPeerId;
            string caption = $"{state.Callsign}  {Mathf.CeilToInt(state.Health)}";
            if (caption != view.Nameplate.Text) view.Nameplate.Text = caption;
            float length = (state.Boosting ? 2.8f : 1.1f) + .1f * Mathf.Sin(_time * 45);
            if (view.LeftExhaust != null) view.LeftExhaust.Scale = view.LeftScale * new Vector3(1, 1, length);
            if (view.RightExhaust != null) view.RightExhaust.Scale = view.RightScale * new Vector3(1, 1, length);
        }
        foreach (int id in _ships.Keys.Where(id => !ships.ContainsKey(id)).ToArray())
        {
            _ships[id].Root.QueueFree();
            _ships.Remove(id);
        }
        var activeShots = new HashSet<int>();
        foreach (var state in shots)
            if (state.Sector == DisplayedSector) activeShots.Add(state.Id);
        // Keep the small set of projectile instances alive across bursts and sector changes.
        // Expired shots return to the pool before replacements are assigned this frame.
        foreach (int id in _shots.Keys.Where(id => !activeShots.Contains(id)).ToArray())
        {
            var idle = _shots[id];
            idle.Visible = false;
            _idleShots.Push(idle);
            _shots.Remove(id);
        }
        foreach (var state in shots)
        {
            if (state.Sector != DisplayedSector) continue;
            if (!_shots.TryGetValue(state.Id, out var visual))
            {
                if (_idleShots.TryPop(out var idle)) visual = idle;
                else
                {
                    visual = new MeshInstance3D { Mesh = ShotMesh, MaterialOverride = ShotMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                    _projectiles.AddChild(visual);
                }
                _shots.Add(state.Id, visual);
            }
            visual.Visible = true;
            visual.Position = state.Position;
            if (state.Velocity.LengthSquared() > 1) visual.LookAt(state.Position + state.Velocity, Mathf.Abs(state.Velocity.Normalized().Dot(Vector3.Up)) > .98f ? Vector3.Right : Vector3.Up);
        }
        if (hasLocal && _ships.TryGetValue(localPeerId, out var localView)) UpdateCamera(local!, localView, dt);
    }

    private ShipView CreateShip(ArenaShip state)
    {
        var view = new ShipView { Root = new Node3D { Name = $"Pilot{state.PeerId}", Position = state.Position, Quaternion = state.Rotation }, Sector = state.Sector };
        _actors.AddChild(view.Root);
        ReplaceModel(view, state.ShipId);
        view.Nameplate = new Label3D
        {
            Position = new Vector3(0, 15, 0), Font = _font, FontSize = 40, PixelSize = .075f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, OutlineSize = 8,
            Modulate = new Color("b4f2ff"), VisibilityRangeEnd = 3000,
        };
        view.Root.AddChild(view.Nameplate);
        return view;
    }

    private void ReplaceModel(ShipView view, string shipId)
    {
        if (view.Model != null)
        {
            view.Root.RemoveChild(view.Model);
            view.Model.QueueFree();
        }
        if (!_shipScenes.TryGetValue(shipId, out var scene))
        {
            scene = ShipCatalog.Find(shipId).VisualScene;
            _shipScenes[shipId] = scene;
        }
        view.Model = scene.Instantiate<Node3D>();
        view.Model.Scale *= 4;
        view.Root.AddChild(view.Model);
        view.ShipId = shipId;
        view.LeftExhaust = view.Model.GetNodeOrNull<Node3D>("LeftExhaust");
        view.RightExhaust = view.Model.GetNodeOrNull<Node3D>("RightExhaust");
        view.LeftScale = view.LeftExhaust?.Scale ?? Vector3.One;
        view.RightScale = view.RightExhaust?.Scale ?? Vector3.One;
        var shield = view.Model.GetNodeOrNull<Node3D>("Shield");
        if (shield != null) shield.Visible = false;
    }

    private void UpdateCamera(ArenaShip state, ShipView view, float dt)
    {
        var basis = view.Root.Basis.Orthonormalized();
        var offset = Cockpit ? new Vector3(0, 1.6f, -2.0f) : new Vector3(0, state.Boosting ? 12 : 9, state.Boosting ? 56 : 42);
        var target = view.Root.Position + basis * offset;
        if (DisplayedSector != 3)
            target.Y = Mathf.Max(target.Y, SectorCatalog.FlightFloor(DisplayedSector, target.X, target.Z) + 8);
        float follow = 1 - Mathf.Exp(-dt * (Cockpit ? 28 : 9));
        bool reset = !_hasCameraTarget || _cameraPeer != state.PeerId || Camera.Position.DistanceSquaredTo(target) > 1000000;
        Camera.Position = reset ? target : Camera.Position.Lerp(target, follow);
        if (DisplayedSector != 3)
        {
            var safe = Camera.Position;
            safe.Y = Mathf.Max(safe.Y, SectorCatalog.FlightFloor(DisplayedSector, safe.X, safe.Z) + 8);
            Camera.Position = safe;
        }
        var aim = view.Root.Position - basis.Z * 200 + basis.Y * (Cockpit ? 1.6f : 5);
        Camera.LookAt(aim, basis.Y);
        Camera.Fov = Mathf.Lerp(Camera.Fov, Cockpit ? 87 : state.Boosting ? 88 : 76, follow);
        _hasCameraTarget = true;
        _cameraPeer = state.PeerId;
    }
}

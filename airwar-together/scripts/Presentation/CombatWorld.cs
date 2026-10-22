using Godot;
using System;
using System.Collections.Generic;
using Airwar.Core;

namespace Airwar.Presentation;

/// <summary>Updates authored world and effect scenes from the simulation state.</summary>
public partial class CombatWorld : Node3D
{
    [ExportGroup("Reusable effects")]
    [Export] public PackedScene BeamScene { get; set; } = null!;
    [Export] public PackedScene ProjectileScene { get; set; } = null!;
    [Export] public PackedScene RepairSupplyScene { get; set; } = null!;
    [Export] public PackedScene CreditsSupplyScene { get; set; } = null!;
    [Export] public PackedScene SparkScene { get; set; } = null!;
    [Export] public Material LiveBeamMaterial { get; set; } = null!;
    [Export] public Material WarningBeamMaterial { get; set; } = null!;
    public Camera3D Camera { get; private set; } = null!;
    public PlaneVisual Plane { get; private set; } = null!;
    public bool IsMenu { get; set; } = true;
    public bool ReducedEffects { get; set; }
    private readonly Dictionary<int, Node3D> _entities = new();
    private readonly List<(Node3D Node, Vector3 Velocity, float Life)> _sparks = new();
    private readonly List<Node3D> _scenery = new();
    private Node3D _pad = null!, _boundary = null!, _entityRoot = null!, _sparkRoot = null!;
    private Vector3 _cameraHome, _planeHome, _planeScale, _planeRotation;
    private float _time, _shake;
    private readonly Random _random = new(711);

    public override void _Ready()
    {
        Camera = GetNode<Camera3D>("Camera");
        Plane = GetNode<PlaneVisual>("PlayerPlane");
        _pad = GetNode<Node3D>("HangarPad");
        _boundary = GetNode<Node3D>("Boundary");
        _entityRoot = GetNode<Node3D>("Entities");
        _sparkRoot = GetNode<Node3D>("Sparks");
        foreach (var child in GetNode<Node3D>("Scenery").GetChildren())
            if (child is Node3D scenery) _scenery.Add(scenery);
        _cameraHome = Camera.Position;
        _planeHome = Plane.Position;
        _planeScale = Plane.Scale;
        _planeRotation = Plane.Rotation;
    }

    public Vector3 ScreenToFlight(Vector2 position)
    {
        var target = ProjectToFlightPlane(position);
        return new(Mathf.Clamp(target.X, -GameSession.HalfWidth, GameSession.HalfWidth), GameSession.FlightHeight, Mathf.Clamp(target.Z, -GameSession.HalfDepth, GameSession.HalfDepth));
    }

    public Vector3 ScreenDeltaToFlight(Vector2 from, Vector2 to)
    {
        // Relative touch input must retain motion outside the projected arena.
        return ProjectToFlightPlane(to) - ProjectToFlightPlane(from);
    }

    private Vector3 ProjectToFlightPlane(Vector2 position)
    {
        var origin = Camera.ProjectRayOrigin(position);
        var direction = Camera.ProjectRayNormal(position);
        float distance = (GameSession.FlightHeight - origin.Y) / direction.Y;
        return origin + direction * distance;
    }

    public void ClearEntities()
    {
        foreach (var node in _entities.Values) node.QueueFree();
        _entities.Clear();
        foreach (var spark in _sparks) spark.Node.QueueFree();
        _sparks.Clear();
    }

    public void Render(GameSession? session, float dt)
    {
        _time += dt;
        float aspect = GetViewport().GetVisibleRect().Size.X / Mathf.Max(1, GetViewport().GetVisibleRect().Size.Y);
        Camera.Size = IsMenu ? Mathf.Max(20.4f, 26f / aspect) : Mathf.Max(16.3f, 23.5f / aspect);
        _pad.Visible = IsMenu;
        _boundary.Visible = !IsMenu;
        Camera.Position = _cameraHome + new Vector3(Mathf.Sin(_time * 38) * _shake, 0, Mathf.Cos(_time * 29) * _shake);
        _shake = Mathf.MoveToward(_shake, 0, dt * 2.2f);
        foreach (var tower in _scenery)
        {
            tower.Position += Vector3.Back * dt * (IsMenu ? .4f : 3.2f);
            if (tower.Position.Z > 45) tower.Position = new(tower.Position.X, -1, -60);
        }
        if (IsMenu)
        {
            Plane.Position = _planeHome + Vector3.Up * Mathf.Sin(_time * 1.2f) * .15f;
            Plane.Scale = _planeScale;
            Plane.Animate(_time, Vector3.Zero, 0, false);
            Plane.Rotation = _planeRotation + new Vector3(0, Mathf.Sin(_time * .35f) * .16f, 0);
        }
        else if (session != null)
        {
            Plane.Position = Plane.Position.Lerp(session.PlayerPosition, Mathf.Min(1, dt * 30));
            Plane.Scale = Vector3.One * .42f;
            Plane.Animate(_time, session.PlayerVelocity, session.Invulnerability, session.BoostRemaining > 0);
            var alive = new HashSet<int>();
            foreach (var threat in session.Threats)
            {
                alive.Add(threat.Id);
                if (!_entities.TryGetValue(threat.Id, out var node))
                {
                    node = (threat.Kind == ThreatKind.Beam ? BeamScene : ProjectileScene).Instantiate<Node3D>();
                    _entityRoot.AddChild(node);
                    _entities.Add(threat.Id, node);
                    if (threat.Kind == ThreatKind.Beam)
                    {
                        node.SetMeta("warning_width", node.GetNode<MeshInstance3D>("Core").Scale.X);
                        var trace = node.GetNode<MeshInstance3D>("GroundTrace");
                        PositionLine(trace, threat.Start + Vector3.Down * 1.2f, threat.End + Vector3.Down * 1.2f, trace.Scale.X);
                        node.GetNode<Node3D>("StartEmitter").Position = threat.Start;
                        node.GetNode<Node3D>("EndEmitter").Position = threat.End;
                    }
                    else if (threat.Velocity.LengthSquared() > .0001f)
                        node.Basis = Basis.LookingAt(threat.Velocity, Vector3.Up);
                }
                if (threat.Kind == ThreatKind.Projectile) node.Position = threat.Position;
                else
                {
                    var beam = node.GetNode<MeshInstance3D>("Core");
                    beam.MaterialOverride = threat.IsLive ? LiveBeamMaterial : WarningBeamMaterial;
                    float width = node.GetMeta("warning_width").AsSingle() * (threat.IsLive ? 4.2f : 1);
                    PositionLine(beam, threat.Start, threat.End, width);
                    node.Visible = threat.IsLive || Mathf.Sin(threat.Age * 24) > -.4f;
                }
            }
            foreach (var supply in session.Supplies)
            {
                alive.Add(supply.Id);
                if (!_entities.TryGetValue(supply.Id, out var node))
                {
                    node = (supply.Kind == SupplyKind.Repair ? RepairSupplyScene : CreditsSupplyScene).Instantiate<Node3D>();
                    _entityRoot.AddChild(node);
                    _entities.Add(supply.Id, node);
                }
                node.Position = supply.Position + Vector3.Up * Mathf.Sin(_time * 3) * .18f;
                node.Rotation = new(0, _time, 0);
            }
            var expired = new List<int>();
            foreach (var pair in _entities) if (!alive.Contains(pair.Key)) expired.Add(pair.Key);
            foreach (int id in expired) { _entities[id].QueueFree(); _entities.Remove(id); }
        }
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var spark = _sparks[i];
            float life = spark.Life - dt;
            if (life <= 0) { spark.Node.QueueFree(); _sparks.RemoveAt(i); continue; }
            spark.Node.Position += spark.Velocity * dt;
            spark.Node.Scale = Vector3.One * Mathf.Min(1, life * 2);
            _sparks[i] = (spark.Node, spark.Velocity, life);
        }
    }

    private static void PositionLine(MeshInstance3D line, Vector3 start, Vector3 end, float thickness)
    {
        var direction = end - start;
        line.Position = (start + end) / 2;
        var orientation = direction.LengthSquared() > .000001f ? Basis.LookingAt(direction, Vector3.Up) : Basis.Identity;
        line.Basis = orientation * Basis.FromScale(new Vector3(thickness, thickness, direction.Length()));
    }

    public void OnFlightEvent(FlightEvent flightEvent)
    {
        if (flightEvent.Kind == FlightEventKind.Hit) _shake = ReducedEffects ? .08f : .3f;
        if (flightEvent.Kind == FlightEventKind.NearMiss || flightEvent.Kind == FlightEventKind.Finished) return;
        Color color = flightEvent.Kind == FlightEventKind.Hit ? new("ff8256") : Plane.Accent;
        int count = ReducedEffects ? 5 : flightEvent.Kind == FlightEventKind.Pulse ? 35 : 16;
        for (int i = 0; i < count; i++)
        {
            var spark = SparkScene.Instantiate<Node3D>();
            _sparkRoot.AddChild(spark);
            spark.Position = flightEvent.Position;
            var material = (StandardMaterial3D)spark.GetNode<MeshInstance3D>("Glow").MaterialOverride;
            material.AlbedoColor = color;
            material.Emission = color;
            float angle = Mathf.Tau * i / count;
            float speed = flightEvent.Kind == FlightEventKind.Pulse ? 13 : 4 + (float)_random.NextDouble() * 4;
            var velocity = new Vector3(Mathf.Cos(angle), .15f + (float)_random.NextDouble(), Mathf.Sin(angle)) * speed;
            _sparks.Add((spark, velocity, .55f + (float)_random.NextDouble() * .3f));
        }
    }
}

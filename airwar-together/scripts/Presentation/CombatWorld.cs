using Godot;
using System;
using System.Collections.Generic;
using Airwar.Core;

namespace Airwar.Presentation;

public partial class CombatWorld : Node3D
{
    public Camera3D Camera { get; private set; } = null!;
    public PlaneVisual Plane { get; private set; } = null!;
    public bool IsMenu { get; set; } = true;
    public bool ReducedEffects { get; set; }
    private readonly Dictionary<int, Node3D> _entities = new();
    private readonly List<(Node3D Node, Vector3 Velocity, float Life)> _sparks = new();
    private readonly List<Node3D> _scenery = new();
    private Node3D _pad = null!, _boundary = null!;
    private float _time, _shake;
    private readonly Random _random = new(711);
    private StandardMaterial3D _orange = null!, _warning = null!, _cyan = null!;

    public override void _Ready()
    {
        _orange = Models.Material(new("ff764a"), 1.5f);
        _warning = Models.Material(new Color("ffba7a") { A = .5f }, 1, true);
        _cyan = Models.Material(new("65ddeb"), 1);
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new("050e1c"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new("81a2c9"), AmbientLightEnergy = .68f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            FogEnabled = true, FogLightColor = new("091e32"), FogDensity = .008f
        };
        AddChild(new WorldEnvironment { Environment = environment });
        AddChild(new DirectionalLight3D { RotationDegrees = new(-48, -35, 0), LightColor = new("c7dfff"), LightEnergy = 1.6f, ShadowEnabled = true, DirectionalShadowMaxDistance = 48 });
        AddChild(new DirectionalLight3D { RotationDegrees = new(-25, 130, 0), LightColor = new("4babbf"), LightEnergy = .9f });
        Camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 20, Position = new(0, 25, 17), Near = .1f, Far = 160, Current = true };
        AddChild(Camera); Camera.LookAt(new Vector3(0, 0, -.6f));
        Models.Box(this, new(0, -1.15f, 0), new(160, .2f, 180), Models.Material(new("071422")));
        var grid = Models.Material(new("102c3c"), .5f);
        for (int x = -45; x <= 45; x += 3) Models.Line(this, new(x, -1, -70), new(x, -1, 70), .025f, grid);
        for (int z = -70; z <= 70; z += 3) Models.Line(this, new(-45, -1, z), new(45, -1, z), .025f, grid);
        var dim = Models.Material(new("153a4e"), .5f);
        _boundary = new Node3D { Position = new(0, -.82f, 0) }; AddChild(_boundary);
        Models.Line(_boundary, new(-10.6f, 0, -6.5f), new(10.6f, 0, -6.5f), .08f, dim);
        Models.Line(_boundary, new(-10.6f, 0, 6.5f), new(10.6f, 0, 6.5f), .08f, dim);
        Models.Line(_boundary, new(-10.6f, 0, -6.5f), new(-10.6f, 0, 6.5f), .08f, dim);
        Models.Line(_boundary, new(10.6f, 0, -6.5f), new(10.6f, 0, 6.5f), .08f, dim);
        for (int i = 0; i < 12; i++)
        {
            float x = -10 + i * 1.82f;
            Models.Box(_boundary, new(x, 0, -6.5f), new(.2f, .035f, .22f), _cyan);
            Models.Box(_boundary, new(x, 0, 6.5f), new(.2f, .035f, .22f), _cyan);
        }
        _pad = new Node3D { Position = new(5, -.75f, 0) }; AddChild(_pad);
        Models.Ring(_pad, 4.7f, dim, .07f);
        Models.Ring(_pad, 3.8f, _cyan, .035f);
        Models.Ring(_pad, 4.1f, dim, .035f);
        for (int i = 0; i < 20; i++)
        {
            float a = Mathf.Tau * i / 20;
            Models.Line(_pad, new(Mathf.Cos(a) * 4.3f, 0, Mathf.Sin(a) * 4.3f), new(Mathf.Cos(a) * 4.65f, 0, Mathf.Sin(a) * 4.65f), .08f, i % 5 == 0 ? _cyan : dim);
        }
        var metal = Models.Material(new("0c2334"));
        for (int i = 0; i < 36; i++)
        {
            var tower = new Node3D { Position = new((i % 2 == 0 ? -1 : 1) * (14 + (float)_random.NextDouble() * 18), -1, -55 + i * 3.3f) };
            AddChild(tower); _scenery.Add(tower);
            float height = 1.3f + (float)_random.NextDouble() * 5;
            Models.Box(tower, new(0, height / 2, 0), new(1.5f, height, 2.2f), metal);
            Models.Box(tower, new(.78f, height * .6f, 0), new(.04f, height * .4f, .1f), dim);
            Models.Box(tower, new(0, height + .02f, 0), new(.15f, .03f, .4f), i % 3 == 0 ? _orange : _cyan);
        }
        var stars = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new SphereMesh { Radius = .065f, Height = .13f, RadialSegments = 4, Rings = 2 }, InstanceCount = 140 };
        for (int i = 0; i < 140; i++) stars.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3(-45 + (float)_random.NextDouble() * 90, 2 + (float)_random.NextDouble() * 8, -65 + (float)_random.NextDouble() * 100)));
        AddChild(new MultiMeshInstance3D { Multimesh = stars, MaterialOverride = dim });
        Plane = new PlaneVisual { Position = new(5, 2.2f, 0), Scale = Vector3.One * 1.4f }; AddChild(Plane); Plane.Build(ShipCatalog.All[0]);
    }

    public Vector3 ScreenToFlight(Vector2 position)
    {
        var target = ProjectToFlightPlane(position);
        return new(Mathf.Clamp(target.X, -GameSession.HalfWidth, GameSession.HalfWidth), GameSession.FlightHeight, Mathf.Clamp(target.Z, -GameSession.HalfDepth, GameSession.HalfDepth));
    }

    public Vector3 ScreenDeltaToFlight(Vector2 from, Vector2 to)
    {
        // Relative touch input must retain motion outside the projected arena.
        // Clamping either endpoint would turn edge gestures into zero deltas.
        return ProjectToFlightPlane(to) - ProjectToFlightPlane(from);
    }

    private Vector3 ProjectToFlightPlane(Vector2 position)
    {
        var origin = Camera.ProjectRayOrigin(position); var direction = Camera.ProjectRayNormal(position);
        float distance = (GameSession.FlightHeight - origin.Y) / direction.Y;
        return origin + direction * distance;
    }

    public void ClearEntities()
    {
        foreach (var node in _entities.Values) node.QueueFree(); _entities.Clear();
        foreach (var spark in _sparks) spark.Node.QueueFree(); _sparks.Clear();
    }

    public void Render(GameSession? session, float dt)
    {
        _time += dt;
        float aspect = GetViewport().GetVisibleRect().Size.X / Mathf.Max(1, GetViewport().GetVisibleRect().Size.Y);
        Camera.Size = IsMenu ? Mathf.Max(20.4f, 26f / aspect) : Mathf.Max(16.3f, 23.5f / aspect);
        _pad.Visible = IsMenu; _boundary.Visible = !IsMenu;
        Camera.Position = new(Mathf.Sin(_time * 38) * _shake, 25, 17 + Mathf.Cos(_time * 29) * _shake);
        _shake = Mathf.MoveToward(_shake, 0, dt * 2.2f);
        foreach (var tower in _scenery)
        {
            tower.Position += Vector3.Back * dt * (IsMenu ? .4f : 3.2f);
            if (tower.Position.Z > 45) tower.Position = new(tower.Position.X, -1, -60);
        }
        if (IsMenu)
        {
            Plane.Position = new(5, 2.2f + Mathf.Sin(_time * 1.2f) * .15f, 0);
            Plane.Scale = Vector3.One * 1.4f;
            Plane.Visible = true; Plane.Rotation = new(.09f, -.22f + Mathf.Sin(_time * .35f) * .16f, -.09f);
            Plane.Animate(_time, Vector3.Zero, 0, false);
            Plane.Rotation = new(.09f, -.22f + Mathf.Sin(_time * .35f) * .16f, -.09f);
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
                    node = new Node3D(); AddChild(node); _entities.Add(threat.Id, node);
                    if (threat.Kind == ThreatKind.Beam)
                    {
                        Models.Line(node, threat.Start, threat.End, .06f, _warning);
                        Models.Line(node, threat.Start + Vector3.Down * 1.2f, threat.End + Vector3.Down * 1.2f, .035f, _warning);
                        Models.Sphere(node, threat.Start, .22f, _orange); Models.Sphere(node, threat.End, .22f, _orange);
                    }
                    else
                    {
                        Models.Sphere(node, Vector3.Zero, .2f, _orange);
                        Models.Sphere(node, Vector3.Zero, .35f, Models.Material(new Color(1, .3f, .1f, .2f), 1, true));
                        var trail = Models.Box(node, new(0, 0, .35f), new(.075f, .075f, .7f), _warning);
                        if (threat.Velocity.LengthSquared() > .0001f) node.Basis = Basis.LookingAt(threat.Velocity, Vector3.Up);
                    }
                }
                if (threat.Kind == ThreatKind.Projectile) node.Position = threat.Position;
                else
                {
                    var beam = (MeshInstance3D)node.GetChild(0);
                    beam.MaterialOverride = threat.IsLive ? _orange : _warning;
                    beam.Scale = new(threat.IsLive ? 4.2f : 1, threat.IsLive ? 4.2f : 1, 1);
                    node.Visible = threat.IsLive || Mathf.Sin(threat.Age * 24) > -.4f;
                }
            }
            foreach (var supply in session.Supplies)
            {
                alive.Add(supply.Id);
                if (!_entities.TryGetValue(supply.Id, out var node))
                {
                    node = new Node3D(); AddChild(node); _entities.Add(supply.Id, node);
                    var material = supply.Kind == SupplyKind.Repair ? Models.Material(new("8ce7b8"), 1) : Models.Material(new("f2c777"), 1);
                    Models.Ring(node, .52f, material, .035f, 16);
                    Models.Box(node, Vector3.Zero, new(.55f, .12f, .14f), material);
                    if (supply.Kind == SupplyKind.Repair) Models.Box(node, Vector3.Zero, new(.14f, .12f, .55f), material);
                    else Models.Box(node, Vector3.Zero, new(.3f, .26f, .3f), material).RotationDegrees = new(0, 45, 0);
                }
                node.Position = supply.Position + Vector3.Up * Mathf.Sin(_time * 3) * .18f; node.Rotation = new(0, _time, 0);
            }
            var expired = new List<int>();
            foreach (var pair in _entities) if (!alive.Contains(pair.Key)) expired.Add(pair.Key);
            foreach (int id in expired) { _entities[id].QueueFree(); _entities.Remove(id); }
        }
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var spark = _sparks[i]; float life = spark.Life - dt;
            if (life <= 0) { spark.Node.QueueFree(); _sparks.RemoveAt(i); continue; }
            spark.Node.Position += spark.Velocity * dt; spark.Node.Scale = Vector3.One * Mathf.Min(1, life * 2);
            _sparks[i] = (spark.Node, spark.Velocity, life);
        }
    }

    public void OnFlightEvent(FlightEvent flightEvent)
    {
        if (flightEvent.Kind == FlightEventKind.Hit) _shake = ReducedEffects ? .08f : .3f;
        if (flightEvent.Kind == FlightEventKind.NearMiss || flightEvent.Kind == FlightEventKind.Finished) return;
        Color color = flightEvent.Kind == FlightEventKind.Hit ? new("ff8256") : Plane.Accent;
        int count = ReducedEffects ? 5 : flightEvent.Kind == FlightEventKind.Pulse ? 35 : 16;
        var material = Models.Material(color, 1);
        for (int i = 0; i < count; i++)
        {
            var spark = new Node3D { Position = flightEvent.Position }; AddChild(spark);
            Models.Box(spark, Vector3.Zero, new(.055f, .055f, .15f), material);
            float angle = Mathf.Tau * i / count;
            float speed = flightEvent.Kind == FlightEventKind.Pulse ? 13 : 4 + (float)_random.NextDouble() * 4;
            var velocity = new Vector3(Mathf.Cos(angle), .15f + (float)_random.NextDouble(), Mathf.Sin(angle)) * speed;
            _sparks.Add((spark, velocity, .55f + (float)_random.NextDouble() * .3f));
        }
    }
}

using Godot;
using Airwar.Core;

namespace Airwar.Presentation;

public static class Models
{
    public static StandardMaterial3D Material(Color color, float emission = 0, bool transparent = false)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color, Metallic = emission > 0 ? .25f : .65f, Roughness = .38f,
            EmissionEnabled = emission > 0, Emission = color, EmissionEnergyMultiplier = emission,
            ShadingMode = emission > 0 ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
            Transparency = transparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
    }

    public static MeshInstance3D Box(Node3D parent, Vector3 position, Vector3 size, Material material)
    {
        var node = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = position, MaterialOverride = material };
        parent.AddChild(node); return node;
    }

    public static MeshInstance3D Sphere(Node3D parent, Vector3 position, float radius, Material material)
    {
        var node = new MeshInstance3D { Mesh = new SphereMesh { Radius = radius, Height = radius * 2, RadialSegments = 12, Rings = 6 }, Position = position, MaterialOverride = material };
        parent.AddChild(node); return node;
    }

    public static MeshInstance3D Line(Node3D parent, Vector3 a, Vector3 b, float thickness, Material material)
    {
        var node = Box(parent, (a + b) / 2, new(thickness, thickness, a.DistanceTo(b)), material);
        if (a.DistanceTo(b) > .001f) node.Basis = Basis.LookingAt(b - a, Vector3.Up);
        return node;
    }

    public static void Ring(Node3D parent, float radius, Material material, float thickness = .035f, int segments = 64)
    {
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.Tau / segments, b = (i + 1) * Mathf.Tau / segments;
            Vector3 outerA = new(Mathf.Cos(a) * (radius + thickness), 0, Mathf.Sin(a) * (radius + thickness));
            Vector3 innerA = new(Mathf.Cos(a) * (radius - thickness), 0, Mathf.Sin(a) * (radius - thickness));
            Vector3 outerB = new(Mathf.Cos(b) * (radius + thickness), 0, Mathf.Sin(b) * (radius + thickness));
            Vector3 innerB = new(Mathf.Cos(b) * (radius - thickness), 0, Mathf.Sin(b) * (radius - thickness));
            surface.SetNormal(Vector3.Up);
            foreach (var vertex in new[] { outerA, innerA, outerB, outerB, innerA, innerB }) surface.AddVertex(vertex);
        }
        parent.AddChild(new MeshInstance3D { Mesh = surface.Commit(), MaterialOverride = material });
    }

    public static MeshInstance3D Prism(Node3D parent, Vector2[] polygon, float height, Material material, float y = 0)
    {
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            var normal = (b - a).Cross(c - a).Normalized();
            surface.SetNormal(normal); surface.AddVertex(a); surface.AddVertex(b); surface.AddVertex(c);
        }
        Vector3 Point(int i, bool top) => new(polygon[i].X, y + (top ? height / 2 : -height / 2), polygon[i].Y);
        for (int i = 1; i < polygon.Length - 1; i++)
        {
            Triangle(Point(0, true), Point(i + 1, true), Point(i, true));
            Triangle(Point(0, false), Point(i, false), Point(i + 1, false));
        }
        for (int i = 0; i < polygon.Length; i++)
        {
            int j = (i + 1) % polygon.Length;
            Triangle(Point(i, false), Point(j, true), Point(i, true));
            Triangle(Point(i, false), Point(j, false), Point(j, true));
        }
        var node = new MeshInstance3D { Mesh = surface.Commit(), MaterialOverride = material };
        parent.AddChild(node); return node;
    }
}

public partial class PlaneVisual : Node3D
{
    private MeshInstance3D? _leftExhaust, _rightExhaust;
    private Node3D? _shield;
    public Color Accent { get; private set; }

    public void Build(ShipDefinition ship)
    {
        foreach (var child in GetChildren()) child.QueueFree();
        Accent = ship.Color;
        var hull = Models.Material(new("bacbd9"));
        var dark = Models.Material(new("152c43"));
        var accent = Models.Material(ship.Color);
        var light = Models.Material(ship.Color, 1.8f);
        var glass = Models.Material(new("287a94")); glass.Metallic = .8f; glass.Roughness = .14f;
        float wing = ship.Shape == 4 ? 2.9f : ship.Shape == 2 ? 2.15f : 2.5f;
        float sweep = ship.Shape == 1 ? .3f : ship.Shape == 3 ? -.65f : .65f;
        Models.Prism(this, new Vector2[] { new(0, -2.55f), new(-.38f, -.4f), new(-.48f, 1.4f), new(.48f, 1.4f), new(.38f, -.4f) }, .36f, hull);
        Models.Prism(this, new Vector2[] { new(0, -2.3f), new(-.19f, -.55f), new(-.24f, .5f), new(.24f, .5f), new(.19f, -.55f) }, .16f, hull, .22f);
        for (int sign = -1; sign <= 1; sign += 2)
        {
            Models.Prism(this, new Vector2[] { new(sign * .26f, -.7f), new(sign * wing, .5f + sweep), new(sign * wing * .86f, 1.2f), new(sign * .38f, .85f) }, .13f, accent, -.1f);
            Models.Prism(this, new Vector2[] { new(sign * .38f, 1.05f), new(sign * 1.3f, 1.65f), new(sign * 1.1f, 1.95f), new(sign * .4f, 1.7f) }, .14f, hull);
            Models.Box(this, new(sign * .63f, -.01f, .63f), new(.32f, .36f, 1.95f), dark);
            Models.Line(this, new(sign * .79f, .19f, -.22f), new(sign * .79f, .19f, 1.38f), .055f, light);
            Models.Box(this, new(sign * .63f, .06f, 1.65f), new(.25f, .24f, .1f), light);
            Models.Box(this, new(sign * wing * .86f, -.05f, .9f), new(.1f, .08f, .42f), light);
            Models.Line(this, new(sign * .64f, 0, .02f), new(sign * (wing - .2f), 0, .76f + sweep), .045f, hull);
            Models.Box(this, new(sign * .63f, .2f, .8f), new(.24f, .035f, .7f), hull);
            for (int i = 0; i < 4; i++) Models.Box(this, new(sign * .63f, .225f, .5f + i * .16f), new(.19f, .03f, .045f), dark);
        }
        Models.Prism(this, new Vector2[] { new(0, -1.6f), new(-.25f, -.6f), new(-.18f, .24f), new(.18f, .24f), new(.25f, -.6f) }, .27f, glass, .3f);
        Models.Line(this, new(0, .43f, -1.58f), new(0, .43f, .16f), .035f, light);
        Models.Box(this, new(0, .33f, 1.05f), new(.1f, .6f, .55f), accent);
        if (ship.Shape == 5) Models.Prism(this, new Vector2[] { new(-.7f, -1.2f), new(-1.25f, -.5f), new(.0f, -.75f), new(1.25f, -.5f), new(.7f, -1.2f) }, .09f, accent);
        var exhaust = Models.Material(new Color(ship.Color, .6f), 2, true);
        _leftExhaust = Models.Box(this, new(-.63f, .03f, 2.28f), new(.2f, .15f, 1.18f), exhaust);
        _rightExhaust = Models.Box(this, new(.63f, .03f, 2.28f), new(.2f, .15f, 1.18f), exhaust);
        _shield = new Node3D(); AddChild(_shield);
        Models.Ring(_shield, 2.9f, Models.Material(new Color(ship.Color, .65f), 1, true), .035f, 40);
        _shield.Visible = false;
    }

    public void Animate(float time, Vector3 velocity, float protection, bool boosting)
    {
        Rotation = new(Mathf.Sin(time * 2) * .015f, 0, Mathf.Lerp(Rotation.Z, -velocity.X * .015f, .12f));
        if (_leftExhaust != null && _rightExhaust != null)
        {
            float length = (boosting ? 2.8f : 1) + Mathf.Sin(time * 35) * .12f;
            _leftExhaust.Scale = new(1, 1, length); _rightExhaust.Scale = new(1, 1, length);
        }
        if (_shield != null) { _shield.Visible = protection > 0; _shield.Rotation = new(0, time * .8f, 0); }
        Visible = protection < .1f || Mathf.Sin(time * 35) > -.6f;
    }
}

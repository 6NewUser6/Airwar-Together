using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Airwar.Arena;

/// <summary>Collision data baked alongside authored worlds, indexed into kilometre cells.</summary>
internal sealed class ArenaObstacles
{
    private sealed class Shape
    {
        public int Sector;
        public bool Sphere;
        public Vector3 Center, HalfSize;
        public float Radius;
    }
    private readonly Dictionary<(int, int, int, int), List<Shape>> _cells = new();
    internal int Count { get; private set; }
    internal ArenaObstacles()
    {
        const string path = "res://assets/arena/collision_world.json";
        if (!Godot.FileAccess.FileExists(path)) return;
        using var document = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path));
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var shape = new Shape { Sector = item.GetProperty("sector").GetInt32(), Sphere = item.GetProperty("kind").GetString() == "sphere",
                Center = ReadVector(item.GetProperty("center")) };
            if (shape.Sphere) { shape.Radius = item.GetProperty("radius").GetSingle(); shape.HalfSize = Vector3.One * shape.Radius; }
            else shape.HalfSize = ReadVector(item.GetProperty("size"));
            Vector3 low = shape.Center - shape.HalfSize, high = shape.Center + shape.HalfSize;
            for (int x = Cell(low.X); x <= Cell(high.X); x++)
            for (int y = Cell(low.Y); y <= Cell(high.Y); y++)
            for (int z = Cell(low.Z); z <= Cell(high.Z); z++)
            {
                var key = (shape.Sector, x, y, z);
                if (!_cells.TryGetValue(key, out var list)) _cells[key] = list = new List<Shape>();
                list.Add(shape);
            }
            Count++;
        }
    }
    private static Vector3 ReadVector(JsonElement array) => new(array[0].GetSingle(), array[1].GetSingle(), array[2].GetSingle());
    private static int Cell(float coordinate) => (int)Math.Floor(coordinate / 1000);
    internal bool Intersects(int sector, Vector3 from, Vector3 to, float radius, out float fraction)
    {
        fraction = 2; var seen = new HashSet<Shape>();
        var low = from.Min(to) - Vector3.One * radius; var high = from.Max(to) + Vector3.One * radius;
        for (int x = Cell(low.X); x <= Cell(high.X); x++)
        for (int y = Cell(low.Y); y <= Cell(high.Y); y++)
        for (int z = Cell(low.Z); z <= Cell(high.Z); z++)
        {
            if (!_cells.TryGetValue((sector, x, y, z), out var shapes)) continue;
            foreach (var shape in shapes)
            {
                if (!seen.Add(shape)) continue;
                float hit = shape.Sphere ? Sphere(from, to, shape.Center, shape.Radius + radius)
                    : Box(from, to, shape.Center - shape.HalfSize - Vector3.One * radius, shape.Center + shape.HalfSize + Vector3.One * radius);
                if (hit >= 0) fraction = Math.Min(fraction, hit);
            }
        }
        return fraction <= 1;
    }
    private static float Sphere(Vector3 from, Vector3 to, Vector3 center, float radius)
    {
        var direction = to - from; var offset = from - center;
        float a = direction.LengthSquared(), c = offset.LengthSquared() - radius * radius;
        if (c <= 0) return 0;
        if (a < .00001f) return -1;
        float b = offset.Dot(direction), discriminant = b * b - a * c;
        if (discriminant < 0) return -1;
        float t = (-b - Mathf.Sqrt(discriminant)) / a;
        return t >= 0 && t <= 1 ? t : -1;
    }
    private static float Box(Vector3 from, Vector3 to, Vector3 low, Vector3 high)
    {
        Vector3 delta = to - from; float near = 0, far = 1;
        for (int axis = 0; axis < 3; axis++)
        {
            if (Math.Abs(delta[axis]) < .00001f) { if (from[axis] < low[axis] || from[axis] > high[axis]) return -1; continue; }
            float a = (low[axis] - from[axis]) / delta[axis], b = (high[axis] - from[axis]) / delta[axis];
            near = Math.Max(near, Math.Min(a, b)); far = Math.Min(far, Math.Max(a, b));
            if (near > far) return -1;
        }
        return near;
    }
}

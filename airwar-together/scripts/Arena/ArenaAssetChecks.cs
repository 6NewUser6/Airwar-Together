using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;
using System.Security.Cryptography;
using Airwar.Core;

namespace Airwar.Arena;

/// <summary>Checks saved sources and exported resources without running scene Ready callbacks.</summary>
public static class ArenaAssetChecks
{
    private static IEnumerable<Node> Walk(Node node)
    {
        yield return node;
        foreach (Node child in node.GetChildren()) foreach (Node descendant in Walk(child)) yield return descendant;
    }
    public static bool Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); GD.Print("PASS_ARENA_ASSET ", message); checks++; }
        try
        {
            for (int sector = 0; sector < SectorCatalog.Count; sector++)
            {
                var scene = GD.Load<PackedScene>(SectorCatalog.ScenePaths[sector]).Instantiate<Node3D>();
                try
                {
                    var meshes = Walk(scene).OfType<MeshInstance3D>().ToArray();
                    Check(meshes.Length > 100 && meshes.All(x => x.Mesh != null && !string.IsNullOrEmpty(x.Mesh.ResourcePath)), "persisted sector meshes: " + sector);
                    Check(Walk(scene).OfType<WorldEnvironment>().Single().Environment != null && Walk(scene).OfType<DirectionalLight3D>().Any(), "authored lighting and environment: " + sector);
                    if (sector == 3) continue;
                    var terrain = scene.GetNode("Terrain").GetChildren().OfType<MeshInstance3D>().ToArray();
                    Check(terrain.Length == 16, "sixteen terrain source chunks: " + sector);
                    Aabb area = terrain[0].Mesh.GetAabb();
                    foreach (var chunk in terrain.Skip(1)) area = area.Merge(chunk.Mesh.GetAabb());
                    Check(Math.Abs(area.Size.X - 20000) < 1 && Math.Abs(area.Size.Z - 20000) < 1, "actual terrain covers twenty kilometres: " + sector);
                }
                finally { scene.Free(); }
            }
            Check(Godot.FileAccess.FileExists("res://assets/arena/collision_world.json"), "export contains collision data");
            using (var collision = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://assets/arena/collision_world.json")))
                Check(collision.RootElement.GetArrayLength() >= 1900 && collision.RootElement.EnumerateArray().Select(x => x.GetProperty("sector").GetInt32()).Distinct().Count() == 4,
                    "static collision data covers all four sectors");
            var independent = new HashSet<string>();
            foreach (var ship in ShipCatalog.All)
            {
                var model = ship.VisualScene.Instantiate<Node3D>();
                try
                {
                    var hull = model.GetNode<Node3D>("Hull");
                    using var vertices = new MemoryStream();
                    using (var writer = new BinaryWriter(vertices, System.Text.Encoding.UTF8, true))
                        foreach (var mesh in Walk(hull).OfType<MeshInstance3D>())
                            foreach (var vertex in mesh.Mesh.GetFaces())
                            { writer.Write(vertex.X); writer.Write(vertex.Y); writer.Write(vertex.Z); }
                    independent.Add(Convert.ToHexString(SHA256.HashData(vertices.ToArray())));
                    Check(Walk(hull).OfType<MeshInstance3D>().Any(x => x.Mesh.GetFaces().Length > 300), "community hull loads: " + ship.Id);
                }
                finally { model.Free(); }
            }
            Check(independent.Count >= 10, "ship library has at least ten independently saved hulls");
            var arena = GD.Load<PackedScene>("res://scenes/arena/Arena.tscn").Instantiate();
            try
            {
                Check(arena.HasNode("Network") && arena.HasNode("World/Camera") && arena.HasNode("Interface/Hud/Lobby"), "mode is assembled from authored scenes");
                var music = arena.GetNode<AudioStreamPlayer>("Audio/Music").Stream as AudioStreamWav;
                Check(music != null && music.GetLength() > 20 && music.LoopMode == AudioStreamWav.LoopModeEnum.Forward, "ambient music is a saved looping asset");
            }
            finally
            {
                foreach (var player in Walk(arena).OfType<AudioStreamPlayer>()) { player.Stop(); player.Stream = null; }
                arena.Free();
            }
            GD.Print("ARENA_ASSET_OK ", checks); return true;
        }
        catch (Exception error) { GD.PushError("ARENA_ASSET_FAILED " + error); return false; }
    }
}

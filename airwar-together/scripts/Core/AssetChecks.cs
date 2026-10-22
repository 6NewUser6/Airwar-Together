using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Airwar.Core;

/// <summary>Checks that the saved assets exist before any scene's Ready callback runs.</summary>
public static class AssetChecks
{
    private static IEnumerable<Node> Walk(Node root)
    {
        yield return root;
        foreach (Node child in root.GetChildren())
            foreach (Node descendant in Walk(child)) yield return descendant;
    }

    public static bool Run()
    {
        int checks = 0;
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            GD.Print("PASS_ASSET ", label); checks++;
        }
        try
        {
            var main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
            try
            {
                var world = main.GetNode<Node3D>("CombatWorld");
                Check(world.GetNode<Camera3D>("Camera").Current &&
                    world.GetNode<WorldEnvironment>("Environment").Environment != null,
                    "camera and environment are authored scene nodes");
                var meshes = Walk(world).OfType<MeshInstance3D>().ToArray();
                Check(meshes.Length > 80 && meshes.All(mesh => mesh.Mesh != null &&
                    !string.IsNullOrEmpty(mesh.Mesh.ResourcePath)),
                    "battlefield and aircraft meshes exist before Ready and reference saved resources");
                Check(Walk(world.GetNode("PlayerPlane/Model")).OfType<MeshInstance3D>().Any(mesh => mesh.Mesh?.GetFaces().Length > 300),
                    "default aircraft is visible in the static main scene");

                var hud = main.GetNode<Control>("Interface/FlightHud");
                string[] pages = { "Menu", "Hangar", "Settings", "Game", "Pause", "Results", "Legacy" };
                Check(pages.All(page => hud.HasNode(page) &&
                    !string.IsNullOrEmpty(hud.GetNode(page).SceneFilePath)),
                    "all seven UI pages are reusable scene instances");
                Check(hud.GetNode<Control>("Menu").Visible &&
                    pages.Skip(1).All(page => !hud.GetNode<Control>(page).Visible),
                    "static main menu is visible and other pages start hidden");
                Check(hud.Theme != null && hud.Theme.ResourcePath.EndsWith(".tres"),
                    "UI uses an editable external theme");
                Check(hud.GetNode("Hangar/Column/ShipGrid").GetChildCount() == ShipCatalog.All.Length,
                    "hangar contains authored buttons for every ship");

                var audio = main.GetNode("FlightAudio");
                var music = audio.GetNode<AudioStreamPlayer>("Music").Stream as AudioStreamWav;
                Check(music != null && Math.Abs(music.GetLength() - 16) < .01 &&
                    music.LoopMode == AudioStreamWav.LoopModeEnum.Forward,
                    "saved music is sixteen seconds long and loops");
                Check(audio.GetNode("Voices").GetChildCount() == 8,
                    "audio voice pool is authored in its reusable scene");
            }
            finally
            {
                // Detached scene instances never receive ExitTree; release their
                // audio playback objects explicitly before freeing the snapshot.
                foreach (var player in Walk(main).OfType<AudioStreamPlayer>())
                {
                    player.Stop();
                    player.Stream = null;
                }
                main.Free();
            }

            Check(ShipCatalog.All.Length == 12 &&
                ShipCatalog.All.Select(ship => ship.Id).Distinct().Count() == 12,
                "ship library contains twelve distinct editable definitions");
            foreach (var ship in ShipCatalog.All)
            {
                Check(!string.IsNullOrEmpty(ship.ResourcePath) && ship.VisualScene != null,
                    "ship definition and prefab: " + ship.Id);
                var scene = ship.VisualScene ?? throw new InvalidOperationException("Missing visual: " + ship.Id);
                var model = scene.Instantiate<Node3D>();
                try
                {
                    var geometry = Walk(model).OfType<MeshInstance3D>().ToArray();
                    Check(geometry.Any(mesh => mesh.Mesh?.GetFaces().Length > 300) &&
                        model.HasNode("LeftExhaust") && model.HasNode("RightExhaust") && model.HasNode("Shield"),
                        "imported ship geometry and effect attachment points: " + ship.Id);
                }
                finally { model.Free(); }
            }

            foreach (string effect in new[] { "Beam", "Projectile", "RepairSupply", "CreditsSupply", "Spark" })
            {
                var instance = GD.Load<PackedScene>($"res://scenes/effects/{effect}.tscn").Instantiate();
                try { Check(Walk(instance).OfType<MeshInstance3D>().Any(), "reusable effect: " + effect); }
                finally { instance.Free(); }
            }
            GD.Print("ASSET_TEST_OK ", checks);
            return true;
        }
        catch (Exception error) { GD.PushError("ASSET_TEST_FAILED " + error); return false; }
    }
}

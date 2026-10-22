using Godot;
using System;
using System.Linq;
using System.IO;
using System.Text.Json;
using Airwar.Persistence;

namespace Airwar.Core;

public static class GameChecks
{
    public static bool Run()
    {
        int checks = 0;
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            checks++; GD.Print("PASS ", label);
        }
        try
        {
            Check(SpatialMath.SegmentDistance(new(-2, 0, 0), new(2, 0, 0), new(0, 0, -2), new(0, 0, 2)) < .001f, "swept crossing beam collision");
            Check(Mathf.Abs(SpatialMath.SegmentDistance(new(-2, 3, 0), new(2, 3, 0), new(0, 0, -2), new(0, 0, 2)) - 3) < .001f, "three-dimensional altitude separation");
            Check(Mathf.Abs(SpatialMath.PointSegmentDistance(new(0, 2, 0), Vector3.Zero, Vector3.Zero) - 2) < .001f, "zero-length segment");
            var session = new GameSession(Difficulty.Normal, 27);
            for (int i = 0; i < 60; i++) session.Step(new(Vector3.Right, null, false, false));
            Check(Mathf.Abs(session.Elapsed - 1) < .001f && session.Tick == 60, "fixed simulation time");
            Check(session.PlayerPosition.X <= GameSession.HalfWidth && session.PlayerPosition.Y == GameSession.FlightHeight, "flight boundary and plane constraint");
            session.Step(new(new(float.NaN, 0, 0), null, false, false));
            Check(SpatialMath.IsFinite(session.PlayerPosition), "non-finite input rejected");
            session = new(Difficulty.Normal, 7);
            var fineTarget = session.PlayerPosition + Vector3.Right * .01f;
            session.Step(new(Vector3.Zero, fineTarget, false, false));
            Check(session.PlayerPosition.DistanceTo(fineTarget) < .0001f, "fine target motion has no visible dead zone");
            session = new(Difficulty.Normal, 7);
            session.Threats.Add(new() { Id = 901, Kind = ThreatKind.Beam, Start = new(-10, GameSession.FlightHeight, 1), End = new(10, GameSession.FlightHeight, 1), Warning = 0, Lifetime = 1 });
            for (int i = 0; i < 17; i++) session.Step(new(Vector3.Forward, null, false, false));
            Check(session.Health == 4 && session.Invulnerability > 1.8f, "laser damage and protection");
            session.Threats.Clear();
            for (int i = 0; i < 80; i++)
            {
                session.Threats.Clear();
                session.Threats.Add(new() { Id = 1000 + i, Kind = ThreatKind.Projectile, Position = session.PlayerPosition, Lifetime = 10 });
                session.Step(new());
            }
            Check(session.Health == 4, "protection prevents repeated projectile damage");
            session = new(Difficulty.Normal, 7);
            session.Supplies.Add(new() { Id = 800, Kind = SupplyKind.Credits, Position = session.PlayerPosition });
            session.Step(new()); session.Step(new());
            Check(session.CreditsCollected == 10 && session.Supplies.Count == 0, "pickup awarded exactly once");
            session.Threats.Add(new() { Id = 700, Kind = ThreatKind.Projectile, Position = session.PlayerPosition, Lifetime = 5 });
            session.Threats.Add(new() { Id = 701, Kind = ThreatKind.Projectile, Position = new(9, GameSession.FlightHeight, -5), Lifetime = 5 });
            session.Step(new(Vector3.Zero, null, false, true));
            Check(session.Threats.Count == 1 && session.Threats[0].Id == 701 && session.PulseCooldown > 11, "pulse clears local projectiles only");
            session.Step(new(Vector3.Right, null, true, false));
            Check(session.BoostRemaining > 0 && session.BoostCooldown > 3 && session.Invulnerability > 0, "boost grants short protection");
            session.Step(new(Vector3.Right, null, true, true));
            Check(!session.Events.Any(e => e.Kind is FlightEventKind.Boost or FlightEventKind.Pulse), "ability edges respect cooldowns");
            Check(new GameSession(Difficulty.Inferno, 1).Health == 1 && SurvivalRules.For(Difficulty.Easy).SpawnInterval > SurvivalRules.For(Difficulty.Hard).SpawnInterval, "difficulty changes health and pacing");
            var profile = new PilotProfile { Credits = -25, SelectedShip = "missing", Callsign = "  ", MusicVolume = float.NaN };
            profile.Normalize();
            Check(profile.Credits == 0 && profile.SelectedShip == "spectre" && float.IsFinite(profile.MusicVolume) && profile.UnlockedShips.Contains("spectre"), "invalid saved profile normalization");
            session = new(Difficulty.Inferno, 0);
            session.Threats.Add(new() { Id = 600, Kind = ThreatKind.Projectile, Position = session.PlayerPosition, Lifetime = 5 });
            session.Supplies.Add(new() { Id = 601, Kind = SupplyKind.Repair, Position = session.PlayerPosition });
            session.Step(new());
            Check(session.Finished && session.Health == 0, "fatal collision does not collect repair");
            string directory = Path.Combine(Path.GetTempPath(), "airwar-save-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "pilot.json");
                profile = PilotProfile.Load(path); profile.Credits = 60;
                Check(profile.Save() && profile.Unlock("ember") && profile.Credits == 35, "unlock transaction saved");
                var reloaded = PilotProfile.Load(path);
                Check(reloaded.SelectedShip == "ember" && reloaded.UnlockedShips.Contains("ember") && reloaded.Credits == 35, "profile round trip");
                reloaded.Credits = 31; reloaded.Save();
                string goodBackup = File.ReadAllText(path + ".bak");
                File.WriteAllText(path, "{broken");
                var recovered = PilotProfile.Load(path);
                Check(recovered.Credits == 35 && recovered.RecoveryNotice != "", "corrupt primary recovered from backup");
                recovered.Save();
                Check(File.ReadAllText(path + ".bak") == goodBackup && PilotProfile.Load(path).Credits == 35, "recovery save preserves valid backup");
            }
            finally { Directory.Delete(directory, true); }
            GD.Print("SELF_TEST_OK ", checks); return true;
        }
        catch (Exception e) { GD.PushError("SELF_TEST_FAILED " + e); return false; }
    }
}

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Airwar.Arena;

/// <summary>Server-owned, fixed-step flight and combat. No client supplied position is accepted.</summary>
public sealed class ArenaSimulation
{
    public const int MaxPlayers = 32;
    public const float StepSeconds = 1f / 60;
    public const float ShipRadius = 13;
    private sealed class Control
    {
        public ArenaCommand Command;
        public float LastTime, FireCooldown, LastDamage = -100, Protection = 2, WarpCooldown;
        public int PendingWarp = -1, WarpTarget = -1, Slot;
        public uint Sequence;
    }
    private readonly Dictionary<int, ArenaShip> _ships = new();
    private readonly Dictionary<int, Control> _controls = new();
    private readonly List<ArenaShot> _shots = new();
    private readonly ArenaObstacles _obstacles = new();
    public int StaticObstacleCount => _obstacles.Count;
    private int _nextShot = 1;
    public IReadOnlyDictionary<int, ArenaShip> Ships => _ships;
    public IReadOnlyList<ArenaShot> Shots => _shots;
    public uint Tick { get; private set; }
    public float Elapsed => Tick * StepSeconds;
    public int PeakPlayers { get; private set; }
    public long ShotsFired { get; private set; }
    public long Hits { get; private set; }
    public long Warps { get; private set; }
    public event Action<string>? Notice;

    public bool AddPlayer(int id, string callsign, string shipId)
    {
        if (id <= 0 || _ships.ContainsKey(id) || _ships.Count >= MaxPlayers) return false;
        int slot = Enumerable.Range(0, MaxPlayers).First(i => !_controls.Values.Any(c => c.Slot == i));
        _ships[id] = new ArenaShip { PeerId = id, Callsign = callsign, ShipId = shipId,
            Position = SafeSpawn(0, slot), Speed = 150 };
        _controls[id] = new Control { Slot = slot, LastTime = Elapsed };
        PeakPlayers = Math.Max(PeakPlayers, _ships.Count);
        return true;
    }

    public void RemovePlayer(int id)
    {
        _ships.Remove(id); _controls.Remove(id); _shots.RemoveAll(s => s.Owner == id);
    }

    public static bool IsNewer(uint value, uint previous) => unchecked((int)(value - previous)) > 0;
    public static bool ValidCommand(ArenaCommand input) => float.IsFinite(input.Pitch) && float.IsFinite(input.Yaw)
        && float.IsFinite(input.Roll) && float.IsFinite(input.Throttle) && input.Destination is >= 0 and < SectorCatalog.Count;

    public bool SetCommand(int id, ArenaCommand command, uint sequence)
    {
        if (!_controls.TryGetValue(id, out var c) || !ValidCommand(command) || !IsNewer(sequence, c.Sequence)) return false;
        c.Sequence = sequence; c.LastTime = Elapsed;
        c.Command = command with { Pitch = Mathf.Clamp(command.Pitch, -1, 1), Yaw = Mathf.Clamp(command.Yaw, -1, 1),
            Roll = Mathf.Clamp(command.Roll, -1, 1), Throttle = Mathf.Clamp(command.Throttle, 0, 1), Warp = false };
        if (command.Warp) c.PendingWarp = command.Destination;
        _ships[id].LastInput = sequence;
        return true;
    }

    public bool RequestWarp(int id, int destination)
    {
        if (!_controls.TryGetValue(id, out var c) || destination is < 0 or >= SectorCatalog.Count) return false;
        c.PendingWarp = destination; return true;
    }

    public void Step()
    {
        Tick++;
        foreach (var (id, ship) in _ships)
        {
            var c = _controls[id];
            c.Protection = Math.Max(0, c.Protection - StepSeconds);
            c.FireCooldown = Math.Max(0, c.FireCooldown - StepSeconds);
            c.WarpCooldown = Math.Max(0, c.WarpCooldown - StepSeconds);
            if (!ship.Alive)
            {
                ship.RespawnRemaining = Math.Max(0, ship.RespawnRemaining - StepSeconds);
                if (ship.RespawnRemaining <= 0) Respawn(ship, c);
                c.PendingWarp = -1;
                continue;
            }
            var command = Elapsed - c.LastTime > .5f ? ArenaCommand.Neutral with { Throttle = .5f } : c.Command;
            if (c.PendingWarp >= 0)
            {
                if (c.PendingWarp != ship.Sector && ship.Energy >= 50 && c.WarpCooldown <= 0 && c.WarpTarget < 0)
                { c.WarpTarget = c.PendingWarp; ship.WarpCharge = .001f; }
                c.PendingWarp = -1;
            }
            if (c.WarpTarget >= 0)
            {
                ship.WarpCharge += StepSeconds;
                if (ship.WarpCharge >= 3)
                {
                    ship.Sector = c.WarpTarget; ship.Position = SafeSpawn(ship.Sector, c.Slot);
                    ship.Rotation = Quaternion.Identity; ship.Energy -= 50; ship.WarpCharge = 0;
                    c.WarpTarget = -1; c.WarpCooldown = 10; c.Protection = 2; Warps++;
                    Notice?.Invoke($"{ship.Callsign} 已跃迁至 {SectorCatalog.Name(ship.Sector)}");
                }
            }
            float turn = c.WarpTarget >= 0 ? .35f : 1;
            ship.Rotation = (ship.Rotation * new Quaternion(Vector3.Right, command.Pitch * 1.25f * turn * StepSeconds)
                * new Quaternion(Vector3.Up, command.Yaw * 1.1f * turn * StepSeconds)
                * new Quaternion(Vector3.Back, command.Roll * 1.8f * turn * StepSeconds)).Normalized();
            ship.Boosting = command.Boost && ship.Energy > 2 && c.WarpTarget < 0;
            ship.Energy = Mathf.Clamp(ship.Energy + (ship.Boosting ? -30 : 12) * StepSeconds, 0, 100);
            float wanted = c.WarpTarget >= 0 ? 80 : ship.Boosting ? 330 : 80 + command.Throttle * 140;
            ship.Speed = Mathf.MoveToward(ship.Speed, wanted, 160 * StepSeconds);
            ship.Velocity = ship.Rotation * Vector3.Forward * ship.Speed;
            Vector3 previousPosition = ship.Position;
            ship.Position += ship.Velocity * StepSeconds;
            if (_obstacles.Intersects(ship.Sector, previousPosition, ship.Position, ShipRadius * .7f, out _))
                ApplyDamage(ship, 1000, 0);
            if (ship.Sector != 3)
            {
                float floor = SurfaceHeight(ship.Sector, ship.Position.X, ship.Position.Z);
                if (ship.Position.Y < floor + 7)
                {
                    ApplyDamage(ship, 1000, 0);
                    ship.Position = new Vector3(ship.Position.X, floor + 25, ship.Position.Z);
                }
                if (ship.Sector == 2 && ship.Position.Y < Math.Max(160, floor + 45)) ApplyDamage(ship, 22 * StepSeconds, 0);
            }
            float ceiling = ship.Sector == 3 ? 8000 : 3800;
            bool boundary = Math.Abs(ship.Position.X) > SectorCatalog.HalfExtent - 100 || Math.Abs(ship.Position.Z) > SectorCatalog.HalfExtent - 100
                || ship.Position.Y > ceiling || (ship.Sector == 3 && ship.Position.Y < -8000);
            if (boundary)
            {
                var p = ship.Position;
                ship.Position = new Vector3(Mathf.Clamp(p.X, -9800, 9800), Mathf.Clamp(p.Y, ship.Sector == 3 ? -7900 : 30, ceiling - 100), Mathf.Clamp(p.Z, -9800, 9800));
                var direction = (new Vector3(0, ship.Sector == 3 ? 350 : 650, 0) - ship.Position).Normalized();
                ship.Rotation = Basis.LookingAt(direction, Math.Abs(direction.Dot(Vector3.Up)) > .98f ? Vector3.Right : Vector3.Up).GetRotationQuaternion();
                ApplyDamage(ship, 12, 0);
            }
            if (Elapsed - c.LastDamage > 5) ship.Shield = Math.Min(50, ship.Shield + 10 * StepSeconds);
            if (command.Fire && c.WarpTarget < 0 && c.FireCooldown <= 0 && _shots.Count < 256 && ship.Alive)
            {
                var forward = ship.Rotation * Vector3.Forward;
                _shots.Add(new ArenaShot { Id = _nextShot++, Owner = id, Sector = ship.Sector,
                    Position = ship.Position + forward * 20, Velocity = forward * (1100 + ship.Speed), Life = 1.8f });
                c.FireCooldown = .14f; ShotsFired++;
            }
        }
        for (int i = _shots.Count - 1; i >= 0; i--)
        {
            var shot = _shots[i];
            Vector3 from = shot.Position, to = from + shot.Velocity * StepSeconds;
            ArenaShip? closest = null; float closestT = 2;
            if (_obstacles.Intersects(shot.Sector, from, to, 0, out float obstacleT)) closestT = obstacleT;
            closestT = Math.Min(closestT, TerrainHitFraction(shot.Sector, from, to));
            foreach (var ship in _ships.Values)
            {
                if (ship.PeerId == shot.Owner || !ship.Alive || ship.Sector != shot.Sector || _controls[ship.PeerId].Protection > 0) continue;
                float t = SphereHitFraction(from, to, ship.Position, ShipRadius);
                if (t >= 0 && t < closestT)
                { closest = ship; closestT = t; }
            }
            shot.Position = to; shot.Life -= StepSeconds;
            if (closest != null) { ApplyDamage(closest, 24, shot.Owner); Hits++; shot.Life = 0; }
            else if (closestT <= 1) shot.Life = 0;
            if (shot.Life <= 0) _shots.RemoveAt(i);
        }
    }

    internal static float SphereHitFraction(Vector3 from, Vector3 to, Vector3 center, float radius)
    {
        Vector3 delta = to - from, offset = from - center;
        float a = delta.LengthSquared(), c = offset.LengthSquared() - radius * radius;
        if (c <= 0) return 0;
        if (a < .000001f) return -1;
        float b = offset.Dot(delta), discriminant = b * b - a * c;
        if (discriminant < 0) return -1;
        float hit = (-b - Mathf.Sqrt(discriminant)) / a;
        return hit >= 0 && hit <= 1 ? hit : -1;
    }

    public static float SurfaceHeight(int sector, float x, float z)
    {
        float height = SurfaceMeshHeight(sector, x, z);
        return sector == 1 ? Math.Max(0, height) : height;
    }

    private static float SurfaceMeshHeight(int sector, float x, float z)
    {
        if (sector == 3) return -10000;
        const float grid = 125;
        float x0 = Mathf.Floor((x + 10000) / grid) * grid - 10000, z0 = Mathf.Floor((z + 10000) / grid) * grid - 10000;
        float tx = (x - x0) / grid, tz = (z - z0) / grid;
        float h00 = SectorCatalog.GroundHeight(sector, x0, z0), h10 = SectorCatalog.GroundHeight(sector, x0 + grid, z0);
        float h01 = SectorCatalog.GroundHeight(sector, x0, z0 + grid), h11 = SectorCatalog.GroundHeight(sector, x0 + grid, z0 + grid);
        float height = tx + tz <= 1 ? h00 + (h10 - h00) * tx + (h01 - h00) * tz : h11 + (h01 - h11) * (1 - tx) + (h10 - h11) * (1 - tz);
        return height;
    }

    internal static float TerrainHitFraction(int sector, Vector3 from, Vector3 to)
    {
        if (sector == 3) return 2;
        // The saved mesh is piecewise planar on a 125 m grid with an x+z
        // diagonal. Split at every crossed triangle edge, so a ridge between
        // two clear endpoints still occludes the shot, independent of speed.
        var cuts = new List<float> { 0, 1 };
        void GridCuts(float a, float b)
        {
            if (Math.Abs(a - b) < .00001f) return;
            int first = (int)Math.Floor(Math.Min(a, b) / 125) + 1;
            int last = (int)Math.Ceiling(Math.Max(a, b) / 125) - 1;
            for (int grid = first; grid <= last; grid++) cuts.Add((grid * 125 - a) / (b - a));
        }
        GridCuts(from.X, to.X); GridCuts(from.Z, to.Z); GridCuts(from.X + from.Z, to.X + to.Z);
        cuts.Sort();
        float closest = 2;
        if (sector == 1)
        {
            if (from.Y <= 0) closest = 0;
            else if (to.Y <= 0) closest = from.Y / (from.Y - to.Y);
        }
        for (int i = 1; i < cuts.Count; i++)
        {
            float a = cuts[i - 1], b = cuts[i];
            var p = from.Lerp(to, a); var q = from.Lerp(to, b);
            float aboveP = p.Y - SurfaceMeshHeight(sector, p.X, p.Z);
            float aboveQ = q.Y - SurfaceMeshHeight(sector, q.X, q.Z);
            if (aboveP <= 0) return Math.Min(closest, a);
            if (aboveQ <= 0) return Math.Min(closest, Mathf.Lerp(a, b, aboveP / (aboveP - aboveQ)));
        }
        return closest;
    }

    private void ApplyDamage(ArenaShip ship, float damage, int attacker)
    {
        var c = _controls[ship.PeerId];
        if (!ship.Alive || c.Protection > 0) return;
        c.LastDamage = Elapsed;
        if (c.WarpTarget >= 0) { c.WarpTarget = -1; ship.WarpCharge = 0; }
        float blocked = Math.Min(ship.Shield, damage); ship.Shield -= blocked;
        ship.Health = Math.Max(0, ship.Health - (damage - blocked));
        if (ship.Health > 0) return;
        ship.Alive = false; ship.Deaths++; ship.RespawnRemaining = 5; ship.Boosting = false;
        if (_ships.TryGetValue(attacker, out var winner) && attacker != ship.PeerId)
        { winner.Kills++; Notice?.Invoke($"{winner.Callsign} 击落了 {ship.Callsign}"); }
        else Notice?.Invoke($"{ship.Callsign} 机体损毁，5 秒后重生");
    }

    private Vector3 SafeSpawn(int sector, int slot)
    {
        Vector3 preferred = SectorCatalog.Spawn(sector, slot);
        for (int attempt = 0; attempt < 32; attempt++)
        {
            var candidate = preferred + new Vector3(Mathf.Sin(attempt * 2.4f) * attempt * 75, attempt * 35, Mathf.Cos(attempt * 2.4f) * attempt * 75);
            if (!_obstacles.Intersects(sector, candidate, candidate + Vector3.Forward * 100, ShipRadius * 2, out _)) return candidate;
        }
        return preferred + Vector3.Up * 2000;
    }

    private void Respawn(ArenaShip ship, Control c)
    {
        ship.Alive = true; ship.Health = 100; ship.Shield = 50; ship.Energy = 100;
        ship.Position = SafeSpawn(ship.Sector, c.Slot); ship.Rotation = Quaternion.Identity;
        ship.Velocity = Vector3.Forward * 150; ship.Speed = 150; ship.WarpCharge = 0;
        c.Protection = 2; c.WarpTarget = -1; c.Command = ArenaCommand.Neutral;
    }
}

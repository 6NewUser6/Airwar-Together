using Godot;
using System;
using System.Collections.Generic;

namespace Airwar.Core;

public enum Difficulty { Easy, Normal, Hard, Inferno }
public enum ThreatKind { Beam, Projectile }
public enum SupplyKind { Repair, Credits }
public enum FlightEventKind { Hit, Collect, Boost, Pulse, NearMiss, Finished }
public readonly record struct FlightEvent(FlightEventKind Kind, Vector3 Position);

// Commands and authoritative state contain no scene nodes. A network authority can
// later validate these commands without loading a renderer or UI.
public readonly record struct FlightCommand(Vector3 Direction, Vector3? Target, bool Boost, bool Pulse);

public sealed class Threat
{
    public int Id;
    public ThreatKind Kind;
    public Vector3 Start, End, Position, Velocity;
    public float Age, Warning, Lifetime;
    public bool Spent, Grazed;
    public bool IsLive => Kind == ThreatKind.Projectile || (Age >= Warning && Age < Warning + .42f);
}

public sealed class Supply
{
    public int Id;
    public SupplyKind Kind;
    public Vector3 Position;
    public float Age;
}

public sealed record SurvivalRules(float SpawnInterval, float Warning, float BulletSpeed, int InitialHealth)
{
    public static SurvivalRules For(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => new(1.35f, 1.1f, 4.4f, 5),
        Difficulty.Normal => new(1f, .95f, 5.2f, 5),
        Difficulty.Hard => new(.75f, .8f, 6f, 5),
        _ => new(.56f, .7f, 6.8f, 1)
    };
}

public static class SpatialMath
{
    public static float PointSegmentDistance(Vector3 point, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        float length = ab.LengthSquared();
        if (length < .000001f) return point.DistanceTo(a);
        float t = Mathf.Clamp((point - a).Dot(ab) / length, 0, 1);
        return point.DistanceTo(a + ab * t);
    }

    // Closest points between finite 3D segments, including zero-length segments.
    public static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        var d1 = q1 - p1; var d2 = q2 - p2; var r = p1 - p2;
        float a = d1.Dot(d1), e = d2.Dot(d2), f = d2.Dot(r), s, t;
        const float epsilon = .000001f;
        if (a <= epsilon && e <= epsilon) return p1.DistanceTo(p2);
        if (a <= epsilon) { s = 0; t = Mathf.Clamp(f / e, 0, 1); }
        else
        {
            float c = d1.Dot(r);
            if (e <= epsilon) { t = 0; s = Mathf.Clamp(-c / a, 0, 1); }
            else
            {
                float b = d1.Dot(d2), denominator = a * e - b * b;
                s = denominator > epsilon ? Mathf.Clamp((b * f - c * e) / denominator, 0, 1) : 0;
                t = (b * s + f) / e;
                if (t < 0) { t = 0; s = Mathf.Clamp(-c / a, 0, 1); }
                else if (t > 1) { t = 1; s = Mathf.Clamp((b - c) / a, 0, 1); }
            }
        }
        return (p1 + d1 * s).DistanceTo(p2 + d2 * t);
    }

    public static bool IsFinite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}

public sealed class GameSession
{
    public const float StepSeconds = 1f / 60f;
    public const float FlightHeight = 1.35f;
    public const float HalfWidth = 10f, HalfDepth = 5.8f;
    public Vector3 PlayerPosition { get; private set; } = new(0, FlightHeight, 3.5f);
    public Vector3 PlayerVelocity { get; private set; }
    public int Health { get; private set; }
    public int Tick { get; private set; }
    public float Elapsed { get; private set; }
    public float Invulnerability { get; private set; }
    public float BoostCooldown { get; private set; }
    public float PulseCooldown { get; private set; }
    public float BoostRemaining { get; private set; }
    public int CreditsCollected { get; private set; }
    public int NearMisses { get; private set; }
    public int Score => (int)(Elapsed * 100) + NearMisses * 50;
    public int Reward => CreditsCollected + (int)(Elapsed / 10) * 2;
    public bool Finished { get; private set; }
    public Difficulty Difficulty { get; }
    public SurvivalRules Rules { get; }
    public List<Threat> Threats { get; } = new();
    public List<Supply> Supplies { get; } = new();
    public List<FlightEvent> Events { get; } = new();
    public int Wave => 1 + (int)(Elapsed / 20);
    private readonly Random _random;
    private float _spawnTimer = 2.2f, _trackingTimer = 5f, _supplyTimer = 6f;
    private int _nextId;
    private Vector3 _boostDirection = Vector3.Forward;

    public GameSession(Difficulty difficulty, int seed)
    {
        Difficulty = difficulty; Rules = SurvivalRules.For(difficulty);
        Health = Rules.InitialHealth; _random = new Random(seed);
    }

    public void Step(FlightCommand command)
    {
        Events.Clear();
        if (Finished) return;
        const float dt = StepSeconds;
        Tick++; Elapsed += dt;
        Invulnerability = Mathf.Max(0, Invulnerability - dt);
        BoostCooldown = Mathf.Max(0, BoostCooldown - dt);
        PulseCooldown = Mathf.Max(0, PulseCooldown - dt);
        BoostRemaining = Mathf.Max(0, BoostRemaining - dt);
        Vector3 previous = PlayerPosition;
        var direction = SpatialMath.IsFinite(command.Direction) ? command.Direction : Vector3.Zero;
        direction.Y = 0;
        if (command.Target is Vector3 target && SpatialMath.IsFinite(target))
        {
            var displacement = target - PlayerPosition; displacement.Y = 0;
            float distance = displacement.Length();
            direction = distance > .0001f ? displacement / distance * Mathf.Min(1, distance / (12 * dt)) : Vector3.Zero;
        }
        if (direction.LengthSquared() > 1) direction = direction.Normalized();
        if (command.Boost && BoostCooldown <= 0)
        {
            _boostDirection = direction.LengthSquared() > .01f ? direction.Normalized() : Vector3.Forward;
            BoostRemaining = .25f; BoostCooldown = 3.5f;
            Invulnerability = Mathf.Max(Invulnerability, .4f);
            Events.Add(new(FlightEventKind.Boost, PlayerPosition));
        }
        PlayerVelocity = BoostRemaining > 0 ? _boostDirection * 24 : direction * 12;
        var next = PlayerPosition + PlayerVelocity * dt;
        // This is the current mode's plane constraint, not a 2D render representation.
        PlayerPosition = new(Mathf.Clamp(next.X, -HalfWidth, HalfWidth), FlightHeight, Mathf.Clamp(next.Z, -HalfDepth, HalfDepth));
        if (command.Pulse && PulseCooldown <= 0)
        {
            PulseCooldown = 12;
            Threats.RemoveAll(t => t.Kind == ThreatKind.Projectile && t.Position.DistanceTo(PlayerPosition) < 7);
            Invulnerability = Mathf.Max(Invulnerability, .65f);
            Events.Add(new(FlightEventKind.Pulse, PlayerPosition));
        }
        _spawnTimer -= dt; _trackingTimer -= dt; _supplyTimer -= dt;
        float intensity = Mathf.Min(1.65f, 1 + Elapsed / 110f);
        if (_spawnTimer <= 0)
        {
            _spawnTimer += Rules.SpawnInterval / intensity;
            SpawnBeam(false); SpawnProjectile(intensity);
            if (Wave >= 3) SpawnProjectile(intensity);
        }
        if (_trackingTimer <= 0)
        {
            _trackingTimer += Rules.SpawnInterval * 3.7f / intensity;
            SpawnBeam(true);
        }
        if (_supplyTimer <= 0)
        {
            _supplyTimer += 8.5f;
            Supplies.Add(new() { Id = ++_nextId, Kind = Health < 4 && _random.NextDouble() < .5 ? SupplyKind.Repair : SupplyKind.Credits,
                Position = new(Range(-8, 8), FlightHeight, Range(-4.5f, 4.5f)) });
        }
        foreach (var threat in Threats)
        {
            Vector3 old = threat.Position;
            threat.Age += dt;
            if (threat.Kind == ThreatKind.Projectile) threat.Position += threat.Velocity * dt;
            if (!threat.IsLive || threat.Spent) continue;
            float distance = threat.Kind == ThreatKind.Beam
                ? SpatialMath.SegmentDistance(previous, PlayerPosition, threat.Start, threat.End)
                : SpatialMath.PointSegmentDistance(Vector3.Zero, previous - old, PlayerPosition - threat.Position);
            float hitRadius = threat.Kind == ThreatKind.Beam ? .48f : .55f;
            if (distance <= hitRadius && Invulnerability <= 0)
            {
                threat.Spent = true; Damage();
            }
            else if (distance > hitRadius && distance < hitRadius + .38f && !threat.Grazed && Invulnerability <= 0)
            {
                threat.Grazed = true; NearMisses++;
                Events.Add(new(FlightEventKind.NearMiss, PlayerPosition));
            }
        }
        foreach (var supply in Supplies)
        {
            if (Finished) break;
            supply.Age += dt;
            if (supply.Age < 0 || SpatialMath.PointSegmentDistance(supply.Position, previous, PlayerPosition) > .95f) continue;
            if (supply.Kind == SupplyKind.Repair) Health = Mathf.Min(5, Health + 1);
            else CreditsCollected += 10;
            supply.Age = -1;
            Events.Add(new(FlightEventKind.Collect, supply.Position));
        }
        Supplies.RemoveAll(s => s.Age < 0 || s.Age > 9);
        Threats.RemoveAll(t => t.Age >= t.Lifetime || (t.Kind == ThreatKind.Projectile && (Mathf.Abs(t.Position.X) > 14 || Mathf.Abs(t.Position.Z) > 10)));
    }

    private void Damage()
    {
        Health--; Invulnerability = 2;
        Events.Add(new(FlightEventKind.Hit, PlayerPosition));
        if (Health <= 0) { Finished = true; Events.Add(new(FlightEventKind.Finished, PlayerPosition)); }
    }

    private Vector3 Edge()
    {
        return _random.Next(4) switch
        {
            0 => new(-11.5f, FlightHeight, Range(-6.5f, 6.5f)),
            1 => new(11.5f, FlightHeight, Range(-6.5f, 6.5f)),
            2 => new(Range(-10.5f, 10.5f), FlightHeight, -7.5f),
            _ => new(Range(-10.5f, 10.5f), FlightHeight, 7.5f)
        };
    }

    private void SpawnBeam(bool tracking)
    {
        var start = Edge(); var end = Edge();
        if (tracking) end = start + (PlayerPosition - start).Normalized() * 30;
        else if (start.DistanceTo(end) < 8) end = new(-start.X, FlightHeight, -start.Z);
        Threats.Add(new() { Id = ++_nextId, Kind = ThreatKind.Beam, Start = start, End = end,
            Warning = Rules.Warning + (tracking ? .12f : 0), Lifetime = Rules.Warning + .65f });
    }

    private void SpawnProjectile(float intensity)
    {
        var start = Edge(); var target = PlayerPosition + new Vector3(Range(-3, 3), 0, Range(-2, 2));
        Threats.Add(new() { Id = ++_nextId, Kind = ThreatKind.Projectile, Position = start,
            Velocity = (target - start).Normalized() * Rules.BulletSpeed * intensity, Lifetime = 8 });
    }

    private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
}

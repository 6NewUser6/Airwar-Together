using Godot;
using System.Collections.Generic;

namespace Airwar.Arena;

// Shared contracts for simulation, transport, presentation and input. Distances are metres.
public readonly record struct ArenaCommand(float Pitch, float Yaw, float Roll, float Throttle,
    bool Fire, bool Boost, bool Warp, int Destination)
{
    public static ArenaCommand Neutral => new(0, 0, 0, 0, false, false, false, 0);
}

public sealed class ArenaShip
{
    public int PeerId { get; set; }
    public string Callsign { get; set; } = "Pilot";
    public string ShipId { get; set; } = "spectre";
    public int Sector { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public Quaternion Rotation { get; set; } = Quaternion.Identity;
    public float Health { get; set; } = 100;
    public float Shield { get; set; } = 50;
    public float Energy { get; set; } = 100;
    public float Speed { get; set; }
    public bool Alive { get; set; } = true;
    public bool Boosting { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public float RespawnRemaining { get; set; }
    public float WarpCharge { get; set; }
    public uint LastInput { get; set; }
}

public sealed class ArenaShot
{
    public int Id { get; set; }
    public int Owner { get; set; }
    public int Sector { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public float Life { get; set; }
}

public static class SectorCatalog
{
    public const int Count = 4;
    public const float HalfExtent = 10000;
    public static readonly string[] Names = { "阿斯特里昂 · 峡谷前哨", "涅瑞伊德 · 冰海群岛", "赫利俄斯 · 火山工业区", "深空 · 曙光航道" };
    public static readonly string[] Descriptions = {
        "砂岩峡谷与采矿前哨。利用山脊遮断追击，小心贴地飞行。",
        "冰架、海面与群岛。低空航线开阔，水面下无法飞行。",
        "火山口与地热设施。熔岩低空区域会持续灼伤机体。",
        "空间站与小行星航道。无地面限制，留意三维方向。"
    };
    public static readonly string[] ScenePaths = {
        "res://scenes/arena/sectors/Asterion.tscn", "res://scenes/arena/sectors/Nereid.tscn",
        "res://scenes/arena/sectors/Helios.tscn", "res://scenes/arena/sectors/DeepSpace.tscn"
    };
    public static string Name(int sector) => Names[Mathf.Clamp(sector, 0, Count - 1)];

    // The offline terrain baker uses precisely these functions. Keep physics and mesh aligned.
    public static float GroundHeight(int sector, float x, float z)
    {
        return sector switch
        {
            0 => 180 + 100 * Mathf.Sin(x * .0009f) * Mathf.Cos(z * .00075f)
                + 85 * Mathf.Sin((x + z) * .002f)
                + 120 * Mathf.Pow(Mathf.Abs(Mathf.Sin(x * .0012f + Mathf.Sin(z * .001f))), 6),
            1 => -45 + 38 * Mathf.Sin(x * .0016f) * Mathf.Cos(z * .0011f)
                + 85 * Mathf.Pow(Mathf.Max(0, Mathf.Sin(x * .0007f + Mathf.Cos(z * .0013f))), 6),
            2 => 110 + 100 * Mathf.Sin(x * .001f) * Mathf.Cos(z * .0008f)
                + 450 * Mathf.Exp(-((x - 1800) * (x - 1800) + (z + 800) * (z + 800)) / 2000000f),
            _ => -10000
        };
    }

    public static float FlightFloor(int sector, float x, float z) => sector == 1
        ? Mathf.Max(0, GroundHeight(sector, x, z)) : GroundHeight(sector, x, z);
    public static Vector3 Spawn(int sector, int slot) => new(
        Mathf.Sin(slot * 2.399963f) * (250 + slot * 22),
        (sector == 3 ? 0 : FlightFloor(sector, Mathf.Sin(slot * 2.399963f) * (250 + slot * 22), Mathf.Cos(slot * 2.399963f) * (250 + slot * 22))) + 350,
        Mathf.Cos(slot * 2.399963f) * (250 + slot * 22));
}

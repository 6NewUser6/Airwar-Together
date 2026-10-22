using Godot;

namespace Airwar.Core;

public sealed record ShipDefinition(string Id, string Name, string Role, Color Color, int Shape, int Cost);

public static class ShipCatalog
{
    public static readonly ShipDefinition[] All =
    {
        new("spectre", "幽灵", "SPECTRE / 截击型", new("56def0"), 0, 0),
        new("ember", "余烬", "EMBER / 突击型", new("ff9b60"), 1, 25),
        new("wraith", "夜魇", "WRAITH / 隐形型", new("b8a0ff"), 2, 25),
        new("stratus", "云层", "STRATUS / 侦察型", new("9ac8ef"), 3, 25),
        new("manta", "蝠鲼", "MANTA / 重翼型", new("8ad3ab"), 4, 25),
        new("raptor", "猛禽", "RAPTOR / 截击型", new("e9d7a1"), 5, 25),
        new("ion", "离子", "ION / 能量涂装", new("70a1ff"), 0, 25),
        new("flare", "耀斑", "FLARE / 日冕涂装", new("f66c7c"), 1, 25),
        new("nebula", "星云", "NEBULA / 深空涂装", new("a977df"), 2, 25),
        new("arctic", "极地", "ARCTIC / 极昼涂装", new("e4eef5"), 3, 25),
        new("jade", "青玉", "JADE / 玉石涂装", new("51c8bb"), 4, 25),
        new("gold", "曙光", "AURORA / 曙光涂装", new("e6b563"), 5, 25)
    };
    public static ShipDefinition Find(string id) => System.Array.Find(All, s => s.Id == id) ?? All[0];
}

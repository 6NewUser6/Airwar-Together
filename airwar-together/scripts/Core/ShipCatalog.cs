using Godot;
using System.Linq;

namespace Airwar.Core;

public static class ShipCatalog
{
    public static readonly ShipDefinition[] All =
        GD.Load<ShipLibrary>("res://assets/catalog/ShipLibrary.tres").Ships.ToArray();

    public static ShipDefinition Find(string id) => System.Array.Find(All, s => s.Id == id) ?? All[0];
}

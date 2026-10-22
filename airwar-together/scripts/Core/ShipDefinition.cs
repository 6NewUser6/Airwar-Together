using Godot;

namespace Airwar.Core;

/// <summary>Editable appearance, identity and unlock price for a reusable ship scene.</summary>
[GlobalClass]
public partial class ShipDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string Name { get; set; } = "";
    [Export] public string Role { get; set; } = "";
    [Export] public Color Color { get; set; } = Colors.White;
    [Export] public int Shape { get; set; }
    [Export(PropertyHint.Range, "0,1000,1")] public int Cost { get; set; }
    [Export] public PackedScene VisualScene { get; set; } = null!;
}

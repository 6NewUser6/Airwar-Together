using Godot;

namespace Airwar.Core;

/// <summary>The authored ship list shared by gameplay, the hangar and profile validation.</summary>
[GlobalClass]
public partial class ShipLibrary : Resource
{
    [Export] public Godot.Collections.Array<ShipDefinition> Ships { get; set; } = new();
}

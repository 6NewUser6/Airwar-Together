using Godot;
using System.Linq;

namespace Airwar.Arena;

// A lightweight entry scene keeps the real multi-process network test independent of world rendering and audio.
public partial class ArenaCheckEntry : Node
{
    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--arena-self-test")) { GetTree().Quit(ArenaNetworkChecks.RunSelfTests() ? 0 : 1); return; }
        if (!ArenaNetworkChecks.TryRun(this, args)) { GD.PushError("Arena check mode missing"); GetTree().Quit(2); }
    }
}

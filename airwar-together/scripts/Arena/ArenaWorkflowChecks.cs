using Godot;
using System;
using System.Linq;

namespace Airwar.Arena;

public partial class ArenaController
{
    private async void RunWorkflowChecks()
    {
        int checks = 0;
        ArenaNetwork? remote = null;
        void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); GD.Print("PASS_ARENA_WORKFLOW ", label); checks++; }
        async System.Threading.Tasks.Task Frames(int count = 4)
        { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
        async System.Threading.Tasks.Task Until(Func<bool> condition, string label, int frames = 600)
        { for (int i = 0; i < frames && !condition(); i++) await Frames(1); Check(condition(), label); }
        void Press(string path) => _hud.GetNode<Button>(path).EmitSignal(Button.SignalName.Pressed);
        void Inject(InputEvent input) => Input.ParseInputEvent(input.XformedBy(GetViewport().GetFinalTransform()));
        void Touch(int index, Vector2 position, bool pressed) => Inject(new InputEventScreenTouch { Index = index, Position = position, Pressed = pressed });
        try
        {
            await Frames();
            Check(_hud.InLobby && !_network.IsConnected && _world.Camera.Current, "lobby opens with live scene preview");
            foreach (int sector in Enumerable.Range(0, SectorCatalog.Count))
            {
                _world.SetPreviewSector(sector); await Frames(2);
                Check(_world.DisplayedSector == sector, "authored sector loads: " + sector);
            }
            _world.SetPreviewSector(0);
            _hud.SetEndpoint("127.0.0.1", 100);
            Press("Lobby/Panel/Column/Host"); await Frames();
            Check(!_network.IsConnected, "invalid port is rejected before creating transport");
            _hud.SetEndpoint("127.0.0.1", 24691);
            Press("Lobby/Panel/Column/Host"); await Frames();
            Check(_network.IsConnected && !_hud.InLobby && _input.Enabled, "host enters flight through lobby button");
            Vector3 start = _network.Ships[_network.LocalPeerId].Position;
            await Frames(40);
            Check(_network.Ships[_network.LocalPeerId].Position.DistanceTo(start) > 60, "flight advances in authored world");
            remote = new ArenaNetwork(); AddChild(remote);
            Check(remote.Join("127.0.0.1", 24691, "Wingmate", "ember") == Error.Ok, "second pilot starts real ENet join");
            await Until(() => remote.IsConnected && remote.PlayerCount == 2 && _network.PlayerCount == 2, "both peers see two-person roster");
            await Frames(15);
            Check(!_hud.InLobby && !_hud.MenuVisible && _input.Enabled, "joining pilot preserves host flight UI");
            Check(_world.GetNode("Actors").GetChildCount() == 2, "both network aircraft are rendered");
            Press("Flight/Camera"); await Frames(); Check(_world.Cockpit, "camera button enables cockpit view");
            Press("Flight/Camera"); await Frames(); Check(!_world.Cockpit, "camera button restores chase view");

            _hud.TouchLayout = true;
            foreach (string name in new[] { "Fire", "Boost", "RollLeft", "RollRight" }) _hud.GetNode<Button>("Flight/" + name).Show();
            Vector2 view = GetViewport().GetVisibleRect().Size;
            Vector2 stick = new(view.X * .2f, view.Y * .72f);
            Vector2 fire = _hud.GetNode<Button>("Flight/Fire").GetGlobalRect().GetCenter();
            Vector2 boost = _hud.GetNode<Button>("Flight/Boost").GetGlobalRect().GetCenter();
            Touch(0, stick, true); await Frames();
            Inject(new InputEventScreenDrag { Index = 0, Position = stick + new Vector2(35,-25), Relative = new(35,-25) });
            Touch(1, fire, true); Touch(2, boost, true); await Frames(12);
            Check(_input.HasStick && _input.Stick.Length() > .2f, "owned steering finger remains active with two action fingers");
            var sampled = _input.Sample(0);
            Check(sampled.Fire && sampled.Boost, "touch fire and boost can be held simultaneously");
            Touch(0, fire, false); await Frames();
            Check(!_input.HasStick && _input.Sample(0).Fire, "steering release over fire button keeps other finger ownership");
            Touch(1, stick, false); Touch(2, stick, false); await Frames();
            Check(!_input.Sample(0).Fire && !_input.Sample(0).Boost, "action release outside its button clears held commands");
            _input.Reset();

            Press("Flight/Top/Menu"); await Frames();
            Check(_hud.MenuVisible && !_input.Enabled && Input.MouseMode == Input.MouseModeEnum.Visible, "menu releases pointer and disables input");
            Press("Menu/Center/Panel/Column/Resume"); await Frames();
            Check(!_hud.MenuVisible && _input.Enabled, "resume restores controls");
            _hud.GetNode<OptionButton>("Flight/Navigation/Destination").Select(3);
            Press("Flight/Navigation/Warp");
            await Until(() => _network.Ships[_network.LocalPeerId].Sector == 3, "navigation button completes authoritative sector warp", 300);
            await Frames(10);
            Check(_world.DisplayedSector == 3 && _network.Ships[_network.LocalPeerId].Energy < 100, "warp changes scene and consumes energy");
            remote.Disconnect();
            await Until(() => _network.PlayerCount == 1, "leaving remote pilot frees the room slot");
            Press("Flight/Top/Menu"); Press("Menu/Center/Panel/Column/Leave"); await Frames();
            Check(_hud.InLobby && !_network.IsConnected && !_input.Enabled, "leave room restores editable lobby");
            GD.Print("ARENA_WORKFLOW_OK ", checks);
            remote.QueueFree(); Quit();
        }
        catch (Exception error)
        {
            GD.PushError("ARENA_WORKFLOW_FAILED " + error); remote?.Disconnect();
            _input.Reset(); _network.Disconnect();
            await _audio.StopAndDrainAsync(); GetTree().Quit(1);
        }
    }
}

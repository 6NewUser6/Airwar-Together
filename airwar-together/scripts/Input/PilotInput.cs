using Godot;
using Airwar.Core;
using Airwar.Presentation;

namespace Airwar.Inputs;

public sealed class PilotInput
{
    private Vector3? _target;
    private int _finger = -1;
    private Vector2 _lastTouch;
    private bool _boost, _pulse;
    public float Sensitivity { get; set; } = 1.15f;
    public bool TouchActive => _finger >= 0;
    public bool OwnsTouch(int index) => _finger == index;
    public Vector2 TouchPosition => _lastTouch;

    public void Reset() { _target = null; _finger = -1; _boost = _pulse = false; }
    public void Boost() => _boost = true;
    public void Pulse() => _pulse = true;

    public void Handle(InputEvent inputEvent, CombatWorld world, GameSession session)
    {
        if (inputEvent is InputEventMouseMotion mouse && mouse.Device != InputEvent.DeviceIdEmulation && _finger < 0) _target = world.ScreenToFlight(mouse.Position);
        if (inputEvent is InputEventScreenTouch touch)
        {
            if ((!touch.Pressed || touch.Canceled) && touch.Index == _finger)
            {
                _finger = -1; _target = null;
            }
            else if (touch.Pressed && !touch.Canceled && _finger < 0)
            {
                _finger = touch.Index; _lastTouch = touch.Position; _target = session.PlayerPosition;
            }
        }
        if (inputEvent is InputEventScreenDrag drag && drag.Index == _finger)
        {
            var movement = world.ScreenDeltaToFlight(_lastTouch, drag.Position);
            _target = (_target ?? session.PlayerPosition) + movement * Sensitivity;
            if (_target is Vector3 target) _target = new Vector3(Mathf.Clamp(target.X, -GameSession.HalfWidth, GameSession.HalfWidth), GameSession.FlightHeight, Mathf.Clamp(target.Z, -GameSession.HalfDepth, GameSession.HalfDepth));
            _lastTouch = drag.Position;
        }
        if (inputEvent is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.PhysicalKeycode == Key.Shift) _boost = true;
            if (key.PhysicalKeycode == Key.Space) _pulse = true;
        }
    }

    public FlightCommand Sample()
    {
        float x = (Pressed(Key.D, Key.Right) ? 1 : 0) - (Pressed(Key.A, Key.Left) ? 1 : 0);
        float z = (Pressed(Key.S, Key.Down) ? 1 : 0) - (Pressed(Key.W, Key.Up) ? 1 : 0);
        var direction = new Vector3(x, 0, z);
        if (direction.LengthSquared() > 0) _target = null;
        var command = new FlightCommand(direction, _target, _boost, _pulse);
        _boost = _pulse = false; return command;
    }

    private static bool Pressed(Key a, Key b) => Godot.Input.IsPhysicalKeyPressed(a) || Godot.Input.IsPhysicalKeyPressed(b);
}

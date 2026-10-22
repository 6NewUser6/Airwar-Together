using Godot;
using System.Collections.Generic;

namespace Airwar.Arena;

/// <summary>One command contract for desktop controls and independently owned touch fingers.</summary>
public sealed class ArenaPilotInput
{
    private int _stickFinger = -1;
    private Vector2 _stickOrigin, _stick, _mouse;
    private readonly Dictionary<int, string> _actions = new();
    private bool _warp;
    public float Throttle { get; set; } = .5f;
    public int Destination { get; set; } = 3;
    public Vector2 Stick => _stick;
    public Vector2 StickOrigin => _stickOrigin;
    public bool HasStick => _stickFinger >= 0;
    public bool MouseSteering { get; private set; }
    public bool Enabled { get; set; }

    public void RequestWarp() => _warp = true;
    public void Reset()
    {
        _actions.Clear(); _stickFinger = -1; _stick = _mouse = Vector2.Zero; _warp = false;
        MouseSteering = false; Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public bool Handle(InputEvent input, Vector2 viewport, IReadOnlyDictionary<string, Rect2> actions, IReadOnlyList<Rect2> reserved)
    {
        if (!Enabled) return false;
        if (input is InputEventScreenTouch touch)
        {
            if (!touch.Pressed || touch.Canceled)
            {
                bool owned = _actions.Remove(touch.Index);
                if (_stickFinger == touch.Index) { _stickFinger = -1; _stick = Vector2.Zero; owned = true; }
                return owned;
            }
            foreach (var action in actions)
                if (action.Value.HasPoint(touch.Position)) { _actions[touch.Index] = action.Key; return true; }
            foreach (var rect in reserved) if (rect.HasPoint(touch.Position)) return false;
            if (_stickFinger < 0 && touch.Position.X < viewport.X * .47f && touch.Position.Y > viewport.Y * .32f)
            {
                _stickFinger = touch.Index; _stickOrigin = touch.Position; _stick = Vector2.Zero; return true;
            }
        }
        if (input is InputEventScreenDrag drag)
        {
            if (_stickFinger == drag.Index)
            {
                float radius = Mathf.Clamp(viewport.Y * .14f, 65, 110);
                _stick = ((drag.Position - _stickOrigin) / radius).LimitLength(); return true;
            }
            if (_actions.ContainsKey(drag.Index)) return true;
        }
        if (input is InputEventMouseButton button && button.ButtonIndex == MouseButton.Right)
        {
            MouseSteering = button.Pressed; _mouse = Vector2.Zero;
            Input.MouseMode = button.Pressed ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
            return true;
        }
        if (input is InputEventMouseMotion motion && MouseSteering)
        {
            _mouse = (_mouse + motion.Relative * .0035f).LimitLength(); return true;
        }
        if (input is InputEventKey key && key.Pressed && !key.Echo && key.PhysicalKeycode == Key.J)
        { RequestWarp(); return true; }
        return false;
    }

    private bool Held(string action) => _actions.ContainsValue(action);
    public ArenaCommand Sample(float dt, bool mouseOverControl = false)
    {
        if (!Enabled) return ArenaCommand.Neutral with { Throttle = Throttle, Destination = Destination };
        float KeyAxis(Key positive, Key negative) => (Input.IsPhysicalKeyPressed(positive) ? 1 : 0) - (Input.IsPhysicalKeyPressed(negative) ? 1 : 0);
        Throttle = Mathf.Clamp(Throttle + KeyAxis(Key.W, Key.S) * dt * .4f, 0, 1);
        float pitch = KeyAxis(Key.Up, Key.Down) - _stick.Y - _mouse.Y;
        float yaw = KeyAxis(Key.Q, Key.E) + KeyAxis(Key.Left, Key.Right) - _stick.X - _mouse.X;
        float roll = KeyAxis(Key.A, Key.D) + (Held("RollLeft") ? 1 : 0) - (Held("RollRight") ? 1 : 0);
        bool fire = Held("Fire") || Input.IsPhysicalKeyPressed(Key.Space) ||
            (!mouseOverControl && Input.IsMouseButtonPressed(MouseButton.Left) && _actions.Count == 0 && !HasStick);
        bool warp = _warp; _warp = false;
        return new(Mathf.Clamp(pitch, -1, 1), Mathf.Clamp(yaw, -1, 1), Mathf.Clamp(roll, -1, 1), Throttle,
            fire, Held("Boost") || Input.IsPhysicalKeyPressed(Key.Shift), warp, Destination);
    }
}

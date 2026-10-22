using Godot;
using System;

namespace Airwar.Arena;

/// <summary>Flight instruments and same-sector contacts projected from live network state.</summary>
public partial class ArenaFlightDisplay : Control
{
    public ArenaNetwork Network { get; set; } = null!;
    public ArenaWorld World { get; set; } = null!;
    public ArenaPilotInput PilotInput { get; set; } = null!;
    public bool Active { get; set; }
    private static readonly Color Cyan = new(.45f, .95f, .92f, .85f), Enemy = new(1, .45f, .32f, .9f);

    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (!Active || Network == null || World == null || !Network.Ships.TryGetValue(Network.LocalPeerId, out var local)) return;
        Vector2 center = Size * .5f;
        DrawLine(center + new Vector2(-22, 0), center + new Vector2(-7, 0), Cyan, 2, true);
        DrawLine(center + new Vector2(7, 0), center + new Vector2(22, 0), Cyan, 2, true);
        DrawLine(center + new Vector2(0, -22), center + new Vector2(0, -7), Cyan, 2, true);
        DrawCircle(center, 3, Cyan, false, 1.5f, true);
        Vector2 radar = new(Size.X - 110, Size.Y * .58f);
        DrawCircle(radar, 78, new Color(.02f,.05f,.08f,.65f));
        DrawCircle(radar, 78, Cyan with { A = .35f }, false, 1, true);
        DrawCircle(radar, 39, Cyan with { A = .2f }, false, 1, true);
        DrawLine(radar + new Vector2(0,-78), radar + new Vector2(0,78), Cyan with { A=.2f });
        DrawLine(radar + new Vector2(-78,0), radar + new Vector2(78,0), Cyan with { A=.2f });
        DrawCircle(radar, 3, Cyan);
        Basis inverse = new Basis(local.Rotation).Transposed();
        var camera = World.Camera;
        var toCanvas = GetGlobalTransformWithCanvas().AffineInverse();
        foreach (var ship in Network.Ships.Values)
        {
            if (ship.PeerId == local.PeerId || !ship.Alive || ship.Sector != local.Sector) continue;
            Vector3 relative = inverse * (ship.Position - local.Position);
            float distance = relative.Length();
            if (distance < 4000)
                DrawCircle(radar + new Vector2(relative.X, relative.Z) * (78 / 4000f), 3, Enemy);
            if (camera.IsPositionBehind(ship.Position)) continue;
            Vector2 point = toCanvas * camera.UnprojectPosition(ship.Position);
            if (point.X < 10 || point.Y < 65 || point.X > Size.X-10 || point.Y > Size.Y-45) continue;
            DrawRect(new Rect2(point - new Vector2(10,10), new Vector2(20,20)), Enemy, false, 1.5f);
            DrawString(GetThemeDefaultFont(), point + new Vector2(14, -10), ship.Callsign + $"  {distance:0} m", HorizontalAlignment.Left, -1, 14, Enemy);
        }
        var forward = local.Rotation * Vector3.Forward;
        float heading = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(forward.X, -forward.Z)), 360);
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-35, -80), $"{heading:000}°", HorizontalAlignment.Left, -1, 18, Cyan);
        if (PilotInput.HasStick)
        {
            Vector2 origin = toCanvas * PilotInput.StickOrigin;
            DrawCircle(origin, 64, Cyan with { A=.12f });
            DrawCircle(origin, 64, Cyan with { A=.5f }, false, 2, true);
            DrawCircle(origin + PilotInput.Stick * 55, 23, Cyan with { A=.45f });
        }
    }
}

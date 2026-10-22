using Godot;

namespace Airwar;

/// <summary>Draws the live player hit marker over the authored scene.</summary>
public partial class FlightMarker : Control
{
    public Main? Game { get; set; }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var session = Game?.Session;
        if (session == null || Game!.World.IsMenu) return;
        Vector2 point = Game.World.Camera.UnprojectPosition(session.PlayerPosition);
        var color = new Color("95f1f7");
        DrawCircle(point, 3, color);
        DrawArc(point, 9, 0, Mathf.Tau, 24, new Color(color, .4f), 1, true);
    }
}

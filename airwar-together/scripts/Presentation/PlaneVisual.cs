using Godot;
using Airwar.Core;

namespace Airwar.Presentation;

/// <summary>Animates a reusable ship scene; geometry and materials live in assets.</summary>
public partial class PlaneVisual : Node3D
{
    private Node3D _model = null!;
    private MeshInstance3D _leftExhaust = null!, _rightExhaust = null!;
    private Node3D _shield = null!;
    private Vector3 _leftExhaustScale, _rightExhaustScale;
    private ulong _visualSceneId;
    public Color Accent { get; private set; } = new("56def0");

    public override void _Ready() => BindModel(GetNode<Node3D>("Model"));

    public void Build(ShipDefinition ship)
    {
        Accent = ship.Color;
        var scene = ship.VisualScene ?? GD.Load<PackedScene>($"res://scenes/ships/{ship.Id}.tscn");
        // The catalog identity and its visual asset are independent. Changing a
        // definition's prefab must take effect even when its gameplay id is unchanged.
        if (_model != null && (_visualSceneId == scene.GetInstanceId() ||
            (_visualSceneId == 0 && !string.IsNullOrEmpty(scene.ResourcePath) && _model.SceneFilePath == scene.ResourcePath)))
        {
            _visualSceneId = scene.GetInstanceId();
            return;
        }
        var model = scene.Instantiate<Node3D>();
        model.Name = "Model";
        if (_model != null)
        {
            RemoveChild(_model);
            _model.QueueFree();
        }
        AddChild(model);
        BindModel(model);
        _visualSceneId = scene.GetInstanceId();
    }

    private void BindModel(Node3D model)
    {
        _model = model;
        _leftExhaust = model.GetNode<MeshInstance3D>("LeftExhaust");
        _rightExhaust = model.GetNode<MeshInstance3D>("RightExhaust");
        _leftExhaustScale = _leftExhaust.Scale;
        _rightExhaustScale = _rightExhaust.Scale;
        _shield = model.GetNode<Node3D>("Shield");
    }

    public void Animate(float time, Vector3 velocity, float protection, bool boosting)
    {
        Rotation = new(Mathf.Sin(time * 2) * .015f, 0, Mathf.Lerp(Rotation.Z, -velocity.X * .015f, .12f));
        float length = (boosting ? 2.8f : 1) + Mathf.Sin(time * 35) * .12f;
        _leftExhaust.Scale = _leftExhaustScale * new Vector3(1, 1, length);
        _rightExhaust.Scale = _rightExhaustScale * new Vector3(1, 1, length);
        _shield.Visible = protection > 0;
        _shield.Rotation = new(0, time * .8f, 0);
        Visible = protection < .1f || Mathf.Sin(time * 35) > -.6f;
    }
}

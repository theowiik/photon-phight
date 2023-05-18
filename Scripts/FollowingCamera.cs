﻿using System.Collections.Generic;
using Godot;

namespace PhotonPhighters.Scripts;

public partial class FollowingCamera : Camera2D
{
  public enum ShakeStrength
  {
    Weak,
    Medium,
    Strong,
    Uber
  }

  private readonly IList<Node2D> _targets = new List<Node2D>();
  private float _remainingShakeTime;
  private ShakeStrength _shakeStrength;
  private float _zoomFactor = 0.3f;

  public override void _PhysicsProcess(double delta)
  {
    if (_targets.Count == 0)
    {
      return;
    }

    HandleZoomInput();

    var targetPosition = Vector2.Zero;
    foreach (var target in _targets)
    {
      targetPosition += target.Position;
    }

    targetPosition /= _targets.Count;
    Position = Position.Lerp(targetPosition, (float)delta * 5.0f);

    // Camera shake
    if (_remainingShakeTime > 0)
    {
      var shakeOffset = ShakeStrengthToOffset(_shakeStrength);
      Position += new Vector2(GD.RandRange(-shakeOffset, shakeOffset), GD.RandRange(-shakeOffset, shakeOffset));
      _remainingShakeTime -= (float)delta;
    }

    // Zoom
    FitZoom();
  }

  private void HandleZoomInput()
  {
    if (Input.IsActionJustPressed("camera_zoom_in"))
    {
      _zoomFactor += 0.1f;
    }

    if (Input.IsActionJustPressed("camera_zoom_out"))
    {
      _zoomFactor -= 0.1f;
    }
  }

  public void AddTarget(Node2D target)
  {
    if (_targets.Contains(target))
    {
      return;
    }

    if (target == null)
    {
      return;
    }

    _targets.Add(target);
  }

  /// <summary>
  ///   Shake the camera for the given amount of time.
  /// </summary>
  /// <param name="shakeTime">
  ///   The amount of time to shake the camera.
  /// </param>
  /// <param name="strength">
  ///   The strength of the shake.
  /// </param>
  public void Shake(float shakeTime, ShakeStrength strength)
  {
    _remainingShakeTime = shakeTime;
    _shakeStrength = strength;
  }

  private static int ShakeStrengthToOffset(ShakeStrength strength)
  {
    return strength switch
    {
      ShakeStrength.Weak => 5,
      ShakeStrength.Medium => 10,
      ShakeStrength.Strong => 30,
      ShakeStrength.Uber => 150,
      _ => 0
    };
  }

  private void FitZoom()
  {
    // Calculate the bounding box of all objects in the list
    var bounds = new Rect2();
    foreach (var obj in _targets)
    {
      var rect = obj.GetViewportRect();
      rect.Position = obj.ToGlobal(rect.Position);
      bounds = bounds.Merge(rect);
    }

    // Calculate the target zoom level to fit the bounding box on the screen
    var screenBounds = GetViewportRect().Size;
    var targetZoom = Mathf.Min(screenBounds.X / bounds.Size.X, screenBounds.Y / bounds.Size.Y);

    // Smoothly adjust the camera's zoom level and set its offset to the center of the bounding box
    Zoom = Zoom.Lerp(new Vector2(targetZoom + _zoomFactor, targetZoom + _zoomFactor), 1);
  }
}

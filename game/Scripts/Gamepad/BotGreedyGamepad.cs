﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;

namespace PhotonPhighters.Scripts.Gamepad;

/// <summary>
///   Shoot towards the closest enemy.
/// </summary>
public sealed class BotGreedyGamepad : AbstractBotGamepad
{
  private const float JumpIntervalSeconds = 0.4f;
  private const float MarginDeg = (180 - JumpRangeDeg) / 2f;

  /// <summary>
  ///   The range in degrees from the top for which the bot will jump.
  /// </summary>
  private const int JumpRangeDeg = 166;

  private readonly Stopwatch _jumpTimer;
  private readonly List<Player> _opponents;
  private readonly Player _self;
  private Vector2 _aim;
  private Vector2 _movement;

  /// <summary>
  ///   Creates a new bot that shoots towards the closest enemy.
  /// </summary>
  /// <param name="self">
  ///   The bot itself.
  /// </param>
  public BotGreedyGamepad(Player self)
  {
    _self = self ?? throw new ArgumentNullException(nameof(self), "Self must not be null.");
    _opponents = new List<Player>();

    _jumpTimer = new Stopwatch();
    _jumpTimer.Start();
  }

  /// <summary>
  ///   Adds opponents to the bot's list of opponents.
  ///   May include the bot itself and teammates, will be filtered out.
  /// </summary>
  /// <param name="opponents">
  ///   The opponents to add.
  /// </param>
  public void AddOpponents(IEnumerable<Player> opponents)
  {
    if (opponents == null)
    {
      throw new ArgumentNullException(nameof(opponents), "Opponents must not be null.");
    }

    _opponents.AddRange(opponents.Where(p => p != _self).Where(p => p.Team != _self.Team));
  }

  public override void Vibrate() { }

  public override bool IsShootPressed()
  {
    Update();
    return true;
  }

  public override bool IsJumpPressed()
  {
    Update();

    if (_jumpTimer.Elapsed.TotalSeconds > JumpIntervalSeconds)
    {
      _jumpTimer.Restart();

      var angleDeg = Mathf.RadToDeg(_movement.Angle());
      return angleDeg is <= -MarginDeg and >= -(180 - MarginDeg);
    }

    return false;
  }

  public override Vector2 GetAim()
  {
    Update();
    return _aim;
  }

  public override Vector2 GetMovement()
  {
    Update();
    return _movement;
  }

  protected override void MakeDecision()
  {
    if (_opponents.Count == 0)
    {
      return;
    }

    var closestPlayer = _opponents.MinBy(p => p.GlobalPosition.DistanceSquaredTo(_self.GlobalPosition));
    if (closestPlayer == null)
    {
      _aim = Vector2.Zero;
      _movement = Vector2.Zero;
      return;
    }

    var direction = (closestPlayer.GlobalPosition - _self.GlobalPosition).Normalized();
    _aim = direction.Normalized();
    _movement = direction.Normalized();
  }
}

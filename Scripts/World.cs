﻿using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using PhotonPhighters.Scripts.OverlayControllers;
using PhotonPhighters.Scripts.Utils;

namespace PhotonPhighters.Scripts;

public partial class World : Node2D
{
  private const int RoundTime = 40;
  private const int ScoreToWin = 4;
  private const int TimeBetweenCapturePoint = 10;

  private readonly PackedScene _capturePointScene = GD.Load<PackedScene>("res://Objects/CapturePoint.tscn");

  private readonly PackedScene _explosionScene = GD.Load<PackedScene>("res://Objects/Explosion.tscn");

  private readonly PackedScene _ragdollDarkScene = GD.Load<PackedScene>(
    "res://Objects/Player/Ragdolls/RagdollDark.tscn"
  );

  private readonly PackedScene _ragdollLightScene = GD.Load<PackedScene>(
    "res://Objects/Player/Ragdolls/RagdollLight.tscn"
  );

  private readonly PackedScene _scene = GD.Load<PackedScene>("res://Objects/UI/DamageAmountIndicator.tscn");

  [GetNode("FollowingCamera")]
  private FollowingCamera _camera;

  private Player _darkPlayer;

  [GetNode("Sfx/DarkWin")]
  private AudioStreamPlayer _darkWin;

  private Player _lastPlayerToScore;
  private Player _lightPlayer;

  [GetNode("Sfx/LightWin")]
  private AudioStreamPlayer _lightWin;

  [GetNode("MapManager")]
  private MapManager _mapManager;

  [GetNode("CanvasLayer/Overlay")]
  private Overlay _overlay;

  [GetNode("CanvasLayer/PauseOverlay")]
  private PauseOverlay _pauseOverlay;

  private IEnumerable<Player> _players;

  [GetNode("CanvasLayer/PowerUpPicker")]
  private PowerUpPicker _powerUpPicker;

  [GetNode("RoundTimer")]
  private Timer _roundTimer;

  private Score _score;

  public override void _Ready()
  {
    this.AutoWire();
    _score = new Score();

    // UI
    var uiUpdateTimer = this.GetNodeOrExplode<Timer>("UIUpdateTimer");
    uiUpdateTimer.Timeout += UpdateScore;
    uiUpdateTimer.Timeout += UpdateRoundTimer;
    _roundTimer.Timeout += OnRoundFinished;
    _pauseOverlay.ResumeGame += TogglePause;
    _powerUpPicker.Visible = false;
    _powerUpPicker.PowerUpPickedListeners += OnPowerUpSelected;

    // Setup map
    _mapManager.OutOfBoundsEventListeners += OnOutOfBounds;

    // Setup players
    _players = GetTree().GetNodesInGroup("players").Cast<Player>();
    foreach (var player in _players)
    {
      player.Frozen = true;
      player.PlayerDied += OnPlayerDied;
      player.PlayerHurt += OnPlayerHurt;
      player.Gun.ShootDelegate += OnShoot;
      _camera.AddTarget(player);
      player.PlayerEffectAddedListeners += OnPlayerEffectAdded;
    }

    _lightPlayer = _players.First(p => p.PlayerNumber == 1);
    _darkPlayer = _players.First(p => p.PlayerNumber == 2);

    if (_lightPlayer == null || _darkPlayer == null)
    {
      throw new Exception("Could not find players");
    }

    // Start round
    SetupCapturePoint();
    StartRound();
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    if (@event.IsActionPressed("ui_down"))
    {
      _roundTimer.Start(0.00001);
    }

    if (@event.IsActionPressed("ui_left"))
    {
      _mapManager.InitNextMap();
    }
  }

  private static string GetRandomDeathMessage()
  {
    var deathMessages = new List<string>
    {
      "oof",
      "ouch",
      "ow",
      "yikes",
      "rip",
      "x_x",
      "-_-",
      "T_T",
      "u_u",
      "X_X",
      "@_@",
      "(X_X)",
      "[-_-]",
      "{x_x}",
      "[T_T]",
      "{@_@}",
      "<x_x>",
      "(.-.)",
      "[._.]",
      "<@_@>",
      "(xOx)",
      "[x_x]",
      "<-_->",
      "{-_-}",
      "(XoX)"
    };
    return deathMessages[GD.RandRange(0, deathMessages.Count - 1)];
  }

  private static void OnOutOfBounds(Player player)
  {
    if (player.Exists)
    {
      GD.Print("Player out of bounds!");
      GD.Print("Is alive: " + player.IsAlive);
      GD.Print("Is frozen: " + player.Frozen);
      GD.Print("Exists: " + player.Exists);
      player.TakeDamage(99999);
    }
  }

  private Results GetResults()
  {
    var lights = GetTree().GetNodesInGroup("lights");
    var results = new Results();

    return results;

    foreach (var light in lights)
    {
      if (light is not Light lightNode)
      {
        throw new Exception("Light node is not a Light!!");
      }

      switch (lightNode.LightState)
      {
        case Light.LightMode.Light:
          results.Light++;
          break;

        case Light.LightMode.Dark:
          results.Dark++;
          break;

        case Light.LightMode.None:
          results.Neutral++;
          break;
      }
    }

    return results;
  }

  private void OnCapturePointCaptured(CapturePoint which, Player.TeamEnum team)
  {
    var light = team == Player.TeamEnum.Light ? Light.LightMode.Light : Light.LightMode.Dark;
    SpawnExplosion(which, light, Explosion.ExplosionRadiusEnum.Large);
    which.QueueFree();
  }

  private void OnPlayerDied(Player player)
  {
    var oppositeLight = player.Team == Player.TeamEnum.Light ? Light.LightMode.Dark : Light.LightMode.Light;

    SpawnRagdoll(player);
    SpawnExplosion(player, oppositeLight, Explosion.ExplosionRadiusEnum.Medium);
    SpawnHurtIndicator(player, GetRandomDeathMessage());

    player.GlobalPosition =
      player.PlayerNumber == 1 ? _mapManager.LightSpawn.GlobalPosition : _mapManager.DarkSpawn.GlobalPosition;
    player.Frozen = true;

    var liveTimer = new Timer { OneShot = true, WaitTime = 2 };
    liveTimer.Timeout += () =>
    {
      player.Frozen = false;
      player.IsAlive = true;
    };
    AddChild(liveTimer);
    liveTimer.Start();
  }

  private void OnPlayerEffectAdded(Node2D effect, Player who)
  {
    AddChild(effect);
    effect.GlobalPosition = who.GlobalPosition;
  }

  private void OnPlayerHurt(Player player, int damage)
  {
    SpawnHurtIndicator(player, damage.ToString());
  }

  private void OnPowerUpSelected(PowerUpManager.IPowerUp powerUp)
  {
    _powerUpPicker.Visible = false;

    if (PowerUpPicker.DevMode)
    {
      powerUp.Apply(_lightPlayer);
      powerUp.Apply(_darkPlayer);
    }

    var loser = _lastPlayerToScore.Team == Player.TeamEnum.Light ? _darkPlayer : _lightPlayer;
    powerUp.Apply(loser);

    StartRound();
  }

  private void OnRoundFinished()
  {
    foreach (var player in _players)
    {
      player.Frozen = true;
    }

    // Remove all bullets
    foreach (var bullet in GetTree().GetNodesInGroup("bullets"))
    {
      bullet.QueueFree();
    }

    // Remove all capture points
    foreach (var capturePoint in GetTree().GetNodesInGroup("capture_points"))
    {
      capturePoint.QueueFree();
    }

    var results = GetResults();
    if (results.Light == results.Dark)
    {
      _score.Ties++;
      StartRound();
      return;
    }

    if (results.Light > results.Dark)
    {
      _score.Light++;
      _lastPlayerToScore = _lightPlayer;
      _lightWin.Play();
    }
    else
    {
      _score.Dark++;
      _lastPlayerToScore = _darkPlayer;
      _darkWin.Play();
    }

    _overlay.TotalScore = $"Lightness: {_score.Light}, Darkness: {_score.Dark}, Ties: {_score.Ties}";
    if (_score.Dark >= ScoreToWin || _score.Light >= ScoreToWin)
    {
      if (_score.Light > _score.Dark)
      {
        GetTree().ChangeSceneToFile("res://Scenes/EndScreenLight.tscn");
      }
      else
      {
        GetTree().ChangeSceneToFile("res://Scenes/EndScreenDarkness.tscn");
      }
    }

    StartPowerUpSelection();
  }

  private void OnShoot(Node2D bullet)
  {
    AddChild(bullet);
  }

  private void ResetLights()
  {
    var lights = GetTree().GetNodesInGroup("lights");

    foreach (var light in lights)
    {
      if (light is not Light lightNode)
      {
        throw new Exception("Light node is not a Light!!");
      }

      lightNode.SetLight(Light.LightMode.None);
    }
  }

  private void SetupCapturePoint()
  {
    const int MaxConcurrentCapturePoints = 2;
    var timer = TimerFactory.StartedTimer(TimeBetweenCapturePoint);

    AddChild(timer);

    timer.Timeout += () =>
    {
      if (GetTree().GetNodesInGroup("capture_points").Count >= MaxConcurrentCapturePoints)
      {
        return;
      }

      var res = GetResults();
      var losingPlayer = res.Light > res.Dark ? _darkPlayer : _lightPlayer;

      var capturePoint = _capturePointScene.Instantiate<CapturePoint>();
      AddChild(capturePoint);
      capturePoint.CapturedListeners += OnCapturePointCaptured;

      var offset = new Vector2(GD.RandRange(-100, 100), GD.RandRange(-100, 100));
      capturePoint.GlobalPosition = losingPlayer.GlobalPosition + offset;
    };
  }

  private void SpawnExplosion(Node2D where, Light.LightMode who, Explosion.ExplosionRadiusEnum explosionRadius)
  {
    var explosion = _explosionScene.Instantiate<Explosion>();
    explosion.LightMode = who;
    AddChild(explosion);
    explosion.SetRadius(explosionRadius);
    explosion.GlobalPosition = where.GlobalPosition;
    explosion.Explode();
    _camera.Shake(0.6f, FollowingCamera.ShakeStrength.Strong);
  }

  private void SpawnHurtIndicator(Node2D player, string msg)
  {
    var indicator = _scene.Instantiate<DamageAmountIndicator>();
    indicator.AddChild(TimerFactory.OneShotStartedTimer(6, () => indicator.QueueFree()));
    AddChild(indicator);
    indicator.GlobalPosition = player.GlobalPosition;
    indicator.Message = msg;
  }

  private void SpawnRagdoll(Player player)
  {
    var ragdoll =
      player.Team == Player.TeamEnum.Light
        ? _ragdollLightScene.Instantiate<RigidBody2D>()
        : _ragdollDarkScene.Instantiate<RigidBody2D>();
    var timer = TimerFactory.OneShotStartedTimer(5, () => ragdoll.QueueFree());
    ragdoll.AddChild(timer);

    AddChild(ragdoll);
    ragdoll.GlobalPosition = player.GlobalPosition;
    var angleVec = -Vector2.Right.Rotated((float)GD.RandRange(0, Math.PI));
    ragdoll.ApplyCentralImpulse(angleVec * (float)GD.RandRange(1000f, 1500f));
    ragdoll.AngularVelocity = GD.RandRange(-50, 50);
  }

  private void StartPowerUpSelection()
  {
    _powerUpPicker.WinningSide = _lastPlayerToScore.Team;
    _powerUpPicker.Visible = true;
    _powerUpPicker.GrabFocus();
    _powerUpPicker.Reset();
  }

  private void StartRound()
  {
    _mapManager.InitNextMap();
    ResetLights();

    _lightPlayer.GlobalPosition = _mapManager.LightSpawn.GlobalPosition;
    _darkPlayer.GlobalPosition = _mapManager.DarkSpawn.GlobalPosition;
    ForceUpdateTransform();

    foreach (var player in _players)
    {
      player.Frozen = false;
    }

    // TODO: Hack to ensure players are moved before activating the map
    AddChild(TimerFactory.OneShotSelfDestructingStartedTimer(1, () => _mapManager.StartNextMap()));
    // _mapManager.StartNextMap(); // <- Should be done similar to this
    _roundTimer.Start(RoundTime);
  }

  private void TogglePause()
  {
    var isPaused = !_pauseOverlay.Enabled;
    _pauseOverlay.Enabled = isPaused;
    GetTree().Paused = isPaused;
  }

  private void UpdateRoundTimer()
  {
    _overlay.Time = $"{_roundTimer.TimeLeft:0.0}s";
  }

  private void UpdateScore()
  {
    var results = GetResults();

    if (results is { Light: 0, Dark: 0 })
    {
      return;
    }

    _overlay.RoundScore = results;
  }

  public struct Results
  {
    public int Dark;
    public int Light;
    public int Neutral;

    public static bool operator !=(Results left, Results right)
    {
      return !(left == right);
    }

    public static bool operator ==(Results left, Results right)
    {
      return left.Equals(right);
    }

    public override bool Equals(object obj)
    {
      throw new NotImplementedException();
    }

    public override int GetHashCode()
    {
      throw new NotImplementedException();
    }
  }

  private struct Score
  {
    public int Dark;
    public int Light;
    public int Ties;
  }
}

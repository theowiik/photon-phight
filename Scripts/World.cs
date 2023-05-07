﻿using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using PhotonPhighters.Scripts.OverlayControllers;
using PhotonPhighters.Scripts.Utils;

namespace PhotonPhighters.Scripts;

public partial class World : Node2D
{
    private const int RoundTime = 100;
    private const int ScoreToWin = 4;

    [GetNode("FollowingCamera")]
    private FollowingCamera _camera;

    private Player _darkPlayer;

    [GetNode("DarkSpawn")]
    private Node2D _darkSpawn;

    [GetNode("Sfx/DarkWin")]
    private AudioStreamPlayer _darkWin;

    private Player _lastPlayerToScore;
    private Player _lightPlayer;

    [GetNode("LightSpawn")]
    private Node2D _lightSpawn;

    [GetNode("Sfx/LightWin")]
    private AudioStreamPlayer _lightWin;

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
        _powerUpPicker.Visible = false;
        _powerUpPicker.PowerUpPickedListeners += OnPowerUpSelected;
        _pauseOverlay.ResumeGame += TogglePause;

        var uiUpdateTimer = GetNode<Timer>("UIUpdateTimer");
        uiUpdateTimer.Timeout += UpdateScore;
        uiUpdateTimer.Timeout += UpdateRoundTimer;
        _roundTimer.Timeout += OnRoundFinished;

        var ob = GetNode<Area2D>("OutOfBounds");
        ob.BodyEntered += OnOutOfBounds;

        _players = GetTree().GetNodesInGroup("players").Cast<Player>();
        foreach (var player in _players)
        {
            player.PlayerDied += OnPlayerDied;
            player.Gun.ShootDelegate += OnShoot;
            _camera.AddTarget(player);
            player.PlayerEffectAddedListeners += OnPlayerEffectAdded;
        }

        _lightPlayer = _players.First(p => p.PlayerNumber == 1);
        _darkPlayer = _players.First(p => p.PlayerNumber == 2);

        if (_lightPlayer == null || _darkPlayer == null) throw new Exception("Could not find players");

        StartRound();
    }

    private void OnPlayerEffectAdded(Node2D effect, Player who)
    {
        AddChild(effect);
        effect.GlobalPosition = who.GlobalPosition;
    }

    private void OnPlayerDied(Player player)
    {
        player.GlobalPosition = player.PlayerNumber == 1 ? _lightSpawn.GlobalPosition : _darkSpawn.GlobalPosition;
        player.Freeze = true;

        var liveTimer = new Timer
        {
            OneShot = true,
            WaitTime = 2
        };
        liveTimer.Timeout += () => player.Freeze = false;
        AddChild(liveTimer);
        liveTimer.Start();
    }

    private void OnOutOfBounds(Node body)
    {
        if (body is Player player) player.TakeDamage(99999999);
    }

    private void StartRound()
    {
        ResetLights();

        foreach (var player in _players) player.Freeze = false;

        _roundTimer.Start(RoundTime);
    }

    private void OnRoundFinished()
    {
        GD.Print("Round ended");

        foreach (var player in _players) player.Freeze = true;

        // Remove all bullets
        foreach (var bullet in GetTree().GetNodesInGroup("bullets")) bullet.QueueFree();

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
            GD.Print("Game over");

            if (_score.Light > _score.Dark)
                GetTree().ChangeSceneToFile("res://Scenes/EndScreenLight.tscn");
            else
                GetTree().ChangeSceneToFile("res://Scenes/EndScreenDarkness.tscn");
        }

        StartPowerUpSelection();
    }

    private void StartPowerUpSelection()
    {
        _powerUpPicker.WinningSide = _lastPlayerToScore.Team;
        _powerUpPicker.Visible = true;
        _powerUpPicker.GrabFocus();
        _powerUpPicker.Reset();
    }

    private void OnPowerUpSelected(PowerUpManager.IPowerUpApplier powerUp)
    {
        _powerUpPicker.Visible = false;
        var loser = _lastPlayerToScore.Team == Player.TeamEnum.Light ? _darkPlayer : _lightPlayer;
        powerUp.Apply(loser);
        StartRound();
    }

    private void ResetLights()
    {
        var lights = GetTree().GetNodesInGroup("lights");

        foreach (var light in lights)
        {
            if (light is not Light lightNode) throw new Exception("Light node is not a Light!!");

            lightNode.SetLight(Light.LightMode.None);
        }
    }

    private void OnShoot(Node2D bullet)
    {
        AddChild(bullet);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
        }
    }

    private void TogglePause()
    {
        var isPaused = !_pauseOverlay.Visible;
        _pauseOverlay.Visible = isPaused;

        if (isPaused)
            _pauseOverlay.GrabFocus();
        else
            _pauseOverlay.ReleaseFocus();

        // Stop everything else
        GetTree().Paused = isPaused;
    }

    private void UpdateScore()
    {
        var results = GetResults();

        if (results.Light == 0 && results.Dark == 0) return;

        _overlay.RoundScore = results;
    }

    private void UpdateRoundTimer()
    {
        _overlay.Time = $"{Math.Round(_roundTimer.TimeLeft, 1)}s";
    }

    private Results GetResults()
    {
        var lights = GetTree().GetNodesInGroup("lights");
        var results = new Results();

        foreach (var light in lights)
        {
            if (light is not Light lightNode) throw new Exception("Light node is not a Light!!");

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

    public struct Results
    {
        public int Light;
        public int Dark;
        public int Neutral;

        public override bool Equals(object obj)
        {
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        public static bool operator ==(Results left, Results right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Results left, Results right)
        {
            return !(left == right);
        }
    }

    private struct Score
    {
        public int Light;
        public int Dark;
        public int Ties;
    }
}
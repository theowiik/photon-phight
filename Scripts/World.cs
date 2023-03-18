using Godot;
using System;
using System.Linq;

public partial class World : Node2D
{
    private Overlay _overlay;
    private Timer _roundTimer;
    private FollowingCamera _camera;
    private const int RoundTime = 60;

    public override void _Ready()
    {
        _roundTimer = GetNode<Timer>("RoundTimer");
        _overlay = GetNode<Overlay>("FollowingCamera/Overlay");
        _camera = GetNode<FollowingCamera>("FollowingCamera");
        var uiUpdateTimer = GetNode<Timer>("UIUpdateTimer");
        uiUpdateTimer.Timeout += UpdateScore;
        uiUpdateTimer.Timeout += UpdateRoundTimer;

        foreach (var player in this.GetNodes<Player>())
        {
            player.Gun.ShootDelegate += OnShoot;
            _camera.AddTarget(player);
        }

        StartRound();
    }

    private void StartRound()
    {
        _roundTimer.Start(RoundTime);
    }

    private void OnShoot(Node2D bullet)
    {
        AddChild(bullet);
    }

    private struct Results
    {
        public int On;
        public int Off;
        public int Neutral;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetTree().Quit();
        }
    }

    private void UpdateScore()
    {
        var results = GetResults();

        if (results.On == 0 && results.Off == 0)
        {
            _overlay.Score = "Go!";
            return;
        }

        var percentageOn = (float)results.On / (results.On + results.Off);
        var roundedOn = Math.Round(percentageOn * 100, 2);
        var percentageOff = (float)results.Off / (results.On + results.Off);
        var roundedOff = Math.Round(percentageOff * 100, 2);
        _overlay.Score = $"Lightness: {roundedOn}%, Darkness: {roundedOff}%";
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
            if (light is not Light lightNode)
                throw new Exception("Light node is not a Light!!");

            switch (lightNode.LightState)
            {
                case Light.LightMode.Light:
                    results.On++;
                    break;
                case Light.LightMode.Dark:
                    results.Off++;
                    break;
                case Light.LightMode.None:
                    results.Neutral++;
                    break;
            }
        }

        return results;
    }
}

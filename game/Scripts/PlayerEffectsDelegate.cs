﻿using System;
using Godot;
using GodotSharper;
using GodotSharper.AutoGetNode;
using GodotSharper.Instancing;
using PhotonPhighters.Scripts.PowerUps;
using PhotonPhighters.Scripts.Utils.ResourceWrapper;

namespace PhotonPhighters.Scripts;

public partial class PlayerEffectsDelegate : Node2D
{
  public delegate void PlayerEffectPerformed(Node2D effect);

  private const string JumpAnimation = "jump";
  private const string LandAnimation = "land";
  private const string RunAnimation = "run";

  private readonly PackedScene _curseEffectParticlesScene = ResourceLoader.Load<PackedScene>(
    ObjectResourceWrapper.CurseEffectParticlesPath
  );

  private readonly Color _hurtColor = new(0.8f, 0, 0);

  private readonly PackedScene _hurtParticlesScene = ResourceLoader.Load<PackedScene>(
    ObjectResourceWrapper.HurtParticlesPath
  );

  private readonly PackedScene _jumpParticlesScene = ResourceLoader.Load<PackedScene>(
    ObjectResourceWrapper.JumpParticlesPath
  );

  private readonly PackedScene _powerUpEffectParticlesScene = ResourceLoader.Load<PackedScene>(
    ObjectResourceWrapper.PowerUpEffectParticlesPath
  );

  [GetNode("AnimationPlayer")]
  private AnimationPlayer _animationPlayer;

  [GetUniqueNode("DeathPlayer")]
  private AudioStreamPlayer2D _deathPlayer;

  [GetUniqueNode("FallDeathPlayer")]
  private AudioStreamPlayer2D _fallDeathPlayer;

  [GetUniqueNode("Hurt2Player")]
  private AudioStreamPlayer2D _hurt2Player;

  [GetUniqueNode("HurtPlayer")]
  private AudioStreamPlayer2D _hurtPlayer;

  [GetNode("HurtTimer")]
  private Timer _hurtTimer;

  [GetUniqueNode("JumpPlayer")]
  private AudioStreamPlayer2D _jumpPlayer;

  private Color _originalModulation;

  [GetNode("PowerUpsPickedPlayer")]
  private AudioStreamPlayer2D _powerUpsPickedPlayer;

  public PlayerEffectPerformed PlayerEffectAddedListeners { get; set; }

  public Sprite2D PlayerSprite { get; set; }

  public override void _Ready()
  {
    this.GetNodes();
    _hurtTimer.Timeout += HurtTimerOnTimeout;
  }

  /// <summary>
  ///   Plays the hurt animation by modulating the player's sprite color.
  ///   If the hurt timer is active, the method exits without applying changes.
  /// </summary>
  public void AnimationPlayHurt()
  {
    if (!_hurtTimer.IsStopped())
    {
      return;
    }

    // Save the original modulation
    _originalModulation = PlayerSprite.Modulate;
    var currentColor = PlayerSprite.Modulate;

    // Blend the hurt color with the current color
    var result = new Color(
      Mathf.Clamp(currentColor.R * (1 - _hurtColor.A) + _hurtColor.R * _hurtColor.A, 0, 1),
      Mathf.Clamp(currentColor.G * (1 - _hurtColor.A) + _hurtColor.G * _hurtColor.A, 0, 1),
      Mathf.Clamp(currentColor.B * (1 - _hurtColor.A) + _hurtColor.B * _hurtColor.A, 0, 1),
      Mathf.Clamp(currentColor.A, 0, 1) // Maintain the original alpha
    );

    PlayerSprite.Modulate = result;
    _hurtTimer.Start();
  }

  public void AnimationPlayJump()
  {
    _animationPlayer.Stop();
    _animationPlayer.Play(JumpAnimation);
  }

  public void AnimationPlayLand()
  {
    _animationPlayer.Stop();
    _animationPlayer.Play(LandAnimation);
  }

  public void AnimationPlayRunLeft()
  {
    PlayerSprite.FlipH = true;
    _animationPlayer.Play(RunAnimation);
  }

  public void AnimationPlayRunRight()
  {
    PlayerSprite.FlipH = false;
    _animationPlayer.Play(RunAnimation);
  }

  public void EmitHurtParticles()
  {
    var instance = GenerateParticles(_hurtParticlesScene);
    PlayerEffectAddedListeners?.Invoke(instance);
  }

  public void EmitJumpParticles()
  {
    var instance = GenerateParticles(_jumpParticlesScene);
    PlayerEffectAddedListeners?.Invoke(instance);
  }

  public void PlayDeathSound()
  {
    _deathPlayer.Play();
  }

  private void PlayFallDeathSound()
  {
    _fallDeathPlayer.Play();
  }

  public void PlayHurtSound()
  {
    _hurtPlayer.PitchScale = (float)GD.RandRange(0.8, 1.2);
    _hurtPlayer.Play();

    if (GD.Randf() > 0.6)
    {
      _hurt2Player.Play();
    }
  }

  public void PlayJumpSound()
  {
    _jumpPlayer.Play();
  }

  private static Node2D GenerateParticles(PackedScene particlesScene)
  {
    var instance = particlesScene.Instantiate<CpuParticles2D>();
    var timer = TimerFactory.StartedSelfDestructingOneShot(instance.Lifetime, () => instance.QueueFree());

    instance.Emitting = true;
    instance.AddChild(timer);

    return instance;
  }

  /// <summary>
  ///   Revert back to the players original color.
  /// </summary>
  private void HurtTimerOnTimeout()
  {
    PlayerSprite.Modulate = _originalModulation;
  }

  public void DisplayPowerUpEffect(IPowerUpApplier powerUp)
  {
    var instance = powerUp.IsCurse
      ? GenerateParticles(_curseEffectParticlesScene)
      : GenerateParticles(_powerUpEffectParticlesScene);
    AddChild(instance);

    var label = Instanter.Instantiate<FloatingText>();
    AddChild(label);
    label.Position -= new Vector2(0, 30);
    label.SetText(powerUp.Name);

    // _powerUpsPickedPlayer.Stream = GetPowerUpNameAudioStream(powerUp); // TODO: Re-add
    _powerUpsPickedPlayer.Play();
  }

  private static AudioStream GetPowerUpNameAudioStream(IPowerUpApplier powerUp)
  {
    var fileName = StringUtil.ToSnakeCase(powerUp.GetType().Name);
    var powerUpNamePath = $"{FilesResourceWrapper.PowerUpNamesFolder}{fileName}.mp3";

    try
    {
      return GD.Load<AudioStream>(powerUpNamePath);
    }
    catch (Exception)
    {
      return null;
    }
  }
}

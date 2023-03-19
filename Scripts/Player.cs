using Godot;

public partial class Player : CharacterBody2D
{
    [Export]
    public int PlayerNumber { get; set; }
    public PlayerMovement PlayerMovementDelegate;
    private AudioStreamPlayer2D _deathPlayer;
    private bool _freeze;
    public bool Freeze
    {
        get => _freeze;
        set
        {
            _freeze = value;
            Gun.Freeze = _freeze;
            PlayerMovementDelegate.Freeze = _freeze;
        }
    }

    // Health
    public int MaxHealth { get; set; } = 100;
    private int _health;

    // Aim
    private Marker2D _gunMarker;
    public Gun Gun { get; private set; }
    private bool _aimWithMouse = true;

    // Other
    private CpuParticles2D explosionParticleEmitter;

    public override void _Ready()
    {
        _health = MaxHealth;
        _gunMarker = GetNode<Marker2D>("Marker2D");
        Gun = _gunMarker.GetNode<Gun>("Gun");
        Gun.ShootActionName = $"p{PlayerNumber}_shoot";
        Gun.LightMode = PlayerNumber == 1 ? Light.LightMode.Light : Light.LightMode.Dark;

        PlayerMovementDelegate = GetNode<PlayerMovement>("PlayerMovement");
        _deathPlayer = GetNode<AudioStreamPlayer2D>("DeathPlayer");
        PlayerMovementDelegate.PlayerNumber = PlayerNumber;
        PlayerMovementDelegate.CharacterBody = this;
        PlayerMovementDelegate.CharacterAnimation = GetNode<AnimationPlayer>("AnimationPlayer");

        var bulletDetectionArea = GetNode<Area2D>("BulletDetectionArea");
        bulletDetectionArea.AreaEntered += OnBulletEntered;

        explosionParticleEmitter = GetNode<CpuParticles2D>("Sprite2D/ExplosionParticle");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Freeze)
            return;

        Aim();
    }

    private void OnBulletEntered(Area2D area)
    {
        if (area is Bullet bullet)
        {
            TakeDamage(bullet.Damage);
            bullet.QueueFree();
        }
    }

    public void TakeDamage(int damage)
    {
        if (Freeze)
            return;

        _health -= damage;
        explosionParticleEmitter.Emitting = true;
        _deathPlayer.Play();

        if (_health <= 0)
        {
            HandleDeath();
        }
    }

    public void HandleDeath()
    {
        explosionParticleEmitter.Emitting = true;
        //Visible = false;
        //SetProcess(false);
    }

    public void ResetHealth()
    {
        _health = MaxHealth;
    }

    private void Aim()
    {
        var joystickDeadzone = 0.05f;
        var joystickVector = new Vector2(Input.GetJoyAxis(PlayerNumber - 1, JoyAxis.RightX), Input.GetJoyAxis(PlayerNumber - 1, JoyAxis.RightY));

        // Controller has priority over mouse.
        if (joystickVector.Length() > joystickDeadzone)
        {
            _gunMarker.Rotation = joystickVector.Angle();
            _aimWithMouse = false;
        }

        // Only player one can play with mouse and keyboard.
        if (PlayerNumber == 1 && _aimWithMouse)
        {
            var direction = GetGlobalMousePosition() - GlobalPosition;
            _gunMarker.Rotation = direction.Angle();
        }
    }
}

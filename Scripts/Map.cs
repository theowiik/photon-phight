﻿using Godot;
using PhotonPhighters.Scripts.Utils;

namespace PhotonPhighters.Scripts;

public partial class Map : Node2D
{
  [GetNode("OB")]
  public Area2D OutOfBounds { get; private set; }

  [GetNode("SpawnPointsContainer")]
  private Node2D SpawnPointsContainer { get; set; }

  private Node2D _lastSpawnPoint;

  public override void _Ready()
  {
    this.AutoWire();
  }

  public void SetCollisionsEnabled(bool value)
  {
    OutOfBounds.Monitoring = value;
  }

  /// <summary>
  ///   Pseudo-randomly selects a spawn point from the SpawnPointsContainer.
  ///   The same spawn point will not be returned twice in a row.
  /// </summary>
  /// <returns>
  ///  A spawn point from the SpawnPointsContainer.
  /// </returns>
  public Node2D GetRandomSpawnPoint()
  {
    Node2D nextSpawn = null;
    var spawnPoints = SpawnPointsContainer.GetNodesOfType<Node2D>();

    while (nextSpawn == null || nextSpawn == _lastSpawnPoint)
    {
      nextSpawn = spawnPoints.Sample();
    }

    _lastSpawnPoint = nextSpawn;
    return nextSpawn;
  }
}

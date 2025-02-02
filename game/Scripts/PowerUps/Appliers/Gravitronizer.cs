﻿namespace PhotonPhighters.Scripts.PowerUps.Appliers;

public class Gravitronizer : AbstractPowerUpApplier
{
  public override string Name => "Gravitronizer";
  public override Rarity Rarity => Rarity.Common;
  public override bool IsCurse => false;

  protected override void _Apply(Player playerWhoSelected, Player otherPlayer)
  {
    playerWhoSelected.Gun.BulletGravity = 0.0f;
  }
}

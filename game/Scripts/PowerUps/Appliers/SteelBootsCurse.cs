﻿namespace PhotonPhighters.Scripts.PowerUps.Appliers;

public class SteelBootsCurse : AbstractPowerUpApplier
{
  public override string Name => "Steel Boots Curse";
  public override Rarity Rarity => Rarity.Rare;
  public override bool IsCurse => true;

  protected override void _Apply(Player playerWhoSelected, Player otherPlayer)
  {
    otherPlayer.PlayerMovementDelegate.JumpForce /= 1.33f;
  }
}

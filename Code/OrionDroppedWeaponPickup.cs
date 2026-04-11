using Sandbox;

public sealed class OrionDroppedWeaponPickup : Component
{
	[Sync] public int SlotIndex { get; set; } = 1;
	[Sync] public int AmmoInMagazine { get; set; } = 0;
	[Sync] public string WeaponName { get; set; } = "Weapon";

	public bool CanBePickedUp => !PickedUp;
	[Sync] public bool PickedUp { get; set; }

	public bool TryPickup( OrionPlayerController player )
	{
		if ( !Networking.IsHost )
			return false;

		if ( PickedUp )
			return false;

		if ( !player.IsValid() || player.IsDead )
			return false;

		// For now your whole gun system is slot-based.
		// Any class can receive the gun by enabling HasGun.
		player.ReceiveDroppedGun( AmmoInMagazine );

		PickedUp = true;
		GameObject.Destroy();

		Log.Info( $"[WEAPON PICKUP] {player.GameObject.Name} picked up {WeaponName} with {AmmoInMagazine} ammo" );
		return true;
	}
}

using Sandbox;

public class OrionWeapon : Component
{
	[Property] public string WeaponName { get; set; } = "Weapon";

	// This will hold the instantiated prefab (the arms or gun)
	[Property] public GameObject ViewModel { get; set; }

	[Property] public string AttackTrigger { get; set; } = "b_attack";

	[Property] public Vector3 ViewOffset { get; set; } = new Vector3( 10, 10, -10 );

	public void SetVisible( bool visible )
	{
		if ( ViewModel.IsValid() ) ViewModel.Enabled = visible;
	}
}

using Sandbox;

public class OrionWeapon : Component
{
	[Property] public string WeaponName { get; set; } = "Weapon";
	[Property] public GameObject ViewModel { get; set; }
	[Property] public string AttackTrigger { get; set; } = "b_attack";
	[Property] public Vector3 ViewOffset { get; set; } = new Vector3( 10, 10, -10 );

	[Property] public float Damage { get; set; } = 10f;
	[Property] public float Range { get; set; } = 150f;

	// Back to GameObject; we will put a Decal Renderer on this prefab
	[Property] public GameObject ImpactDecalPrefab { get; set; }

	public void SetVisible( bool visible )
	{
		if ( ViewModel.IsValid() ) ViewModel.Enabled = visible;
	}

	public void Fire( SceneTraceResult tr, GameObject owner )
	{
		if ( !tr.Hit )
			return;

		// 1. Apply Damage
		if ( tr.GameObject.IsValid() )
		{
			var damageable = tr.GameObject.Components.Get<Component.IDamageable>( FindMode.EverythingInSelfAndAncestors );

			if ( damageable != null )
			{
				damageable.OnDamage( new DamageInfo()
				{
					Damage = Damage,
					Attacker = owner,
					Weapon = GameObject,
					Position = tr.HitPosition,
					Origin = owner.WorldPosition
				} );
			}
		}

		// 2. Spawn and Position the Decal
		if ( ImpactDecalPrefab.IsValid() && tr.GameObject.IsValid() )
		{
			var decal = ImpactDecalPrefab.Clone();

			decal.Parent = tr.GameObject;
			decal.WorldPosition = tr.HitPosition + tr.Normal * 1.5f;
			decal.WorldRotation = Rotation.LookAt( -tr.Normal );
			decal.WorldScale = Vector3.One;
		}
	}
}

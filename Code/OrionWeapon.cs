using Sandbox;

public class OrionWeapon : Component
{
	[Property] public string WeaponName { get; set; } = "Weapon";
	
	[Property] public GameObject ViewModel { get; set; }
	[Property] public string AttackTrigger { get; set; } = "b_attack";
	[Property] public Vector3 ViewOffset { get; set; } = new Vector3( 10, 10, -10 );

	[Property] public GameObject WorldModel { get; set; }
	[Property] public Vector3 WorldOffset { get; set; } = Vector3.Zero;
	[Property] public Angles WorldAngles { get; set; } = Angles.Zero;

	[Property] public float Damage { get; set; } = 10f;
	[Property] public float Range { get; set; } = 150f;

	[Property, Group( "Ammo" )] public int MagazineSize { get; set; } = 30;

	[Property, Group( "Audio" )] public SoundEvent ShootSound { get; set; }

	// Back to GameObject; we will put a Decal Renderer on this prefab
	[Property] public GameObject ImpactDecalPrefab { get; set; }

	public void SetFirstPersonVisible( bool visible )
	{
		if ( ViewModel.IsValid() )
			ViewModel.Enabled = visible;
	}

	public void SetThirdPersonVisible( bool visible )
	{
		if ( WorldModel.IsValid() )
			WorldModel.Enabled = visible;
	}

	public void SetVisible( bool visible )
	{
		SetFirstPersonVisible( visible );
		SetThirdPersonVisible( visible );
	}

	public void Fire( SceneTraceResult tr, GameObject owner )
	{
		if ( !tr.Hit )
			return;

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

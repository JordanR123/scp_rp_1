using Sandbox;

public class OrionWeapon : Component
{
	[Property] public string WeaponName { get; set; } = "Weapon";

	[Property] public GameObject ViewModel { get; set; }
	[Property] public string AttackTrigger { get; set; } = "b_attack";

	[Property, Group( "Animation" )] public string ReloadTrigger { get; set; } = "b_reload";
	[Property, Group( "Animation" )] public string EmptyBool { get; set; } = "b_empty";
	[Property, Group( "Animation" )] public string ReloadSpeedFloat { get; set; } = "speed_reload";
	[Property, Group( "Animation" )] public float ReloadAnimSpeed { get; set; } = 1.0f;

	[Property, Group( "FX" )] public GameObject MuzzleFlashPrefab { get; set; }
	[Property, Group( "FX" )] public string MuzzleAttachmentName { get; set; } = "muzzle";

	[Property] public Vector3 ViewOffset { get; set; } = new Vector3( 0, 0, 0 );

	[Property] public GameObject WorldModel { get; set; }
	[Property] public Vector3 WorldOffset { get; set; } = Vector3.Zero;
	[Property] public Angles WorldAngles { get; set; } = Angles.Zero;

	[Property] public float Damage { get; set; } = 10f;
	[Property] public float Range { get; set; } = 150f;

	[Property, Group( "Ammo" )] public int MagazineSize { get; set; } = 30;

	[Property, Group( "Audio" )] public SoundEvent ShootSound { get; set; }
	[Property, Group( "Audio" )] public SoundEvent ReloadSound { get; set; }

	[Property] public GameObject ImpactDecalPrefab { get; set; }

	private SkinnedModelRenderer GetViewModelRenderer()
	{
		if ( !ViewModel.IsValid() )
			return null;

		return ViewModel.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndChildren );
	}

	private SkinnedModelRenderer GetWorldModelRenderer()
	{
		if ( !WorldModel.IsValid() )
			return null;

		return WorldModel.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndChildren );
	}

	public void PlayReloadAnimation( bool emptyReload )
	{
		var vm = GetViewModelRenderer();
		if ( vm.IsValid() )
		{
			vm.Set( EmptyBool, emptyReload );
			vm.Set( ReloadSpeedFloat, ReloadAnimSpeed );
			vm.Set( ReloadTrigger, true );
		}

		var wm = GetWorldModelRenderer();
		if ( wm.IsValid() )
		{
			wm.Set( EmptyBool, emptyReload );
			wm.Set( ReloadSpeedFloat, ReloadAnimSpeed );
			wm.Set( ReloadTrigger, true );
		}
	}


	public void PlayReloadSound()
	{
		if ( ReloadSound == null )
			return;

		Sound.Play( ReloadSound, WorldModel?.WorldPosition ?? GameObject.WorldPosition );
	}

	public void SpawnMuzzleFlash()
	{
		if ( !WorldModel.IsValid() || !MuzzleFlashPrefab.IsValid() )
			return;

		var renderer = GetWorldModelRenderer();
		if ( !renderer.IsValid() )
			return;

		// Try attach to bone/socket
		var attach = renderer.GetAttachment( MuzzleAttachmentName );

		GameObject fx = MuzzleFlashPrefab.Clone();

		if ( attach != null )
		{
			fx.WorldPosition = attach.Value.Position;
			fx.WorldRotation = attach.Value.Rotation;
		}
		else
		{
			// fallback (VERY important or you'll think it's broken)
			fx.WorldPosition = WorldModel.WorldPosition;
			fx.WorldRotation = WorldModel.WorldRotation;
		}

	}


	public void PlayAttackAnimation()
	{
		var vm = GetViewModelRenderer();
		if ( vm.IsValid() )
		{
			vm.Set( AttackTrigger, true );
		}

		var wm = GetWorldModelRenderer();
		if ( wm.IsValid() )
		{
			wm.Set( AttackTrigger, true );
		}
	}

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

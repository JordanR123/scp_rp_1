using Sandbox;
using System;
using System.Linq;

public enum PlayerRole { DClass, Guard, Researcher }

public class PlayerData
{
	public int Level { get; set; }
	public float Experience { get; set; }
}

public partial class OrionPlayerController : Component
{
	[Property] public PlayerRole CurrentRole { get; set; } = PlayerRole.DClass;
	[Property] public int PlayerLevel { get; set; } = 1;
	[Property] public float Experience { get; set; } = 0f;
	[Property] public float Health { get; set; } = 100f;
	[Property] public int ClearanceLevel { get; set; } = 0;

	// ASSIGN THESE 3 BODY OBJECTS IN THE INSPECTOR
	// Each one should already have the correct clothing/model set up on it
	[Property] public CameraComponent PlayerCamera { get; set; }

	[Property, Group( "Death" )] public GameObject RagdollPrefab { get; set; }

	//Weapon System
	[Property] public List<OrionWeapon> Inventory { get; set; } = new();
	public OrionWeapon ActiveWeapon { get; set; }
	public int CurrentSlot { get; set; } = 0;
	public bool HasGun { get; set; } = false;

	public bool IsDead { get; set; } = false;
	public float TimeSinceDeath { get; set; } = 0f;

	private Vector3 _deathLocation;
	private Angles _deathLookAngles;

	private float _yaw;
	private float _pitch;

	protected override void OnStart()
	{
		LoadGame();
		UpdateClearance();
		UpdatePlayerVisuals();

		_yaw = GameObject.WorldRotation.Angles().yaw;
		_pitch = 0f;
	}

	public void UpdatePlayerVisuals()
	{
		// Intentionally empty for now.
		// Role-specific body visuals will be added later.
	}

	private void HandleDeathLookOnly()
	{
		var cam = Components.GetInChildren<CameraComponent>( true );
		if ( !cam.IsValid() ) return;

		Vector3 basePos = _deathLocation + Vector3.Up * 64f;

		var lookDelta = Input.AnalogLook;

		_deathLookAngles.yaw += lookDelta.yaw;
		_deathLookAngles.pitch += lookDelta.pitch;
		_deathLookAngles.pitch = _deathLookAngles.pitch.Clamp( -80f, 80f );

		cam.WorldPosition = basePos;
		cam.WorldRotation = Rotation.From( _deathLookAngles );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !GameObject.Network.IsOwner )
			return;

		if ( IsDead )
		{
			TimeSinceDeath += Time.Delta;
			HandleDeathLookOnly();
			return;
		}

		var look = Input.AnalogLook;



		Experience += Time.Delta;

		if ( PlayerLevel == 1 && Experience >= 600f )
		{
			PlayerLevel = 2;
			SaveGame();
		}
		else if ( PlayerLevel == 2 && Experience >= 1200f )
		{
			PlayerLevel = 3;
			SaveGame();
			Log.Info( "[PROGRESSION] Level 3 reached!" );
		}

		if ( Health <= 0 )
			Die();

		if ( Input.Pressed( "use" ) )
			HandleInteraction();

		HandleWeaponInputs();
	}

	public void SetupLoadoutForRole()
	{
		// Default: nobody has a gun unless their role says so
		HasGun = false;

		switch ( CurrentRole )
		{
			case PlayerRole.Guard:
				HasGun = true;
				EquipWeapon( 1 ); // Guards spawn with gun equipped
				break;

			case PlayerRole.DClass:
			case PlayerRole.Researcher:
			default:
				HasGun = false;
				EquipWeapon( 0 ); // Fists only
				break;
		}

		Log.Info( $"[LOADOUT] Role: {CurrentRole}, HasGun: {HasGun}" );
	}


	public void GiveGun()
	{
		HasGun = true;
		Log.Info( "[LOADOUT] Gun granted to player." );
	}


	private void HandleWeaponInputs()
	{
		// Slot 1 = fists, always allowed
		if ( Input.Pressed( "Slot1" ) )
			EquipWeapon( 0 );

		// Slot 2 = gun, only if player currently has a gun
		if ( Input.Pressed( "Slot2" ) && HasGun )
			EquipWeapon( 1 );

		// Mouse wheel handling
		if ( Inventory.Count > 1 )
		{
			if ( Input.MouseWheel.y > 0 )
			{
				// If player has no gun, force fists only
				if ( HasGun )
					EquipWeapon( (CurrentSlot + 1) % 2 );
				else
					EquipWeapon( 0 );
			}

			if ( Input.MouseWheel.y < 0 )
			{
				// If player has no gun, force fists only
				if ( HasGun )
					EquipWeapon( (CurrentSlot - 1 + 2) % 2 );
				else
					EquipWeapon( 0 );
			}
		}

		// Attack logic
		if ( Input.Pressed( "attack1" ) && ActiveWeapon.IsValid() && ActiveWeapon.ViewModel.IsValid() )
		{
			var renderer = ActiveWeapon.ViewModel.Components.Get<SkinnedModelRenderer>();
			if ( renderer.IsValid() )
			{
				renderer.Set( ActiveWeapon.AttackTrigger, true );
			}
		}
	}

	public void EquipWeapon( int slotIndex )
	{
		if ( slotIndex < 0 || slotIndex >= Inventory.Count ) return;

		foreach ( var weapon in Inventory )
		{
			if ( weapon.IsValid() ) weapon.SetVisible( false );
		}

		CurrentSlot = slotIndex;
		ActiveWeapon = Inventory[slotIndex];

		if ( ActiveWeapon.IsValid() )
		{
			// 1. Parent the weapon container to the camera
			ActiveWeapon.GameObject.Parent = PlayerCamera.GameObject;
			ActiveWeapon.SetVisible( true );

			// 2. Reset the container's local transform
			ActiveWeapon.LocalPosition = Vector3.Zero;
			ActiveWeapon.LocalRotation = Rotation.Identity;

			// 3. FORCE the actual model mesh to be in front of the camera
			if ( ActiveWeapon.ViewModel.IsValid() )
			{
				ActiveWeapon.ViewModel.LocalPosition = ActiveWeapon.ViewOffset;
				ActiveWeapon.ViewModel.LocalRotation = Rotation.Identity;
			}

			Log.Info( $"[EQUIP] {ActiveWeapon.WeaponName} model forced into view." );
		}
	}



	public void Die()
	{
		if ( IsDead ) return;

		IsDead = true;
		TimeSinceDeath = 0f;
		_deathLocation = Transform.World.Position;
		_deathLookAngles = Transform.World.Rotation.Angles();

		if ( RagdollPrefab.IsValid() )
		{
			var ragdoll = RagdollPrefab.Clone( _deathLocation );
			ragdoll.NetworkSpawn();
		}
	}

	public void Respawn()
	{
		var manager = Scene.GetAllComponents<OrionGameManager>().FirstOrDefault();

		if ( manager.IsValid() )
		{
			Log.Info( "[RESPAWN] Resetting existing player instance." );
			manager.ResetPlayer( this );
		}
		else
		{
			Game.ActiveScene.Load( Game.ActiveScene.Source );
		}
	}

	private void HandleInteraction()
	{
		// Use the direct reference instead of searching
		if ( !PlayerCamera.IsValid() )
		{
			Log.Warning( "[INTERACT] PlayerCamera property is not assigned!" );
			return;
		}

		var tr = Scene.Trace.Ray( PlayerCamera.WorldPosition, PlayerCamera.WorldPosition + PlayerCamera.WorldRotation.Forward * 150f )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( tr.Hit )
		{
			Log.Info( $"[INTERACT] Hit: {tr.GameObject.Name}" );

			if ( tr.GameObject.Components.Get<OrionDoor>( FindMode.EverythingInSelfAndAncestors ) is { } door )
			{
				door.OnUse( GameObject );
			}
		}
	}


	public void UpdateClearance()
	{
		ClearanceLevel = CurrentRole switch
		{
			PlayerRole.Guard => 1,
			PlayerRole.Researcher => 2,
			_ => 0
		};
	}

	public void SaveGame()
	{
		var data = new PlayerData
		{
			Level = PlayerLevel,
			Experience = Experience
		};

		FileSystem.Data.WriteJson( "player_stats.json", data );
		Log.Info( $"[SAVE SYSTEM] Progress saved - Level: {PlayerLevel}, Experience: {Experience:F1}" );
	}

	public void LoadGame()
	{
		if ( FileSystem.Data.FileExists( "player_stats.json" ) )
		{
			var data = FileSystem.Data.ReadJson<PlayerData>( "player_stats.json" );

			if ( data != null )
			{
				PlayerLevel = data.Level;
				Experience = data.Experience;
				Log.Info( $"[SAVE SYSTEM] Loaded Level: {PlayerLevel} & Loaded Experience: {Experience:F1}" );
			}
			else
			{
				Log.Warning( "[SAVE SYSTEM] Save file found but could not be read. Resetting stats." );
				PlayerLevel = 1;
				Experience = 0f;
			}
		}
		else
		{
			Log.Info( "[SAVE SYSTEM] No save file found. Initializing new player data (Level 1)." );
			PlayerLevel = 1;
			Experience = 0f;
		}
	}

	protected override void OnDisabled()
	{
		SaveGame();
	}


}

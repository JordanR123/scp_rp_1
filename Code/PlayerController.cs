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
	[Property] public GameObject DClassBody { get; set; }
	[Property] public GameObject GuardBody { get; set; }
	[Property] public GameObject ResearcherBody { get; set; }
	[Property] public CameraComponent PlayerCamera { get; set; }
	[Property] public GameObject FirstPersonArms { get; set; }

	[Property, Group( "Death" )] public GameObject RagdollPrefab { get; set; }

	//Weapon System
	[Property] public List<OrionWeapon> Inventory { get; set; } = new();
		public OrionWeapon ActiveWeapon { get; set; }
	public int CurrentSlot { get; set; } = 0;

	public bool IsDead { get; set; } = false;
	public float TimeSinceDeath { get; set; } = 0f;

	private Vector3 _deathLocation;
	private Angles _deathLookAngles;

	protected override void OnStart()
	{
		LoadGame();
		UpdateClearance();
		UpdatePlayerVisuals();
	}

	public void UpdatePlayerVisuals()
	{
		if ( DClassBody.IsValid() ) DClassBody.Enabled = false;
		if ( GuardBody.IsValid() ) GuardBody.Enabled = false;
		if ( ResearcherBody.IsValid() ) ResearcherBody.Enabled = false;

		switch ( CurrentRole )
		{
			case PlayerRole.DClass:
				if ( DClassBody.IsValid() ) DClassBody.Enabled = true;
				break;
			case PlayerRole.Guard:
				if ( GuardBody.IsValid() ) GuardBody.Enabled = true;
				break;
			case PlayerRole.Researcher:
				if ( ResearcherBody.IsValid() ) ResearcherBody.Enabled = true;
				break;
		}
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
		if ( IsDead )
		{
			TimeSinceDeath += Time.Delta;
			HandleDeathLookOnly();
			return;
		}

		Experience += Time.Delta;

		if ( PlayerLevel < 2 && Experience >= 600f )
		{
			PlayerLevel = 2;
			SaveGame();
			Log.Info( "[PROGRESSION] Level 2 reached!" );
		}

		if ( Health <= 0 ) Die();

		if ( Input.Pressed( "use" ) ) HandleInteraction();

		// Handle Weapon Logic
		HandleWeaponInputs();

	}


	private void HandleWeaponInputs()
{
    // Slot Switching (Keys 1 and 2)
    if ( Input.Pressed( "Slot1" ) ) EquipWeapon( 0 );
    if ( Input.Pressed( "Slot2" ) ) EquipWeapon( 1 );

    // Scroll Switching
    if ( Input.MouseWheel.y > 0 ) EquipWeapon( (CurrentSlot + 1) % Inventory.Count );
    if ( Input.MouseWheel.y < 0 ) EquipWeapon( (CurrentSlot - 1 + Inventory.Count) % Inventory.Count );

    // Attack Logic for Facepunch Prefabs
    if ( Input.Pressed( "attack1" ) && ActiveWeapon.IsValid() )
    {
        var renderer = ActiveWeapon.ViewModel.Components.Get<SkinnedModelRenderer>();
        if ( renderer.IsValid() )
        {
            // Triggers the animation (like b_attack) on the Facepunch prefab
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

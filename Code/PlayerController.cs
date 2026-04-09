using Sandbox;
using System;
using System.Linq;

public enum PlayerRole { DClass, Guard, Researcher }

public class PlayerData
{
	public int Level { get; set; }
	public float Experience { get; set; }
}

public partial class OrionPlayerController : Component, Component.IDamageable
{
	[Property] public PlayerRole CurrentRole { get; set; } = PlayerRole.DClass;
	[Property] public int PlayerLevel { get; set; } = 1;
	[Property] public float Experience { get; set; } = 0f;
	[Property] public float MaxHealth { get; set; } = 100f;


	[Sync( Flags = SyncFlags.FromHost )] public float Health { get; set; } = 100f;
	[Sync( Flags = SyncFlags.FromHost )] public bool IsDead { get; set; } = false;


	[Sync] public bool ShowXpPopup { get; set; }
	[Sync] public bool HasChosenRole { get; set; } = false;
	[Sync] public string XpPopupMessage { get; set; } = "";
	[Property] public int ClearanceLevel { get; set; } = 0;

	// ASSIGN THESE 3 BODY OBJECTS IN THE INSPECTOR
	// Each one should already have the correct clothing/model set up on it
	[Property] public CameraComponent PlayerCamera { get; set; }

	[Property, Group( "Death" )] public GameObject RagdollPrefab { get; set; }


	private string SaveFileName
	{
		get
		{
			var ownerId = GameObject.Network.OwnerId;
			return $"player_stats_{ownerId}.json";
		}
	}

	//Weapon System
	[Property] public List<OrionWeapon> Inventory { get; set; } = new();
	public OrionWeapon ActiveWeapon { get; set; }
	public int CurrentSlot { get; set; } = 0;
	public bool HasGun { get; set; } = false;

	// XP System
	private const float ResearcherXpGiftAmount = 100f;
	private static readonly TimeSpan ResearcherXpCooldown = TimeSpan.FromMinutes( 5 );
	private static readonly TimeSpan XpPopupDuration = TimeSpan.FromSeconds( 3 );

	private DateTime _nextResearcherGiveXpUtc = DateTime.MinValue;
	private DateTime _nextResearcherReceiveXpUtc = DateTime.MinValue;
	private DateTime _xpPopupUntilUtc = DateTime.MinValue;

	private bool CanReceiveResearcherXp =>
	CurrentRole == PlayerRole.Guard || CurrentRole == PlayerRole.DClass;

	private bool IsResearcherGiveReady =>
		DateTime.UtcNow >= _nextResearcherGiveXpUtc;

	private bool IsResearcherReceiveReady =>
		DateTime.UtcNow >= _nextResearcherReceiveXpUtc;

	private void StartResearcherGiveCooldown()
	{
		_nextResearcherGiveXpUtc = DateTime.UtcNow + ResearcherXpCooldown;
	}

	private void StartResearcherReceiveCooldown()
	{
		_nextResearcherReceiveXpUtc = DateTime.UtcNow + ResearcherXpCooldown;
	}

	private void ShowTimedXpPopup( string message )
	{
		XpPopupMessage = message;
		ShowXpPopup = true;
		_xpPopupUntilUtc = DateTime.UtcNow + XpPopupDuration;
	}

	private void UpdateXpPopup()
	{
		if ( ShowXpPopup && DateTime.UtcNow >= _xpPopupUntilUtc )
		{
			ShowXpPopup = false;
			XpPopupMessage = "";
		}
	}

	public bool TryGiveResearcherXpTo( OrionPlayerController target )
	{
		if ( target == null || !target.IsValid() )
			return false;

		if ( target == this )
			return false;

		if ( CurrentRole != PlayerRole.Researcher )
			return false;

		if ( !target.CanReceiveResearcherXp )
			return false;

		if ( !IsResearcherGiveReady )
		{
			ShowTimedXpPopup( "XP GIFT ON COOLDOWN" );
			return false;
		}

		if ( !target.IsResearcherReceiveReady )
		{
			ShowTimedXpPopup( $"{target.CurrentRole.ToString().ToUpper()} ALREADY RECEIVED XP" );
			return false;
		}

		target.Experience += ResearcherXpGiftAmount;

		StartResearcherGiveCooldown();
		target.StartResearcherReceiveCooldown();

		ShowTimedXpPopup( $"YOU GAVE {target.CurrentRole.ToString().ToUpper()} 100 XP" );
		target.ShowTimedXpPopup( $"RESEARCHER GAVE YOU 100 XP" );

		target.SaveGame();
		SaveGame();

		Log.Info( $"[XP GIFT] {GameObject.Name} gave {ResearcherXpGiftAmount} XP to {target.GameObject.Name}" );
		return true;
	}

	public float TimeSinceDeath { get; set; } = 0f;
	private Vector3 _deathLocation;
	private Angles _deathLookAngles;
	private GameObject _spawnedRagdoll;

	private float _yaw;
	private float _pitch;

	protected override void OnStart()
	{
		LoadGame();
		UpdateClearance();
		UpdatePlayerVisuals();

		_yaw = GameObject.WorldRotation.Angles().yaw;
		_pitch = 0f;

		RefreshLocalOwnershipState();

		if ( GameObject.Network.IsOwner && !HasChosenRole )
		{
			ShowRoleSelect();
		}
	}


	public void ChooseRole( PlayerRole role )
	{
		if ( !GameObject.Network.IsOwner )
			return;

		var manager = Scene.GetAllComponents<OrionGameManager>().FirstOrDefault();
		if ( !manager.IsValid() )
		{
			Log.Warning( "[ROLE] Game manager not found." );
			return;
		}

		manager.SpawnPlayer( this, role );
		HideRoleSelect();
		RefreshLocalOwnershipState();

		Log.Info( $"[ROLE] {GameObject.Name} chose {role}" );
	}



	private void RefreshLocalOwnershipState()
	{
		bool isOwner = GameObject.Network.IsOwner && !IsProxy;

		if ( PlayerCamera.IsValid() )
		{
			PlayerCamera.Enabled = isOwner;
		}

		Log.Info( $"[OWNER STATE] {GameObject.Name} | IsOwner={GameObject.Network.IsOwner} | IsProxy={IsProxy} | CameraEnabled={isOwner}" );
	}

	private GameObject _roleUiInstance;

	private void ShowRoleSelect()
	{
		if ( !GameObject.Network.IsOwner )
			return;

		if ( _roleUiInstance.IsValid() )
			return;

		var manager = Scene.GetAllComponents<OrionGameManager>().FirstOrDefault();

		if ( !manager.IsValid() || !manager.RoleSelectPrefab.IsValid() )
		{
			Log.Warning( "[ROLE UI] Missing RoleSelectPrefab!" );
			return;
		}

		_roleUiInstance = manager.RoleSelectPrefab.Clone();
		// DO NOT NetworkSpawn UI. This must stay local-only.

		Log.Info( "[ROLE UI] Local role selection shown." );
	}

	public void HideRoleSelect()
	{
		if ( _roleUiInstance.IsValid() )
		{
			_roleUiInstance.Destroy();
			_roleUiInstance = null;
		}
	}


	public void UpdatePlayerVisuals()
	{
		// Intentionally empty for now.
		// Role-specific body visuals will be added later.
	}

	public void OnDamage( in DamageInfo damage )
	{
		Log.Info(
			$"[ONDAMAGE ENTER] Target={GameObject.Name} | OwnerId={GameObject.Network.OwnerId} | " +
			$"IncomingDamage={damage.Damage} | Attacker={damage.Attacker?.Name} | " +
			$"HealthBefore={Health} | IsDead={IsDead}"
		);

		if ( IsDead )
		{
			Log.Warning( $"[ONDAMAGE IGNORED] Target={GameObject.Name} already dead." );
			return;
		}

		if ( damage.Damage <= 0f )
		{
			Log.Warning( $"[ONDAMAGE IGNORED] Target={GameObject.Name} damage <= 0." );
			return;
		}

		Health -= damage.Damage;

		Log.Info(
			$"[ONDAMAGE APPLIED] Target={GameObject.Name} | Damage={damage.Damage} | HealthAfter={Health}"
		);

		if ( Health <= 0f )
		{
			Health = 0f;

			Log.Warning(
				$"[ONDAMAGE KILL THRESHOLD] Target={GameObject.Name} reached 0 health. Calling OnKilled()."
			);

			OnKilled( damage );
			return;
		}

		SyncHealthState( Health, false );
	}


	public bool CanTakeDamage()
	{
		return !IsDead && Health > 0f;
	}

	public void Heal( float amount )
	{
		if ( IsDead )
			return;

		Health = MathF.Min( Health + amount, MaxHealth );
	}

	private void HandleDeathLookOnly()
	{
		if ( !PlayerCamera.IsValid() )
			return;

		// Keep the body locked where it died
		GameObject.WorldPosition = _deathLocation;
		GameObject.WorldRotation = Rotation.FromYaw( _deathLookAngles.yaw );

		var lookDelta = Input.AnalogLook;

		_deathLookAngles.yaw += lookDelta.yaw;
		_deathLookAngles.pitch += lookDelta.pitch;
		_deathLookAngles.pitch = _deathLookAngles.pitch.Clamp( -80f, 80f );

		PlayerCamera.WorldPosition = _deathLocation + Vector3.Up * 64f;
		PlayerCamera.WorldRotation = Rotation.From( _deathLookAngles );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !GameObject.Network.IsOwner )
			return;

		UpdateXpPopup();

		if ( IsDead )
		{
			TimeSinceDeath += Time.Delta;
			HandleDeathLookOnly();
			return;
		}

		var look = Input.AnalogLook;
		_yaw += look.yaw;
		_pitch += look.pitch;
		_pitch = _pitch.Clamp( -80f, 80f );

		UpdateLivingCamera();

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


		if ( Input.Pressed( "use" ) )
			HandleInteraction();

		if ( Input.Pressed( "GiveXP" ) )
			TryGiveXpFromLook();

		HandleWeaponInputs();
		
	}


	private void UpdateLivingCamera()
	{
		if ( !PlayerCamera.IsValid() )
			return;

		PlayerCamera.WorldPosition = GameObject.WorldPosition + Vector3.Up * 64f;
		PlayerCamera.WorldRotation = Rotation.From( new Angles( _pitch, _yaw, 0f ) );
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

		var slot0Name = Inventory.Count > 0 && Inventory[0].IsValid() ? Inventory[0].WeaponName : "NULL";
		var slot1Name = Inventory.Count > 1 && Inventory[1].IsValid() ? Inventory[1].WeaponName : "NULL";

		Log.Info(
			$"[LOADOUT] Player={GameObject.Name} | Role={CurrentRole} | HasGun={HasGun} | " +
			$"InventoryCount={Inventory.Count} | Slot0={slot0Name} | Slot1={slot1Name}"
		);
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

		if ( Input.Pressed( "attack1" ) && ActiveWeapon.IsValid() && PlayerCamera.IsValid() )
		{
			Log.Info(
				$"[CLIENT ATTACK INPUT] Player={GameObject.Name} | OwnerId={GameObject.Network.OwnerId} | " +
				$"Weapon={ActiveWeapon.WeaponName} | Slot={CurrentSlot} | HasGun={HasGun} | " +
				$"Origin={PlayerCamera.WorldPosition} | Forward={PlayerCamera.WorldRotation.Forward}"
			);

			PlayAttackEffects();

			RequestFireOnHost(
				PlayerCamera.WorldPosition,
				PlayerCamera.WorldRotation.Forward,
				CurrentSlot
			);
		}
	}

	[Rpc.Broadcast]
	private void PlayAttackEffects()
	{
		if ( !ActiveWeapon.IsValid() || !ActiveWeapon.ViewModel.IsValid() )
			return;

		var renderer = ActiveWeapon.ViewModel.Components.Get<SkinnedModelRenderer>();
		if ( renderer.IsValid() )
		{
			renderer.Set( ActiveWeapon.AttackTrigger, true );
		}
	}

	[Rpc.Host]
	private void RequestFireOnHost( Vector3 origin, Vector3 direction, int slotIndex )
	{
		Log.Info(
			$"[HOST FIRE RPC RECEIVED] Shooter={GameObject.Name} | OwnerId={GameObject.Network.OwnerId} | " +
			$"Slot={slotIndex} | Origin={origin} | Direction={direction}"
		);

		if ( IsDead )
		{
			Log.Warning( $"[HOST FIRE BLOCKED] Shooter={GameObject.Name} is dead." );
			return;
		}

		if ( slotIndex < 0 || slotIndex >= Inventory.Count )
		{
			Log.Warning(
				$"[HOST FIRE BLOCKED] Shooter={GameObject.Name} sent invalid slot {slotIndex}. InventoryCount={Inventory.Count}"
			);
			return;
		}

		var weapon = Inventory[slotIndex];
		if ( !weapon.IsValid() )
		{
			Log.Warning(
				$"[HOST FIRE BLOCKED] Shooter={GameObject.Name} has invalid weapon in slot {slotIndex}."
			);
			return;
		}

		Log.Info(
			$"[HOST FIRE WEAPON CHECK] Shooter={GameObject.Name} | Weapon={weapon.WeaponName} | " +
			$"Damage={weapon.Damage} | Range={weapon.Range} | HasGun={HasGun}"
		);

		// If slot 1 is gun, make sure the player really has it.
		if ( slotIndex == 1 && !HasGun )
		{
			Log.Warning(
				$"[HOST FIRE BLOCKED] Shooter={GameObject.Name} tried to use gun slot without HasGun=true."
			);
			return;
		}

		var ray = new Ray( origin, direction );

		var tr = Scene.Trace.Ray( ray, weapon.Range )
			.IgnoreGameObjectHierarchy( GameObject )
			.UsePhysicsWorld()
			.Run();

		Log.Info(
			$"[HOST TRACE RESULT] Shooter={GameObject.Name} | Weapon={weapon.WeaponName} | " +
			$"Hit={tr.Hit} | HitObject={tr.GameObject?.Name} | HitPos={tr.HitPosition} | Normal={tr.Normal}"
		);

		if ( !tr.Hit || !tr.GameObject.IsValid() )
		{
			Log.Warning(
				$"[HOST TRACE MISS] Shooter={GameObject.Name} | Weapon={weapon.WeaponName} | No valid hit object."
			);
			return;
		}

		var targetPlayer = tr.GameObject.Components.Get<OrionPlayerController>( FindMode.EverythingInSelfAndAncestors );
		if ( targetPlayer.IsValid() )
		{
			Log.Info(
				$"[HOST TARGET PLAYER FOUND] Shooter={GameObject.Name} -> Target={targetPlayer.GameObject.Name} | " +
				$"TargetHealthBefore={targetPlayer.Health} | TargetIsDead={targetPlayer.IsDead}"
			);
		}
		else
		{
			Log.Warning(
				$"[HOST TARGET PLAYER NOT FOUND] HitObject={tr.GameObject.Name} has no OrionPlayerController in self/ancestors."
			);
		}

		var damageable = tr.GameObject.Components.Get<Component.IDamageable>( FindMode.EverythingInSelfAndAncestors );

		if ( damageable != null )
		{
			Log.Info(
				$"[HOST APPLY DAMAGE] Shooter={GameObject.Name} -> HitObject={tr.GameObject.Name} | Damage={weapon.Damage}"
			);

			damageable.OnDamage( new DamageInfo()
			{
				Damage = weapon.Damage,
				Attacker = GameObject,
				Weapon = weapon.GameObject,
				Position = tr.HitPosition,
				Origin = origin
			} );

			if ( targetPlayer.IsValid() )
			{
				Log.Info(
					$"[HOST DAMAGE APPLIED] Shooter={GameObject.Name} -> Target={targetPlayer.GameObject.Name} | " +
					$"TargetHealthAfter={targetPlayer.Health} | TargetIsDead={targetPlayer.IsDead}"
				);
			}
		}
		else
		{
			Log.Warning(
				$"[HOST DAMAGEABLE NOT FOUND] HitObject={tr.GameObject.Name} does not expose IDamageable in self/ancestors."
			);
		}

		Log.Info(
			$"[HOST IMPACT FX REQUEST] Shooter={GameObject.Name} | Weapon={weapon.WeaponName} | " +
			$"HitObject={tr.GameObject?.Name} | HitPos={tr.HitPosition} | HitNormal={tr.Normal}"
		);

		SpawnImpactEffects( tr.HitPosition, tr.Normal, slotIndex );
	}

	[Rpc.Broadcast]
	private void SyncHealthState( float health, bool isDead )
	{
		Log.Info(
			$"[SYNC HEALTH STATE] Player={GameObject.Name} | IncomingHealth={health} | IncomingIsDead={isDead} | " +
			$"LocalBeforeHealth={Health} | LocalBeforeIsDead={IsDead}"
		);

		Health = health;
		IsDead = isDead;

		Log.Info(
			$"[SYNC HEALTH STATE APPLIED] Player={GameObject.Name} | Health={Health} | IsDead={IsDead}"
		);
	}

	[Rpc.Broadcast]
	private void SpawnImpactEffects( Vector3 hitPosition, Vector3 hitNormal, int slotIndex )
	{
		Log.Info(
			$"[IMPACT FX] Player={GameObject.Name} | Slot={slotIndex} | HitPos={hitPosition} | HitNormal={hitNormal}"
		);

		if ( slotIndex < 0 || slotIndex >= Inventory.Count )
		{
			Log.Warning( $"[IMPACT FX BLOCKED] Invalid slot {slotIndex}. InventoryCount={Inventory.Count}" );
			return;
		}

		var weapon = Inventory[slotIndex];
		if ( !weapon.IsValid() )
		{
			Log.Warning( $"[IMPACT FX BLOCKED] Weapon in slot {slotIndex} is invalid." );
			return;
		}

		if ( !weapon.ImpactDecalPrefab.IsValid() )
		{
			Log.Warning( $"[IMPACT FX BLOCKED] Weapon={weapon.WeaponName} has no ImpactDecalPrefab assigned." );
			return;
		}

		var decal = weapon.ImpactDecalPrefab.Clone();
		if ( !decal.IsValid() )
		{
			Log.Warning( $"[IMPACT FX BLOCKED] Failed to clone decal prefab for {weapon.WeaponName}." );
			return;
		}

		decal.WorldPosition = hitPosition + hitNormal * 1.5f;
		decal.WorldRotation = Rotation.LookAt( -hitNormal );
		decal.WorldScale = Vector3.One;

		Log.Info(
			$"[IMPACT FX SPAWNED] Weapon={weapon.WeaponName} | Decal={decal.Name} | Position={decal.WorldPosition}"
		);
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

	public void OnKilled( in DamageInfo damage )
	{
		Log.Info(
			$"[ONKILLED ENTER] Target={GameObject.Name} | Attacker={damage.Attacker?.Name} | " +
			$"Health={Health} | IsDead={IsDead}"
		);

		Log.Info( $"[ONKILLED EXECUTE] {GameObject.Name} killed by {damage.Attacker?.Name}" );

		Die();
	}

	public bool IsLocalDead =>
	IsDead && GameObject.Network.IsOwner;

	public void ForceSyncHealthState()
	{
		Log.Info(
			$"[FORCE SYNC HEALTH STATE] Player={GameObject.Name} | Health={Health} | IsDead={IsDead} | Position={GameObject.WorldPosition}"
		);

		SyncHealthState( Health, IsDead );
	}




	public void Die()
	{
		Log.Info(
			$"[DIE ENTER] Player={GameObject.Name} | Health={Health} | IsDead={IsDead} | OwnerId={GameObject.Network.OwnerId}"
		);

		if ( IsDead )
		{
			Log.Warning( $"[DIE IGNORED] Player={GameObject.Name} already dead." );
			return;
		}

		IsDead = true;
		SyncHealthState( Health, true );
		TimeSinceDeath = 0f;

		_deathLocation = GameObject.WorldPosition;

		_deathLookAngles = PlayerCamera.IsValid()
			? PlayerCamera.WorldRotation.Angles()
			: GameObject.WorldRotation.Angles();

		foreach ( var weapon in Inventory )
		{
			if ( weapon.IsValid() )
				weapon.SetVisible( false );
		}

		ActiveWeapon = null;

		if ( RagdollPrefab.IsValid() )
		{
			Log.Info( $"[DIE RAGDOLL] Spawning ragdoll for {GameObject.Name} at {_deathLocation}" );

			_spawnedRagdoll = RagdollPrefab.Clone( _deathLocation );
			_spawnedRagdoll.WorldRotation = GameObject.WorldRotation;
			_spawnedRagdoll.NetworkSpawn();
		}
		else
		{
			Log.Warning( $"[DIE RAGDOLL MISSING] No RagdollPrefab assigned for {GameObject.Name}" );
		}

		Log.Info(
			$"[DIE COMPLETE] Player={GameObject.Name} | Health={Health} | IsDead={IsDead}"
		);
	}

	public void DestroySpawnedRagdoll()
	{
		if ( _spawnedRagdoll.IsValid() )
		{
			Log.Info( $"[RAGDOLL CLEANUP] Destroying ragdoll for {GameObject.Name}" );
			_spawnedRagdoll.Destroy();
			_spawnedRagdoll = null;
		}
	}

	public void Respawn()
	{
		if ( !GameObject.Network.IsOwner )
			return;

		Log.Info( $"[RESPAWN REQUEST] Player={GameObject.Name} requested respawn." );
		RequestRespawnOnHost();
	}

	[Rpc.Host]
	private void RequestRespawnOnHost()
	{
		Log.Info( $"[RESPAWN HOST] Processing respawn for {GameObject.Name}" );

		var manager = Scene.GetAllComponents<OrionGameManager>().FirstOrDefault();

		if ( manager.IsValid() )
		{
			manager.ResetPlayer( this );
		}
		else
		{
			Log.Warning( "[RESPAWN HOST] No OrionGameManager found." );
		}
	}

	private void TryGiveXpFromLook()
	{
		Log.Info( "[GIVE XP] B pressed" );

		if ( CurrentRole != PlayerRole.Researcher )
		{
			Log.Info( $"[GIVE XP] Not a researcher (Role: {CurrentRole})" );
			return;
		}

		if ( !PlayerCamera.IsValid() )
		{
			Log.Warning( "[GIVE XP] PlayerCamera not assigned!" );
			return;
		}

		var tr = Scene.Trace.Ray(
				PlayerCamera.WorldPosition,
				PlayerCamera.WorldPosition + PlayerCamera.WorldRotation.Forward * 150f )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		// ❌ Case 1: hit nothing
		if ( !tr.Hit )
		{
			Log.Info( "[GIVE XP] Hit nothing" );
			return;
		}

		// Always log what we hit
		Log.Info( $"[GIVE XP] Hit object: {tr.GameObject.Name}" );

		var targetPlayer = tr.GameObject.Components.Get<OrionPlayerController>( FindMode.EverythingInSelfAndAncestors );

		// ❌ Case 2: hit something but not a player
		if ( !targetPlayer.IsValid() )
		{
			Log.Info( "[GIVE XP] Hit object is NOT a player" );
			return;
		}

		// ✅ Case 3: hit a player
		Log.Info( $"[GIVE XP] Hit PLAYER: {targetPlayer.GameObject.Name} (Role: {targetPlayer.CurrentRole})" );

		// Optional: check if valid target role
		if ( !targetPlayer.CanReceiveResearcherXp )
		{
			Log.Info( "[GIVE XP] Player cannot receive XP (wrong role)" );
			return;
		}

		// Attempt XP transfer
		var success = TryGiveResearcherXpTo( targetPlayer );

		Log.Info( success
			? "[GIVE XP] XP transfer SUCCESS"
			: "[GIVE XP] XP transfer FAILED (cooldown or rules)" );
	}

	private void HandleInteraction()
	{
		if ( !PlayerCamera.IsValid() )
		{
			Log.Warning( "[INTERACT] PlayerCamera property is not assigned!" );
			return;
		}

		var tr = Scene.Trace.Ray(
				PlayerCamera.WorldPosition,
				PlayerCamera.WorldPosition + PlayerCamera.WorldRotation.Forward * 150f )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !tr.Hit )
			return;

		Log.Info( $"[INTERACT] Hit: {tr.GameObject.Name}" );

		// Existing door interaction
		if ( tr.GameObject.Components.Get<OrionDoor>( FindMode.EverythingInSelfAndAncestors ) is { } door )
		{
			door.OnUse( GameObject );
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

		FileSystem.Data.WriteJson( SaveFileName, data );

		Log.Info( $"[SAVE SYSTEM] Progress saved to {SaveFileName} - Level: {PlayerLevel}, Experience: {Experience:F1}" );
	}

	public void LoadGame()
	{
		if ( FileSystem.Data.FileExists( SaveFileName ) )
		{
			var data = FileSystem.Data.ReadJson<PlayerData>( SaveFileName );

			if ( data != null )
			{
				PlayerLevel = data.Level;
				Experience = data.Experience;
				Log.Info( $"[SAVE SYSTEM] Loaded from {SaveFileName} - Level: {PlayerLevel}, Experience: {Experience:F1}" );
			}
			else
			{
				Log.Warning( $"[SAVE SYSTEM] Save file {SaveFileName} found but could not be read. Resetting stats." );
				PlayerLevel = 1;
				Experience = 0f;
			}
		}
		else
		{
			Log.Info( $"[SAVE SYSTEM] No save file found for {SaveFileName}. Initializing new player data." );
			PlayerLevel = 1;
			Experience = 0f;
		}
	}

	protected override void OnDisabled()
	{
		SaveGame();
	}


}

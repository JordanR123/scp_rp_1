using Sandbox;
using System;
using System.Linq;
using Sandbox.Citizen;
using System.Collections.Generic;


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
	[Sync] public Angles NetworkLookAngles { get; set; }
	[Sync] public Vector3 NetworkEyePosition { get; set; }
	[Property] public int ClearanceLevel { get; set; } = 0;
	public TimeSince TimeSinceLastDamage { get; set; } = 100f;
	public TimeSince TimeSinceLastConfirmedHit { get; set; } = 100f;
	[Property, Group( "Weapon" )] public SoundEvent HitSound { get; set; }
	[Property, Group( "Visuals" )] public SkinnedModelRenderer BodyRenderer { get; set; }

	[Sync] public bool IsInvincible { get; set; }

	[Property, Group( "Visuals" )] public Clothing DClassClothing { get; set; }
	[Property, Group( "Visuals" )] public Clothing GuardClothing { get; set; }
	[Property, Group( "Visuals" )] public Clothing ResearcherClothing { get; set; }

	[Property, Group( "Visuals" )] public CitizenAnimationHelper BodyAnimator { get; set; }
	[Property, Group( "Visuals" )] public GameObject RightHandAnchor { get; set; }


	[Property, Group( "UI" )] public OrionHUDState HudState { get; set; }
	[Property, Group( "UI" )] public OrionChatManager ChatManager { get; set; }
	[Property, Group( "Voice" )] public Voice VoiceChat { get; set; }

	public bool IsVoiceKeyHeld { get; set; }

	private TimeUntil _invincibleTimer;

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


	// Weapon System
	[Property] public List<OrionWeapon> Inventory { get; set; } = new();
	public OrionWeapon ActiveWeapon { get; set; }
	[Sync] public int CurrentSlot { get; set; } = 0;
	[Sync] public bool HasGun { get; set; } = false;

	[Sync( Flags = SyncFlags.FromHost )] public int AmmoInMagazine { get; set; } = 30;
	[Sync( Flags = SyncFlags.FromHost )] public bool IsReloading { get; set; } = false;

	[Property, Group( "Weapon" )] public float ReloadTime { get; set; } = 1.8f;

	private TimeUntil _reloadTimer;


	private OrionWeapon GetWeaponInSlot( int slot )
	{
		if ( slot < 0 || slot >= Inventory.Count )
			return null;

		var weapon = Inventory[slot];
		return weapon.IsValid() ? weapon : null;
	}

	private OrionWeapon GetGunWeapon()
	{
		return GetWeaponInSlot( 1 );
	}

	private void FillMagazineFromWeapon()
	{
		var gun = GetGunWeapon();
		if ( gun.IsValid() )
		{
			AmmoInMagazine = gun.MagazineSize;
		}
	}

	private void StartReloadHost( int slotIndex )
	{
		if ( !Networking.IsHost )
			return;

		if ( IsDead || IsReloading )
			return;

		if ( slotIndex != 1 )
			return;

		if ( !HasGun )
			return;

		var gun = GetWeaponInSlot( slotIndex );
		if ( !gun.IsValid() )
			return;

		if ( AmmoInMagazine >= gun.MagazineSize )
			return;

		IsReloading = true;
		_reloadTimer = ReloadTime;

		Log.Info( $"[RELOAD START HOST] Player={GameObject.Name} | Ammo={AmmoInMagazine}/{gun.MagazineSize}" );
	}

	private void UpdateReload()
	{
		if ( !Networking.IsHost )
			return;

		if ( !IsReloading )
			return;

		if ( _reloadTimer > 0f )
			return;

		IsReloading = false;
		FillMagazineFromWeapon();

		Log.Info( $"[RELOAD COMPLETE HOST] Player={GameObject.Name} | Ammo={AmmoInMagazine}" );
	}

	[Rpc.Host]
	private void RequestReloadOnHost( int slotIndex )
	{
		StartReloadHost( slotIndex );
	}

	public void StartInvincibility( float duration )
	{
		if ( IsProxy )
			return;

		IsInvincible = true;
		_invincibleTimer = duration;

		Log.Info( $"[INVINCIBILITY] {GameObject.Name} for {duration}s" );
	}


	//Chat and Voice

	public bool IsTypingChat
	{
		get
		{
			var hud = ResolveHudState();
			return hud.IsValid() && hud.ShowChat;
		}
	}

	public void OpenChat()
	{
		if ( !GameObject.Network.IsOwner || IsProxy )
			return;

		var hud = ResolveHudState();
		if ( !hud.IsValid() )
		{
			Log.Warning( "[CHAT] No OrionHUDState found." );
			return;
		}

		if ( IsDead )
			return;

		if ( hud.ShowRoleSelect )
			return;

		if ( hud.ShowChat )
			return;

		hud.OpenChat();
		Log.Info( "[CHAT] Opened chat." );
	}

	public void CloseChat()
	{
		if ( !GameObject.Network.IsOwner || IsProxy )
			return;

		var hud = ResolveHudState();
		if ( !hud.IsValid() )
			return;

		if ( !hud.ShowChat )
			return;

		hud.CloseChat();
		Log.Info( "[CHAT] Closed chat." );
	}

	public void SubmitChatMessage()
	{
		if ( !GameObject.Network.IsOwner || IsProxy )
			return;

		var hud = ResolveHudState();
		var chat = ResolveChatManager();

		if ( !hud.IsValid() || !chat.IsValid() )
		{
			Log.Warning( "[CHAT] Missing OrionHUDState or OrionChatManager." );
			return;
		}

		var text = hud.ChatDraft?.Trim() ?? "";

		if ( string.IsNullOrWhiteSpace( text ) )
		{
			hud.CloseChat();
			return;
		}

		chat.SendChatToHost( text );
		hud.CloseChat();

		Log.Info( $"[CHAT] Submitted: {text}" );
	}

	private OrionHUDState ResolveHudState()
	{
		if ( HudState.IsValid() )
			return HudState;

		return Scene.GetAllComponents<OrionHUDState>()
			.FirstOrDefault( x => x.IsValid() );
	}

	private OrionChatManager ResolveChatManager()
	{
		if ( ChatManager.IsValid() )
			return ChatManager;

		return Scene.GetAllComponents<OrionChatManager>()
			.FirstOrDefault( x => x.IsValid() );
	}




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

		NetworkLookAngles = new Angles( _pitch, _yaw, 0f );
		NetworkEyePosition = GameObject.WorldPosition + Vector3.Up * 64f;

		RefreshLocalOwnershipState();


		if ( !VoiceChat.IsValid() )
		{
			VoiceChat = Components.Get<Voice>( FindMode.EverythingInSelfAndChildren );
		}

		if ( !VoiceChat.IsValid() )
		{
			Log.Warning( $"[VOICE] No Voice component found on {GameObject.Name}. Voice chat will not work." );
		}
		else
		{
			VoiceChat.PushToTalkInput = "voice";
			VoiceChat.WorldspacePlayback = true;

			Log.Info(
				$"[VOICE] Ready on {GameObject.Name} | " +
				$"PushToTalkInput={VoiceChat.PushToTalkInput} | " +
				$"IsListening={VoiceChat.IsListening} | " +
				$"IsRecording={VoiceChat.IsRecording}"
			);
		}

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

		UpdateWeaponVisibility();
		UpdateThirdPersonBodyPose();

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
		if ( !BodyRenderer.IsValid() )
		{
			Log.Warning( "[VISUALS] BodyRenderer not assigned." );
			return;
		}

		var container = new ClothingContainer();

		var selectedClothing = CurrentRole switch
		{
			PlayerRole.Guard => GuardClothing,
			PlayerRole.Researcher => ResearcherClothing,
			_ => DClassClothing
		};

		if ( selectedClothing is null )
		{
			Log.Warning( $"[VISUALS] No clothing assigned for role {CurrentRole}" );
			return;
		}

		container.Clothing.Add( new ClothingContainer.ClothingEntry
		{
			Clothing = selectedClothing
		} );

		container.Apply( BodyRenderer );
		Log.Info( $"[VISUALS] Applied {selectedClothing.Title} for role {CurrentRole}" );
	}

	private void UpdateThirdPersonBodyPose()
	{
		if ( !BodyAnimator.IsValid() || !BodyRenderer.IsValid() )
			return;

		BodyAnimator.Target = BodyRenderer;

		if ( PlayerCamera.IsValid() )
			BodyAnimator.EyeSource = PlayerCamera.GameObject;

		BodyAnimator.AimAngle = NetworkLookAngles;
		BodyAnimator.IsGrounded = true;

		if ( IsDead )
		{
			BodyAnimator.HoldType = CitizenAnimationHelper.HoldTypes.None;
			return;
		}

		if ( CurrentSlot == 0 )
		{
			// Fists
			BodyAnimator.HoldType = CitizenAnimationHelper.HoldTypes.Punch;
		}
		else if ( CurrentSlot == 1 && HasGun )
		{
			// Use Pistol for a handgun, Rifle if it's a long gun.
			BodyAnimator.HoldType = CitizenAnimationHelper.HoldTypes.Pistol;
			BodyAnimator.Handedness = CitizenAnimationHelper.Hand.Right;
		}
		else
		{
			BodyAnimator.HoldType = CitizenAnimationHelper.HoldTypes.None;
		}
	}



	public void OnDamage( in DamageInfo damage )
	{
		Log.Info(
			$"[ONDAMAGE ENTER] Target={GameObject.Name} | OwnerId={GameObject.Network.OwnerId} | " +
			$"IncomingDamage={damage.Damage} | Attacker={damage.Attacker?.Name} | " +
			$"HealthBefore={Health} | IsDead={IsDead}"
		);

		if ( IsInvincible )
		{
			Log.Info( $"[DAMAGE BLOCKED - INVINCIBLE] {GameObject.Name}" );
			return;
		}


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
		NotifyTookDamage();

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

		// Host-authoritative timers/simulation.
		// This MUST run even when the host does not own this pawn.
		if ( Networking.IsHost )
		{
			UpdateReload();

			if ( IsInvincible && _invincibleTimer <= 0f )
			{
				IsInvincible = false;
				Log.Info( $"[INVINCIBILITY END] {GameObject.Name}" );
			}
		}

		UpdateThirdPersonBodyPose();

		if ( IsProxy || !GameObject.Network.IsOwner )
			return;


		if ( IsTypingChat )
		{
			if ( Input.Keyboard.Pressed( "ESCAPE" ) )
			{
				CloseChat();
				return;
			}

			// Let the UI text entry handle Enter.
			return;
		}





		bool isHoldingVoice =
			Input.Down( "voice" ) ||
			Input.Keyboard.Down( "V" );

		bool pressedOpenChat =
			Input.Pressed( "chat" ) ||
			Input.Keyboard.Pressed( "T" ) ||
			Input.Keyboard.Pressed( "ENTER" );

		var hud = ResolveHudState();
		if ( hud.IsValid() )
		{
			hud.IsVoiceKeyHeld = isHoldingVoice;
		}





		if ( !IsTypingChat && pressedOpenChat )
		{
			OpenChat();
			return;
		}


		UpdateXpPopup();

		if ( IsDead )
		{
			TimeSinceDeath += Time.Delta;
			HandleDeathLookOnly();

			NetworkLookAngles = _deathLookAngles;
			NetworkEyePosition = _deathLocation + Vector3.Up * 64f;
			return;
		}

		var look = Input.AnalogLook;
		_yaw += look.yaw;
		_pitch += look.pitch;
		_pitch = _pitch.Clamp( -80f, 80f );

		UpdateLivingCamera();

		NetworkLookAngles = new Angles( _pitch, _yaw, 0f );
		NetworkEyePosition = GameObject.WorldPosition + Vector3.Up * 64f;

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

		UpdateReload();
		HandleWeaponInputs();
		

	}

	private TimeUntil _respawnFireLock;

	public void ResetLookAfterRespawn()
	{
		var angles = GameObject.WorldRotation.Angles();

		_yaw = angles.yaw;
		_pitch = 0f;

		_deathLookAngles = new Angles( _pitch, _yaw, 0f );
		NetworkLookAngles = _deathLookAngles;
		NetworkEyePosition = GameObject.WorldPosition + Vector3.Up * 64f;

		UpdateLivingCamera();
		_respawnFireLock = 0.05f;
	}


	private void UpdateLivingCamera()
	{
		if ( !PlayerCamera.IsValid() ) return;

		PlayerCamera.WorldPosition = GameObject.WorldPosition + Vector3.Up * 64f;
		// Correct way to combine angles for a stable forward vector:
		PlayerCamera.WorldRotation = Rotation.FromYaw( _yaw ) * Rotation.FromPitch( _pitch );
	}



	public void SetupLoadoutForRole()
	{
		// Default: nobody has a gun unless their role says so
		HasGun = false;

		switch ( CurrentRole )
		{
			case PlayerRole.Guard:
				HasGun = true;
				FillMagazineFromWeapon();
				EquipWeapon( 1 ); // Guards spawn with gun equipped
				break;

			case PlayerRole.DClass:
				HasGun = true;
				FillMagazineFromWeapon();
				EquipWeapon( 1 ); // Give gun for testing
				break;
			case PlayerRole.Researcher:
			default:
				HasGun = false;
				AmmoInMagazine = 0;
				EquipWeapon( 0 ); // Fists only
				break;
		}

		var slot0Name = Inventory.Count > 0 && Inventory[0].IsValid() ? Inventory[0].WeaponName : "NULL";
		var slot1Name = Inventory.Count > 1 && Inventory[1].IsValid() ? Inventory[1].WeaponName : "NULL";

		UpdateWeaponVisibility();
		UpdateThirdPersonBodyPose();


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

	public bool IsGunEquipped =>
	HasGun && CurrentSlot == 1 && ActiveWeapon.IsValid();

	public int CurrentAmmo =>
		IsGunEquipped ? AmmoInMagazine : 0;

	public int CurrentMagazineSize
	{
		get
		{
			if ( Inventory.Count <= 1 || !Inventory[1].IsValid() )
				return 0;

			return Inventory[1].MagazineSize;
		}
	}

	private void HandleWeaponInputs()
	{
		if ( _respawnFireLock > 0f )
			return;

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
				if ( HasGun )
					EquipWeapon( (CurrentSlot + 1) % 2 );
				else
					EquipWeapon( 0 );
			}

			if ( Input.MouseWheel.y < 0 )
			{
				if ( HasGun )
					EquipWeapon( (CurrentSlot - 1 + 2) % 2 );
				else
					EquipWeapon( 0 );
			}
		}



		if ( Input.Pressed( "Reload" ) )
		{
			RequestReloadOnHost( CurrentSlot );

		}

		if ( Input.Pressed( "attack1" ) && ActiveWeapon.IsValid() && PlayerCamera.IsValid() )
		{
			// No firing while reloading. Revolutionary technology.
			if ( CurrentSlot == 1 )
			{
				if ( IsReloading )
					return;

				if ( !HasGun )
					return;

				if ( AmmoInMagazine <= 0 )
				{
					RequestReloadOnHost( CurrentSlot );
					return;
				}
			}

			Log.Info(
				$"[CLIENT ATTACK INPUT] Player={GameObject.Name} | OwnerId={GameObject.Network.OwnerId} | " +
				$"Weapon={ActiveWeapon.WeaponName} | Slot={CurrentSlot} | HasGun={HasGun} | " +
				$"Ammo={AmmoInMagazine} | Origin={PlayerCamera.WorldPosition} | Forward={PlayerCamera.WorldRotation.Forward}"
			);

			

			UpdateLivingCamera();

			var fireOrigin = PlayerCamera.WorldPosition;
			var fireDirection = PlayerCamera.WorldRotation.Forward;

			var screenCenterRay = PlayerCamera.ScreenNormalToRay( 0.5f ); // The mathematical center of the screen
			Log.Info( $"[DEBUG] Screen Center Forward: {screenCenterRay.Forward}" );
			Log.Info( $"[DEBUG] Camera Component Forward: {PlayerCamera.WorldRotation.Forward}" );
			Log.Info( $"[DEBUG] Calculated Pitch/Yaw Forward: {(Rotation.FromYaw( _yaw ) * Rotation.FromPitch( _pitch )).Forward}" );

			RequestFireOnHost(
				fireOrigin,
				fireDirection,
				CurrentSlot
			);
		}
	}

	[Rpc.Broadcast]
	private void PlayAttackEffects( int slotIndex, Vector3 soundPosition )
	{
		if ( slotIndex < 0 || slotIndex >= Inventory.Count )
			return;

		var weapon = Inventory[slotIndex];
		if ( !weapon.IsValid() )
			return;

		// Only animate the local first-person viewmodel for the owning player
		bool isLocalOwner = GameObject.Network.IsOwner && !IsProxy;

		if ( isLocalOwner && weapon.ViewModel.IsValid() )
		{
			var renderer = weapon.ViewModel.Components.Get<SkinnedModelRenderer>();
			if ( renderer.IsValid() )
			{
				renderer.Set( weapon.AttackTrigger, true );
			}
		}

		if ( weapon.ShootSound is not null )
		{
			Log.Info( $"[GUN SOUND] Playing {weapon.ShootSound.ResourceName} at {soundPosition}" );
			Sound.Play( weapon.ShootSound, soundPosition );
		}
		else
		{
			Log.Warning( $"[GUN SOUND] No ShootSound assigned on weapon {weapon.WeaponName}" );
		}
	}

	[Rpc.Host]
	private void RequestReloadOnHost()
	{
		StartReloadHost( CurrentSlot );
	}

	[Rpc.Host]
	private void RequestFireOnHost( Vector3 origin, Vector3 direction, int slotIndex )
	{


		Log.Info(
			$"[HOST FIRE RPC RECEIVED] Shooter={GameObject.Name} | OwnerId={GameObject.Network.OwnerId} | " +
			$"Slot={slotIndex} | Origin={origin} | Direction={direction}"
		);


		if ( slotIndex == 1 )
		{
			if ( IsReloading )
			{
				Log.Warning( $"[HOST FIRE BLOCKED] Shooter={GameObject.Name} is reloading." );
				return;
			}

			if ( !HasGun )
			{
				Log.Warning( $"[HOST FIRE BLOCKED] Shooter={GameObject.Name} has no gun." );
				return;
			}

			var gun = GetWeaponInSlot( slotIndex );
			if ( !gun.IsValid() )
				return;

			if ( AmmoInMagazine <= 0 )
			{
				Log.Warning( $"[HOST FIRE BLOCKED] Shooter={GameObject.Name} has empty magazine." );
				StartReloadHost( slotIndex );
				return;
			}

			AmmoInMagazine--;
			Log.Info( $"[HOST AMMO] Shooter={GameObject.Name} | AmmoInMagazine={AmmoInMagazine}" );

			if ( AmmoInMagazine <= 0 )
			{
				StartReloadHost( slotIndex );
			}
		}

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

		PlayAttackEffects( slotIndex, GameObject.WorldPosition );

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
				NotifyHitMarker();

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

		SpawnImpactEffects( tr.GameObject, tr.HitPosition, tr.Normal, slotIndex );
	}


	[Rpc.Owner]
	public void NotifyHitMarker()
	{
		TimeSinceLastConfirmedHit = 0;

		if ( HitSound is not null )
		{
			GameObject.PlaySound( HitSound );
		}
	}

	[Rpc.Owner]
	public void NotifyTookDamage()
	{
		TimeSinceLastDamage = 0;
	}


	[Rpc.Broadcast]
	private void SpawnImpactEffects( GameObject hitObject, Vector3 hitPosition, Vector3 hitNormal, int slotIndex )
	{
		Log.Info(
			$"[IMPACT FX] Player={GameObject.Name} | Slot={slotIndex} | HitPos={hitPosition} | HitNormal={hitNormal}"
		);

		if ( slotIndex < 0 || slotIndex >= Inventory.Count )
			return;

		var weapon = Inventory[slotIndex];
		if ( !weapon.IsValid() || !weapon.ImpactDecalPrefab.IsValid() )
			return;

		var decal = weapon.ImpactDecalPrefab.Clone();
		if ( !decal.IsValid() )
			return;

		if ( hitObject.IsValid() )
		{
			decal.Parent = hitObject;
		}

		decal.WorldPosition = hitPosition + hitNormal * 1.5f;
		decal.WorldRotation = Rotation.LookAt( -hitNormal );
		decal.WorldScale = Vector3.One;
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


	private GameObject _viewModelInstance;

	public void EquipWeapon( int slotIndex )
	{
		if ( slotIndex < 0 || slotIndex >= Inventory.Count )
			return;

		CurrentSlot = slotIndex;
		ActiveWeapon = Inventory[slotIndex];

		UpdateWeaponVisibility();
	}


	private void UpdateWeaponVisibility()
	{
		bool isLocalFirstPerson = GameObject.Network.IsOwner && !IsProxy && !IsDead;

		for ( int i = 0; i < Inventory.Count; i++ )
		{
			var weapon = Inventory[i];
			if ( !weapon.IsValid() )
				continue;

			bool isActive = i == CurrentSlot;

			weapon.SetFirstPersonVisible( false );
			weapon.SetThirdPersonVisible( false );

			// Local first-person active weapon
			if ( isLocalFirstPerson && isActive && PlayerCamera.IsValid() )
			{
				weapon.GameObject.Parent = PlayerCamera.GameObject;
				weapon.LocalPosition = Vector3.Zero;
				weapon.LocalRotation = Rotation.Identity;

				weapon.SetFirstPersonVisible( true );

				if ( weapon.ViewModel.IsValid() )
				{
					weapon.ViewModel.LocalPosition = weapon.ViewOffset;
					weapon.ViewModel.LocalRotation = Rotation.Identity;
				}
			}
			// Third-person active weapon for other players
			else if ( isActive && RightHandAnchor.IsValid() && i == 1 )
			{
				weapon.GameObject.Parent = RightHandAnchor;
				weapon.LocalPosition = Vector3.Zero;
				weapon.LocalRotation = Rotation.Identity;

				weapon.SetThirdPersonVisible( true );

				if ( weapon.WorldModel.IsValid() )
				{
					weapon.WorldModel.LocalPosition = weapon.WorldOffset;
					weapon.WorldModel.LocalRotation = Rotation.From( weapon.WorldAngles );
				}
			}


		}
	}

	private void DestroyViewModel()
	{
		if ( _viewModelInstance.IsValid() )
		{
			_viewModelInstance.Destroy();
			_viewModelInstance = null;
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

	[Rpc.Broadcast]
	public void SyncHealthState( float newHealth, bool newIsDead )
	{
		Health = newHealth;
		IsDead = newIsDead;

		Log.Info( $"[SYNC] {GameObject.Name} Health: {Health}, Dead: {IsDead}" );
	}


	public void ForceSyncHealthState()
	{
		Log.Info( $"[FORCE SYNC] {GameObject.Name} | Health={Health} | IsDead={IsDead}" );
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
		IsReloading = false;
		_reloadTimer = 0f;

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

using Sandbox;
using System.Linq;

public class OrionGameManager : Component
{
	[Property] public GameObject DBlockSpawn { get; set; }
	[Property] public GameObject GuardSpawn { get; set; }
	[Property] public GameObject SurfaceSpawn { get; set; }
	[Property] public GameObject RoleSelectPrefab { get; set; }
	[Property] public GameObject HudObject { get; set; }
	[Property] public GameObject SpawnRoomLocation { get; set; }
	[Property] public OrionChatManager ChatManager { get; set; }

	protected override void OnStart()
	{
	}


	public void SpawnPlayer( OrionPlayerController player, PlayerRole role )
	{
		if ( !player.IsValid() )
		{
			Log.Error( "SpawnPlayer: player was invalid." );
			return;
		}

		player.CurrentRole = role;
		player.HasChosenRole = true;
		player.UpdateClearance();
		player.UpdatePlayerVisuals();
		player.SetupLoadoutForRole();

		Transform target = role switch
		{
			PlayerRole.DClass => DBlockSpawn.Transform.World,
			PlayerRole.Guard => GuardSpawn.Transform.World,
			PlayerRole.Researcher => SurfaceSpawn.Transform.World,
			_ => DBlockSpawn.Transform.World
		};

		player.Transform.World = target;
		player.Network.ClearInterpolation();

		if ( player.PlayerCamera.IsValid() )
		{
			player.PlayerCamera.Enabled = player.GameObject.Network.IsOwner;
			player.PlayerCamera.WorldPosition = player.GameObject.WorldPosition + Vector3.Up * 64f;
			player.PlayerCamera.WorldRotation = player.GameObject.WorldRotation;
		}

		Log.Info( $"[SPAWN] {player.GameObject.Name} assigned to {role} and moved to sector." );
	}


	public void ResetPlayer( OrionPlayerController player )
	{
		if ( !player.IsValid() ) return;

		Log.Info( $"[RESET] Relocating player: {player.GameObject.Name}" );

		player.DestroySpawnedRagdoll();

		// 1. Restore Stats
		player.Health = player.MaxHealth;
		player.IsDead = false;
		player.TimeSinceDeath = 0f;
		player.IsReloading = false;
		player.GameObject.Enabled = true;

		// Reward 100 Experience on death
		player.Experience += 100f;
		Log.Info( $"[REWARD] 100 Experience awarded. Total Experience: {player.Experience:F1}" );

		player.SetupLoadoutForRole();
		player.EquipWeapon( player.HasGun ? 1 : 0 );


		// 2. Move to Spawn Point first
		Transform target = player.CurrentRole switch
		{
			PlayerRole.DClass => DBlockSpawn.Transform.World,
			PlayerRole.Guard => GuardSpawn.Transform.World,
			PlayerRole.Researcher => SurfaceSpawn.Transform.World,
			_ => DBlockSpawn.Transform.World
		};

		player.Transform.World = target;
		player.Network.ClearInterpolation();

		// 3. Reset look state so camera + shooting direction match the new spawn
		player.ResetLookAfterRespawn();

		// 4. Re-enable/update camera AFTER teleport + look reset
		var cam = player.PlayerCamera;
		if ( cam.IsValid() )
		{
			cam.Enabled = player.GameObject.Network.IsOwner;
			cam.WorldPosition = player.GameObject.WorldPosition + Vector3.Up * 64f;
			cam.WorldRotation = Rotation.From( player.NetworkLookAngles );
		}

		player.IsInvincible = true;
		player.StartInvincibility( 1.0f ); // 1 second

		player.ForceSyncHealthState();
		player.UpdatePlayerVisuals();
		player.SaveGame();
		Log.Info( "[RESET] Player teleported, revived, and progress saved." );
	}


}

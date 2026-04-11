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


	private Transform GetSpawnTransformForRole( PlayerRole role )
	{
		GameObject spawnObject = role switch
		{
			PlayerRole.DClass => DBlockSpawn,
			PlayerRole.Guard => GuardSpawn,
			PlayerRole.Researcher => SurfaceSpawn,
			_ => null
		};

		Log.Info(
			$"[SPAWN LOOKUP] Role={role} | " +
			$"DBlockSpawn={(DBlockSpawn.IsValid() ? DBlockSpawn.Name : "NULL")} | " +
			$"GuardSpawn={(GuardSpawn.IsValid() ? GuardSpawn.Name : "NULL")} | " +
			$"SurfaceSpawn={(SurfaceSpawn.IsValid() ? SurfaceSpawn.Name : "NULL")} | " +
			$"SpawnRoom={(SpawnRoomLocation.IsValid() ? SpawnRoomLocation.Name : "NULL")}"
		);

		if ( !spawnObject.IsValid() )
		{
			Log.Warning( $"[SPAWN LOOKUP] Role spawn invalid for {role}, falling back to SpawnRoomLocation" );
			spawnObject = SpawnRoomLocation;
		}

		if ( !spawnObject.IsValid() )
		{
			Log.Warning( "[SPAWN LOOKUP] SpawnRoomLocation not assigned. Falling back to GameManager object." );
			spawnObject = GameObject;
		}

		Log.Info( $"[SPAWN LOOKUP] Role={role} -> Using {spawnObject.Name} at {spawnObject.WorldPosition}" );

		return spawnObject.Transform.World;
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

		Transform target = GetSpawnTransformForRole( role );

		player.Transform.World = target;
		player.Network.ClearInterpolation();

		player.ApplySpawnOnOwner( target.Position, target.Rotation );

		Log.Info( $"[SPAWN] {player.GameObject.Name} assigned to {role} and moved to {target.Position}" );
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
		Transform target = GetSpawnTransformForRole( player.CurrentRole );

		player.Transform.World = target;
		player.Network.ClearInterpolation();

		player.ApplySpawnOnOwner( target.Position, target.Rotation );


		player.IsInvincible = true;
		player.StartInvincibility( 1.0f ); // 1 second

		player.ForceSyncHealthState();
		player.UpdatePlayerVisuals();
		player.SaveGame();
		Log.Info( $"[RESET] Player teleported, revived, and progress saved. Role={player.CurrentRole} Spawn={target.Position}" );
	}


}

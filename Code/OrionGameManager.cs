using Sandbox;
using System.Linq;

public class OrionGameManager : Component
{
	[Property] public GameObject DBlockSpawn { get; set; }
	[Property] public GameObject GuardSpawn { get; set; }
	[Property] public GameObject SurfaceSpawn { get; set; }
	[Property] public GameObject RoleSelectPrefab { get; set; }
	[Property] public GameObject HudObject { get; set; }

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

		// 1. Restore Stats
		player.Health = player.MaxHealth;
		player.IsDead = false;
		player.TimeSinceDeath = 0f;
		player.ForceSyncHealthState();

		// Reward 100 Experience on death
		player.Experience += 100f;
		Log.Info( $"[REWARD] 100 Experience awarded. Total Experience: {player.Experience:F1}" );

		player.SetupLoadoutForRole();
		player.EquipWeapon( player.HasGun ? 1 : 0 );


		// 2. Re-enable the camera and force it back to standard view
		var cam = player.PlayerCamera;
		if ( cam.IsValid() )
		{
			cam.Enabled = player.GameObject.Network.IsOwner;
			cam.WorldPosition = player.GameObject.WorldPosition + Vector3.Up * 64f;
			cam.WorldRotation = player.GameObject.WorldRotation;

			Log.Info( $"[RESET] Camera restored. Enabled={cam.Enabled}" );
		}

		// 3. Move to Spawn Point
		Transform target = player.CurrentRole switch
		{
			PlayerRole.DClass => DBlockSpawn.Transform.World,
			PlayerRole.Guard => GuardSpawn.Transform.World,
			PlayerRole.Researcher => SurfaceSpawn.Transform.World,
			_ => DBlockSpawn.Transform.World
		};

		player.Transform.World = target;
		Log.Info( "[RESET] Player teleported and revived." );

		// ADD THIS: Trigger save on respawn
		player.UpdatePlayerVisuals();
		player.SaveGame();
		Log.Info( "[RESET] Player teleported, revived, and progress saved." );
	}
}

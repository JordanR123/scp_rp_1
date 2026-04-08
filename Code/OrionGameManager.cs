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
		// Ensure the Role Selection UI is created if it doesn't exist
		if ( RoleSelectPrefab.IsValid() )
		{
			var menu = RoleSelectPrefab.Clone();
			menu.NetworkSpawn();
		}
	}


	public void SpawnPlayer( PlayerRole role )
	{
		// Find the local player controller in the scene
		var player = Game.ActiveScene.GetAllComponents<OrionPlayerController>().FirstOrDefault();

		if ( !player.IsValid() )
		{
			Log.Error( "SpawnPlayer: Could not find OrionPlayerController!" );
			return;
		}

		// Assign the chosen role and update clearance levels
		player.CurrentRole = role;
		player.UpdateClearance();
		player.UpdatePlayerVisuals();

		// Initialize inventory: Start with Slot 0 (Fists) for everyone
		// This prevents the 'Update' exception by ensuring ActiveWeapon isn't null
		player.EquipWeapon( 0 );

		// Determine the target transform based on the role
		Transform target = role switch
		{
			PlayerRole.DClass => DBlockSpawn.Transform.World,
			PlayerRole.Guard => GuardSpawn.Transform.World,
			PlayerRole.Researcher => SurfaceSpawn.Transform.World,
			_ => DBlockSpawn.Transform.World
		};

		// Teleport the player to the role-specific spawn point
		player.Transform.World = target;

		Log.Info( $"[SPAWN] Player assigned to {role} and moved to sector." );
	}

	public void ResetPlayer( OrionPlayerController player )
	{
		if ( !player.IsValid() ) return;

		Log.Info( $"[RESET] Relocating player: {player.GameObject.Name}" );

		// 1. Restore Stats
		player.Health = 100f;
		player.IsDead = false;
		player.TimeSinceDeath = 0f;

		// Reward 100 Experience on death
		player.Experience += 100f;
		Log.Info( $"[REWARD] 100 Experience awarded. Total Experience: {player.Experience:F1}" );

		player.EquipWeapon( 0 );

		// 2. Re-enable the camera and force it back to standard view
		var cam = player.Components.GetInChildren<CameraComponent>( true );
		if ( cam.IsValid() )
		{
			cam.Enabled = true;

			// This uses the explicit constructor to avoid naming conflicts with 'Transform'
			cam.LocalPosition = Vector3.Zero;
			cam.LocalRotation = Rotation.Identity;

			Log.Info( "[RESET] Camera local transform zeroed out." );
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

using Sandbox;
using Sandbox.Network;
using System.Linq;

public sealed class OrionNetworkManager : Component, Component.INetworkListener
{
	[Property] public GameObject PlayerPrefab { get; set; }
	[Property] public GameObject[] SpawnPoints { get; set; }

	protected override void OnStart()
	{
		// Dedicated server should not create a lobby here.
		if ( Networking.IsActive )
			return;

		// Only do this for listen-host / local testing if you still need it.
		Networking.CreateLobby( new LobbyConfig
		{
			MaxPlayers = 16,
			Privacy = LobbyPrivacy.Public,
			Name = "Orion Networks SCP RP"
		} );
	}

	public void OnActive( Connection connection )
	{
		if ( !PlayerPrefab.IsValid() )
		{
			Log.Error( "OrionNetworkManager: PlayerPrefab is not assigned." );
			return;
		}

		// Find the GameManager to get the Spawn Room location
		var manager = Scene.GetAllComponents<OrionGameManager>().FirstOrDefault();
		var spawnTransform = Transform.World;

		// Use the SpawnRoomLocation if it exists, otherwise fallback to the first SpawnPoint
		if ( manager.IsValid() && manager.SpawnRoomLocation.IsValid() )
		{
			spawnTransform = manager.SpawnRoomLocation.Transform.World;
		}
		else if ( SpawnPoints is { Length: > 0 } && SpawnPoints[0].IsValid() )
		{
			spawnTransform = SpawnPoints[0].Transform.World;
		}

		var player = PlayerPrefab.Clone();
		player.Transform.World = spawnTransform;
		player.NetworkSpawn( connection );

		var controller = player.Components.Get<OrionPlayerController>( FindMode.EverythingInSelfAndChildren );
		if ( !controller.IsValid() )
		{
			Log.Error( $"[NET] Spawned player object for {connection.DisplayName}, but OrionPlayerController was missing." );
			return;
		}

		// Use SteamId for persistence across reconnects.
		controller.PersistentPlayerId = connection.SteamId.ToString();
		controller.NetworkPlayerName = string.IsNullOrWhiteSpace( connection.DisplayName )
			? $"Player {connection.Id}"
			: connection.DisplayName;

		controller.LoadGame();

		Log.Info( $"[NET] Spawned player {connection.DisplayName} | SteamId={connection.SteamId} | SaveKey={controller.PersistentPlayerId}" );
		Log.Info( $"[NET] Spawned player in Spawn Room at {spawnTransform.Position}" );
	}
}

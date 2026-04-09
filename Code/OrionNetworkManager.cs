using Sandbox;
using Sandbox.Network;

public sealed class OrionNetworkManager : Component, Component.INetworkListener
{
	[Property] public GameObject PlayerPrefab { get; set; }
	[Property] public GameObject[] SpawnPoints { get; set; }

	protected override void OnStart()
	{
		// Only create a lobby if we're not already in one
		if ( Networking.IsActive )
			return;

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

		var spawnTransform = Transform.World;

		if ( SpawnPoints is { Length: > 0 } && SpawnPoints[0].IsValid() )
			spawnTransform = SpawnPoints[0].Transform.World;

		// Inside OrionNetworkManager.cs -> OnActive
		var player = PlayerPrefab.Clone();
		player.Transform.World = spawnTransform;
		player.NetworkSpawn( connection );

		Log.Info( $"[NET] Spawned player for connection {connection.Id} at {spawnTransform.Position}" );
	}
}

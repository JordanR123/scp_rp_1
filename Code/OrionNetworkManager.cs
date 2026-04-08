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
			MaxPlayers = 8,
			Privacy = LobbyPrivacy.Public,
			Name = "Orion"
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

		var player = PlayerPrefab.Clone( spawnTransform );
		player.NetworkSpawn( connection );
	}
}

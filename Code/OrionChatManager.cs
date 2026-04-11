using Sandbox;
using System;

public sealed class OrionChatManager : Component
{
	public static event Action<string, string> OnChatMessageReceived;

	[Rpc.Host]
	public void SendChatToHost( string message )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return;

		message = message.Trim();

		if ( message.Length > 180 )
			message = message[..180];

		var caller = Rpc.Caller;
		var playerName = caller.DisplayName ?? $"Player {caller.Id}";

		string rolePrefix = "";

		foreach ( var player in Scene.GetAllComponents<OrionPlayerController>() )
		{
			if ( !player.IsValid() )
				continue;

			if( player.Network.Owner == caller )
			{
				rolePrefix = $"[{player.CurrentRole}] ";
				break;
			}
		}

		Log.Info( $"[CHAT HOST] {rolePrefix}{playerName}: {message}" );

		BroadcastChatMessage( rolePrefix + playerName, message );
	}

	[Rpc.Broadcast]
	private void BroadcastChatMessage( string playerName, string message )
	{
		Log.Info( $"[CHAT] {playerName}: {message}" );
		OnChatMessageReceived?.Invoke( playerName, message );
	}
}

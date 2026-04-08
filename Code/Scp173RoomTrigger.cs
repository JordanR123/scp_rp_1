using Sandbox;
using System.Collections.Generic;
using System.Linq;

public sealed class Scp173RoomTrigger : Component, Component.ITriggerListener
{
	[Property] public Scp173Controller Scp { get; set; }

	// Keep track of player root objects currently in the zone
	private List<GameObject> _playersInRoom = new();

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		// Find the player controller anywhere in the hierarchy of what touched us
		var player = other.GameObject.Components.Get<OrionPlayerController>( FindMode.EverythingInSelfAndAncestors );

		if ( player.IsValid() )
		{
			var playerRoot = player.GameObject;

			if ( !_playersInRoom.Contains( playerRoot ) )
			{
				_playersInRoom.Add( playerRoot );
				Log.Info( $"--- SCP-173 Trigger: {playerRoot.Name} ENTERED ---" );
				UpdateNpcState();
			}
		}
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		var player = other.GameObject.Components.Get<OrionPlayerController>( FindMode.EverythingInSelfAndAncestors );

		if ( player.IsValid() )
		{
			var playerRoot = player.GameObject;

			if ( _playersInRoom.Contains( playerRoot ) )
			{
				_playersInRoom.Remove( playerRoot );
				Log.Info( $"--- SCP-173 Trigger: {playerRoot.Name} EXITED ---" );
				UpdateNpcState();
			}
		}
	}

	private void UpdateNpcState()
	{
		if ( Scp.IsValid() )
		{
			// Wake up if list is not empty
			Scp.IsAwake = _playersInRoom.Count > 0;
			Log.Info( $"SCP-173 Awake State: {Scp.IsAwake} (Players in room: {_playersInRoom.Count})" );
		}
		else
		{
			Log.Warning( "SCP-173 Trigger: No ScpController linked in Inspector!" );
		}
	}
}

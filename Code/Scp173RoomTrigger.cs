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
		if ( !Scp.IsValid() )
		{
			Log.Warning( "SCP-173 Trigger: No ScpController linked in Inspector!" );
			return;
		}

		// Only the authority side should decide whether SCP is awake.
		if ( Scp.IsProxy )
			return;

		_playersInRoom.RemoveAll( x => !x.IsValid() );

		Scp.IsAwake = _playersInRoom.Count > 0;
		Log.Info( $"SCP-173 Awake State: {Scp.IsAwake} (Players in room: {_playersInRoom.Count})" );
	}


	protected override void OnUpdate()
	{
		_playersInRoom.RemoveAll( x => !x.IsValid() );

		if ( Scp.IsValid() && !Scp.IsProxy )
		{
			var aliveCount = _playersInRoom
				.Select( go => go.Components.Get<OrionPlayerController>() )
				.Count( p => p.IsValid() && !p.IsDead && p.Health > 0 );

			var shouldBeAwake = aliveCount > 0;
			if ( Scp.IsAwake != shouldBeAwake )
			{
				Scp.IsAwake = shouldBeAwake;
				Log.Info( $"SCP-173 Awake State corrected: {Scp.IsAwake} (Alive players in room: {aliveCount})" );
			}
		}
	}


}

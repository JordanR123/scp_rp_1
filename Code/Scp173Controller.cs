using Sandbox;
using System;
using System.Linq;

public sealed class Scp173Controller : Component
{
	[Property, Group( "Stats" )] public float KillRange { get; set; } = 110f;
	[Property, Group( "Stats" )] public float MoveSpeed { get; set; } = 1500f;
	[Property, Group( "Stats" )] public float KillCooldown { get; set; } = 1.0f;
	[Property, Group( "Stats" )] public float ResetDelay { get; set; } = 5.0f;
	[Property] public bool IsAwake { get; set; } = false;

	private RealTimeSince _lastKillTime;
	private RealTimeSince _lastTimeWatched;
	private Vector3 _spawnPosition;
	private Rotation _spawnRotation;

	protected override void OnStart()
	{
		_spawnPosition = Transform.World.Position;
		_spawnRotation = Transform.World.Rotation;
		_lastTimeWatched = 0;

		// Add a tag so the visibility trace can always identify this object
		GameObject.Tags.Add( "scp173" );
	}

	protected override void OnPreRender()
	{
		if ( !IsAwake ) return;

		var target = Scene.GetAllComponents<OrionPlayerController>()
			.Where( x => x.Health > 0 )
			.OrderBy( x => Vector3.DistanceBetween( Transform.World.Position, x.Transform.World.Position ) )
			.FirstOrDefault();

		bool beingWatched = IsBeingWatched();

		if ( beingWatched )
		{
			_lastTimeWatched = 0;
			return;
		}

		if ( _lastTimeWatched > ResetDelay )
		{
			ResetToSpawn();
			return;
		}

		if ( target != null )
		{
			MoveAndAttack( target );
		}
	}

	private void MoveAndAttack( OrionPlayerController target )
	{
		float distance = Vector3.DistanceBetween( Transform.World.Position.WithZ( 0 ), target.Transform.World.Position.WithZ( 0 ) );

		if ( distance > KillRange )
		{
			Vector3 direction = (target.Transform.World.Position - Transform.World.Position).WithZ( 0 ).Normal;
			var t = Transform.World;
			t.Position += direction * MoveSpeed * Time.Delta;
			t.Rotation = Rotation.LookAt( direction );
			Transform.World = t;
		}
		else if ( _lastKillTime > KillCooldown )
		{
			Attack( target );
		}
	}

	private void Attack( OrionPlayerController target )
	{
		Log.Info( "SCP-173 SNAPPED NECK!" );
		Sound.Play( "ui.button.press", target.Transform.World.Position );

		target.Die();
		_lastKillTime = 0;

		var t = Transform.World;
		t.Position = target.Transform.World.Position + (target.Transform.World.Rotation.Forward * 35f);
		t.Rotation = Rotation.LookAt( -target.Transform.World.Rotation.Forward );
		Transform.World = t;
	}

	private void ResetToSpawn()
	{
		// Fixed the WorldTransform error here
		var t = Transform.World;
		t.Position = _spawnPosition;
		t.Rotation = _spawnRotation;
		Transform.World = t;

		_lastTimeWatched = 0;
	}

	private bool IsBeingWatched()
	{
		foreach ( var player in Scene.GetAllComponents<OrionPlayerController>() )
		{
			var cam = player.Components.GetInChildren<CameraComponent>();
			if ( cam == null ) continue;

			Vector3[] checkPoints = {
				Transform.World.Position + Vector3.Up * 10f,
				Transform.World.Position + Vector3.Up * 45f,
				Transform.World.Position + Vector3.Up * 80f
			};

			foreach ( var point in checkPoints )
			{
				var screenPos = cam.PointToScreenPixels( point );

				if ( screenPos.x > 0 && screenPos.x < Screen.Width &&
					 screenPos.y > 0 && screenPos.y < Screen.Height )
				{
					var tr = Scene.Trace.Ray( cam.Transform.World.Position, point )
						.IgnoreGameObjectHierarchy( player.GameObject )
						.Run();

					// Line of sight check
					if ( !tr.Hit || tr.GameObject.Root == GameObject || tr.GameObject.Tags.Has( "scp173" ) )
					{
						return true;
					}
				}
			}
		}
		return false;
	}
}

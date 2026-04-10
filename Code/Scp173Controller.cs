using Sandbox;
using System;
using System.Linq;

public sealed class Scp173Controller : Component
{
	[Property, Group( "Stats" )] public float KillRange { get; set; } = 110f;
	[Property, Group( "Stats" )] public float MoveSpeed { get; set; } = 1500f;
	[Property, Group( "Stats" )] public float KillCooldown { get; set; } = 1.0f;
	[Property, Group( "Stats" )] public float ResetDelay { get; set; } = 5.0f;
	[Sync] public bool IsAwake { get; set; } = false;
	[Sync] public Vector3 NetworkPosition { get; set; }
	[Sync] public Rotation NetworkRotation { get; set; }

	private RealTimeSince _lastKillTime;
	private RealTimeSince _lastTimeWatched;
	private Vector3 _spawnPosition;
	private Rotation _spawnRotation;

	protected override void OnStart()
	{
		_spawnPosition = Transform.World.Position;
		_spawnRotation = Transform.World.Rotation;
		_lastTimeWatched = 0;

		NetworkPosition = Transform.World.Position;
		NetworkRotation = Transform.World.Rotation;

		GameObject.Tags.Add( "scp173" );
	}

	protected override void OnUpdate()
	{
		// Non-authority clients do not simulate SCP logic.
		if ( IsProxy )
		{
			var t = Transform.World;
			t.Position = NetworkPosition;
			t.Rotation = NetworkRotation;
			Transform.World = t;
			return;
		}

		if ( !IsAwake )
			return;

		var target = Scene.GetAllComponents<OrionPlayerController>()
			.Where( x => x.IsValid() && x.Health > 0 && !x.IsDead )
			.OrderBy( x => Vector3.DistanceBetween( Transform.World.Position, x.Transform.World.Position ) )
			.FirstOrDefault();

		bool beingWatched = IsBeingWatched();

		if ( beingWatched )
		{
			_lastTimeWatched = 0;
			NetworkPosition = Transform.World.Position;
			NetworkRotation = Transform.World.Rotation;
			return;
		}

		if ( _lastTimeWatched > ResetDelay )
		{
			ResetToSpawn();
			NetworkPosition = Transform.World.Position;
			NetworkRotation = Transform.World.Rotation;
			return;
		}

		if ( target != null )
		{
			MoveAndAttack( target );
			NetworkPosition = Transform.World.Position;
			NetworkRotation = Transform.World.Rotation;
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
		if ( !target.IsValid() || target.IsDead || target.Health <= 0 )
			return;

		Log.Info( $"SCP-173 SNAPPED NECK of {target.GameObject.Name}!" );
		Sound.Play( "ui.button.press", target.Transform.World.Position );

		target.OnDamage( new DamageInfo
		{
			Damage = 999f,
			Attacker = GameObject,
			Position = target.Transform.World.Position
		} );

		_lastKillTime = 0;

		var t = Transform.World;
		t.Position = target.Transform.World.Position + (target.Transform.World.Rotation.Forward * 35f);
		t.Rotation = Rotation.LookAt( -target.Transform.World.Rotation.Forward );
		Transform.World = t;

		NetworkPosition = Transform.World.Position;
		NetworkRotation = Transform.World.Rotation;
	}

	private void ResetToSpawn()
	{
		var t = Transform.World;
		t.Position = _spawnPosition;
		t.Rotation = _spawnRotation;
		Transform.World = t;

		NetworkPosition = Transform.World.Position;
		NetworkRotation = Transform.World.Rotation;

		_lastTimeWatched = 0;
	}

	private bool IsBeingWatched()
	{
		foreach ( var player in Scene.GetAllComponents<OrionPlayerController>() )
		{
			if ( !player.IsValid() || player.IsDead || player.Health <= 0f )
				continue;

			var eyePos = player.NetworkEyePosition;
			var forward = Rotation.From( player.NetworkLookAngles ).Forward;

			Vector3[] checkPoints =
			{
			Transform.World.Position + Vector3.Up * 20f,
			Transform.World.Position + Vector3.Up * 50f,
			Transform.World.Position + Vector3.Up * 80f
		};

			foreach ( var point in checkPoints )
			{
				var toTarget = (point - eyePos).Normal;

				// Stricter cone than 0.3 so "roughly in the same hemisphere" doesn't count.
				if ( Vector3.Dot( forward, toTarget ) > 0.6f )
				{
					var tr = Scene.Trace.Ray( eyePos, point )
						.IgnoreGameObjectHierarchy( player.GameObject )
						.Run();

					if ( tr.Hit && (tr.GameObject.Root == GameObject || tr.GameObject.Tags.Has( "scp173" )) )
					{
						return true;
					}
				}
			}
		}

		return false;
	}
}

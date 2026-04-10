using Sandbox;

public sealed class OrionDoor : Component
{
	[Property] public int RequiredClearance { get; set; } = 1;
	[Property] public float OpenAngle { get; set; } = 90f;
	[Property] public float MoveSpeed { get; set; } = 3f;

	private Angles _closedAngles;

	[Sync( Flags = SyncFlags.FromHost )]
	public bool IsOpen { get; set; }

	protected override void OnStart()
	{
		_closedAngles = Transform.Local.Rotation.Angles();
	}

	public void OnUse( GameObject user )
	{
		// Never trust local callers for world state.
		RequestUseOnHost( user );
	}

	[Rpc.Host]
	private void RequestUseOnHost( GameObject user )
	{
		if ( !user.IsValid() )
			return;

		var player = user.Components.Get<OrionPlayerController>( FindMode.EverythingInSelfAndAncestors );
		if ( !player.IsValid() )
			return;

		if ( player.ClearanceLevel < RequiredClearance )
		{
			var localHud = user.Components.GetInChildren<OrionHUDState>();
			if ( localHud.IsValid() )
			{
				localHud.ShowAccessDenied( RequiredClearance );
			}

			return;
		}

		IsOpen = !IsOpen;
	}

	protected override void OnUpdate()
	{
		var targetAngle = IsOpen ? _closedAngles.yaw + OpenAngle : _closedAngles.yaw;
		var current = Transform.Local.Rotation.Angles();
		var newYaw = MathX.LerpTo( current.yaw, targetAngle, Time.Delta * MoveSpeed );

		Transform.Local = Transform.Local.WithRotation(
			Rotation.From( current.pitch, newYaw, current.roll )
		);
	}
}

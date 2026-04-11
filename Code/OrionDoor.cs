using Sandbox;
using System.Linq;

public sealed class OrionDoor : Component
{
	[Property] public int RequiredClearance { get; set; } = 1;
	[Property] public float OpenAngle { get; set; } = 90f;
	[Property] public float MoveSpeed { get; set; } = 3f;

	private Rotation _closedRotation;

	[Sync( Flags = SyncFlags.FromHost )]
	public bool IsOpen { get; set; }

	protected override void OnStart()
	{
		_closedRotation = LocalRotation;
	}

	public void OnUse()
	{
		RequestUseOnHost();
	}

	[Rpc.Host]
	private void RequestUseOnHost()
	{
		var player = Scene.GetAllComponents<OrionPlayerController>()
			.FirstOrDefault( p =>
				p.IsValid() &&
				p.GameObject.IsValid() &&
				p.GameObject.Network.Owner == Rpc.Caller );

		if ( !player.IsValid() )
		{
			Log.Warning( $"[DOOR] No valid player found for RPC caller on {GameObject.Name}" );
			return;
		}

		Log.Info(
			$"[DOOR] Caller player={player.GameObject.Name} clearance={player.ClearanceLevel} required={RequiredClearance}"
		);

		if ( player.ClearanceLevel < RequiredClearance )
		{
			Log.Info( $"[DOOR] ACCESS DENIED for {player.GameObject.Name}" );
			return;
		}

		IsOpen = !IsOpen;

		// Helps snap out of stale interpolation when state changes.
		Network.ClearInterpolation();

		Log.Info( $"[DOOR] {(IsOpen ? "OPENED" : "CLOSED")} by {player.GameObject.Name}" );
	}

	protected override void OnUpdate()
	{
		var targetRotation = IsOpen
			? _closedRotation * Rotation.FromYaw( OpenAngle )
			: _closedRotation;

		LocalRotation = Rotation.Slerp( LocalRotation, targetRotation, Time.Delta * MoveSpeed );
	}
}

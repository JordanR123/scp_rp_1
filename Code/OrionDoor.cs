using Sandbox;
using System.Linq;

public class OrionDoor : Component
{
	[Property] public int RequiredClearance { get; set; } = 1;
	[Property] public float OpenAngle { get; set; } = 90f;
	[Property] public float MoveSpeed { get; set; } = 3f;

	private Angles _closedAngles;
	private bool _isOpen = false;

	protected override void OnStart()
	{
		_closedAngles = Transform.Local.Rotation.Angles();
	}

	public void OnUse( GameObject user )
	{
		Log.Info( $"OnUse called on {GameObject.Name} by {user.Name}" );

		var player = user.GetComponent<OrionPlayerController>();
		if ( player == null )
		{
			Log.Error( "CRITICAL: The object using the door has no OrionPlayerController!" );
			return;
		}

		Log.Info( $"Clearance Check: Player({player.ClearanceLevel}) vs Required({RequiredClearance})" );

		if ( player.ClearanceLevel < RequiredClearance )
		{
			Log.Warning( "Clearance too low. Access Denied." );

			// FIX: Find the HUDState on the SPECIFIC player that tried to open the door
			// Instead of the first one found in the whole scene.
			var localHud = user.Components.GetInChildren<OrionHUDState>();
			if ( localHud.IsValid() )
			{
				localHud.ShowAccessDenied( RequiredClearance );
			}

			return;
		}

		_isOpen = !_isOpen;
		Log.Info( $"Door toggle success. _isOpen is now: {_isOpen}" );
	}

	protected override void OnUpdate()
	{
		var targetAngle = _isOpen ? _closedAngles.yaw + OpenAngle : _closedAngles.yaw;
		var current = Transform.Local.Rotation.Angles();

		var newYaw = MathX.LerpTo( current.yaw, targetAngle, Time.Delta * MoveSpeed );
		Transform.Local = Transform.Local.WithRotation(
			Rotation.From( current.pitch, newYaw, current.roll )
		);
	}
}

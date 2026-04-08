using Sandbox;

public class OrionHUDState : Component
{
	public bool ShowDenied { get; set; } = false;
	public int DeniedLevel { get; set; } = 0;
	public RealTimeSince DeniedTimer { get; set; }

	public void ShowAccessDenied( int level )
	{
		DeniedLevel = level;
		ShowDenied = true;
		DeniedTimer = 0;
	}
}

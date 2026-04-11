using Sandbox;
using System.Collections.Generic;

public class OrionHUDState : Component
{
	public bool ShowDenied { get; set; } = false;
	public int DeniedLevel { get; set; } = 0;
	public RealTimeSince DeniedTimer { get; set; }

	public bool ShowRoleSelect { get; set; }
	public bool ShowRespawn { get; set; }

	public bool ShowPickupHint { get; set; }
	public string PickupHintText { get; set; } = "";

	// Chat state
	public bool ShowChat { get; set; }
	public string ChatDraft { get; set; } = "";
	public List<string> ChatMessages { get; set; } = new();
	public bool IsVoiceKeyHeld { get; set; } = false;

	public void ShowAccessDenied( int level )
	{
		DeniedLevel = level;
		ShowDenied = true;
		DeniedTimer = 0;
	}

	public void AddChatMessage( string playerName, string message )
	{
		ChatMessages.Add( $"{playerName}: {message}" );

		if ( ChatMessages.Count > 8 )
			ChatMessages.RemoveAt( 0 );
	}

	public void OpenChat()
	{
		ShowChat = true;
		ChatDraft = "";
	}

	public void CloseChat()
	{
		ShowChat = false;
		ChatDraft = "";
	}
}

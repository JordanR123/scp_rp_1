using System;
using Sandbox.UI;

public sealed class OrionChatTextEntry : TextEntry
{
	public Action Submitted { get; set; }
	public Action Cancelled { get; set; }
	public Action<string> Edited { get; set; }

	public override void OnButtonEvent( ButtonEvent e )
	{
		// Only trigger on the initial press, not the release
		if ( e.Pressed )
		{
			var buttonName = e.Button.ToLower();

			if ( buttonName == "escape" )
			{
				Cancelled?.Invoke();
				return; // Stop base TextEntry from handling this
			}

			if ( buttonName == "enter" || buttonName == "kp_enter" )
			{
				Submitted?.Invoke();
				return; // Stop base TextEntry from handling this
			}
		}

		base.OnButtonEvent( e );
	}

	public override void OnValueChanged()
	{
		base.OnValueChanged();
		Edited?.Invoke( Text ?? string.Empty );
	}
}

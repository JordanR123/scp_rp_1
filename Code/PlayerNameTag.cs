using Sandbox;

public sealed class PlayerNameTag : Component
{
	[Property] public OrionPlayerController Player { get; set; }
	[Property] public float HeightOffset { get; set; } = 88f;

	private TextRenderer _textRenderer;
	private string _lastText = "";

	protected override void OnStart()
	{
		_textRenderer = Components.Get<TextRenderer>();

		if ( !_textRenderer.IsValid() )
		{
			_textRenderer = GameObject.AddComponent<TextRenderer>();
		}

		// These affect readability, but Scale is the big one for world size.
		_textRenderer.Text = "Player";
		_textRenderer.FontSize = 40;
		_textRenderer.Scale = 0.08f;
		_textRenderer.Color = Color.White;
		_textRenderer.FogStrength = 1.0f;
	}

	protected override void OnUpdate()
	{
		if ( !Player.IsValid() )
			return;

		// Put the name tag above the player's body
		WorldPosition = Player.WorldPosition + Vector3.Up * HeightOffset;

		// Make the text face the camera
		if ( Scene.Camera is not null )
		{
			var toCamera = Scene.Camera.WorldPosition - WorldPosition;

			if ( toCamera.Length > 0.001f )
			{
				WorldRotation = Rotation.LookAt( toCamera.Normal ) * Rotation.FromYaw( 180f );
			}
		}

		// Hide your own name locally and hide on death
		bool isLocalOwner = Player.GameObject.Network.IsOwner && !Player.IsProxy;
		Enabled = !isLocalOwner && !Player.IsDead;

		string playerName = string.IsNullOrWhiteSpace( Player.NetworkPlayerName )
			? "Player"
			: Player.NetworkPlayerName;

		string roleName = Player.CurrentRole switch
		{
			PlayerRole.Guard => "Guard",
			PlayerRole.Researcher => "Researcher",
			PlayerRole.DClass => "D-Class",
			_ => "Unknown"
		};

		string text = $"{playerName}\n[{roleName}]";

		if ( _lastText != text )
		{
			_lastText = text;
			_textRenderer.Text = text;
		}

		// Color per role
		_textRenderer.Color = Player.CurrentRole switch
		{
			PlayerRole.Guard => Color.Blue,
			PlayerRole.Researcher => Color.Green,
			PlayerRole.DClass => Color.Yellow,
			_ => Color.White
		};
	}
}

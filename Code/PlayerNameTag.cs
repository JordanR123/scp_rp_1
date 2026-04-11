using Sandbox;

public sealed class PlayerNameTag : Component
{
	[Property] public OrionPlayerController Player { get; set; }
	[Property] public float HeightOffset { get; set; } = 82f;
	[Property] public Color NameColor { get; set; } = Color.White;

	private TextRenderer _textRenderer;
	private string _lastText = "";

	protected override void OnStart()
	{
		_textRenderer = Components.Get<TextRenderer>();

		if ( !_textRenderer.IsValid() )
		{
			_textRenderer = GameObject.AddComponent<TextRenderer>();
		}

		_textRenderer.Text = "Player";
		_textRenderer.Color = NameColor;
		_textRenderer.FontSize = 36;
		_textRenderer.Scale = 0.05f;
		_textRenderer.FogStrength = 1.0f;
	}

	protected override void OnUpdate()
	{
		if ( !Player.IsValid() )
			return;

		WorldPosition = Player.WorldPosition + Vector3.Up * HeightOffset;

		if ( Scene.Camera is not null )
		{
			var toCamera = Scene.Camera.WorldPosition - WorldPosition;

			if ( toCamera.Length > 0.001f )
			{
				WorldRotation = Rotation.LookAt( toCamera.Normal );
			}
		}

		bool isLocalOwner = Player.GameObject.Network.IsOwner && !Player.IsProxy;
		Enabled = !isLocalOwner && !Player.IsDead;

		var nameToShow = string.IsNullOrWhiteSpace( Player.NetworkPlayerName )
			? "Player"
			: Player.NetworkPlayerName;

		if ( _lastText != nameToShow )
		{
			_lastText = nameToShow;
			_textRenderer.Text = nameToShow;
		}
	}
}

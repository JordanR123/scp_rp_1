using Sandbox;
using System;
using System.Linq;

public enum PlayerRole { DClass, Guard, Researcher }

public class PlayerData
{
	public int Level { get; set; }
	public float Experience { get; set; }
}

public partial class OrionPlayerController : Component
{
	[Property] public PlayerRole CurrentRole { get; set; } = PlayerRole.DClass;
	[Property] public int PlayerLevel { get; set; } = 1;
	[Property] public float Experience { get; set; } = 0f;
	[Property] public float Health { get; set; } = 100f;
	[Property] public int ClearanceLevel { get; set; } = 0;

	// ASSIGN THESE 3 BODY OBJECTS IN THE INSPECTOR
	// Each one should already have the correct clothing/model set up on it
	[Property] public GameObject DClassBody { get; set; }
	[Property] public GameObject GuardBody { get; set; }
	[Property] public GameObject ResearcherBody { get; set; }

	[Property, Group( "Death" )] public GameObject RagdollPrefab { get; set; }

	public bool IsDead { get; set; } = false;
	public float TimeSinceDeath { get; set; } = 0f;

	private Vector3 _deathLocation;
	private Angles _deathLookAngles;

	protected override void OnStart()
	{
		LoadGame();
		UpdateClearance();
		UpdatePlayerVisuals();
	}

	public void UpdatePlayerVisuals()
	{
		// Turn all bodies off first
		if ( DClassBody.IsValid() ) DClassBody.Enabled = false;
		if ( GuardBody.IsValid() ) GuardBody.Enabled = false;
		if ( ResearcherBody.IsValid() ) ResearcherBody.Enabled = false;

		// Turn on the correct body for the current role
		switch ( CurrentRole )
		{
			case PlayerRole.DClass:
				if ( DClassBody.IsValid() )
				{
					DClassBody.Enabled = true;
					Log.Info( "[CLOTHING] DClass body enabled." );
				}
				else
				{
					Log.Warning( "[CLOTHING] DClassBody is not assigned." );
				}
				break;

			case PlayerRole.Guard:
				if ( GuardBody.IsValid() )
				{
					GuardBody.Enabled = true;
					Log.Info( "[CLOTHING] Guard body enabled." );
				}
				else
				{
					Log.Warning( "[CLOTHING] GuardBody is not assigned." );
				}
				break;

			case PlayerRole.Researcher:
				if ( ResearcherBody.IsValid() )
				{
					ResearcherBody.Enabled = true;
					Log.Info( "[CLOTHING] Researcher body enabled." );
				}
				else
				{
					Log.Warning( "[CLOTHING] ResearcherBody is not assigned." );
				}
				break;
		}
	}

	private void HandleDeathLookOnly()
	{
		var cam = Components.GetInChildren<CameraComponent>( true );
		if ( !cam.IsValid() ) return;

		Vector3 basePos = _deathLocation + Vector3.Up * 64f;

		var lookDelta = Input.AnalogLook;

		_deathLookAngles.yaw += lookDelta.yaw;
		_deathLookAngles.pitch += lookDelta.pitch;
		_deathLookAngles.pitch = _deathLookAngles.pitch.Clamp( -80f, 80f );

		cam.WorldPosition = basePos;
		cam.WorldRotation = Rotation.From( _deathLookAngles );
	}

	protected override void OnUpdate()
	{
		if ( IsDead )
		{
			TimeSinceDeath += Time.Delta;
			HandleDeathLookOnly();
			return;
		}

		Experience += Time.Delta;

		if ( PlayerLevel < 2 && Experience >= 600f )
		{
			PlayerLevel = 2;
			SaveGame();
			Log.Info( "[PROGRESSION] Level 2 reached! Guard/Researcher roles unlocked." );
		}

		if ( Health <= 0 ) Die();

		if ( Input.Pressed( "use" ) )
		{
			HandleInteraction();
		}
	}

	public void Die()
	{
		if ( IsDead ) return;

		IsDead = true;
		TimeSinceDeath = 0f;
		_deathLocation = Transform.World.Position;
		_deathLookAngles = Transform.World.Rotation.Angles();

		if ( RagdollPrefab.IsValid() )
		{
			var ragdoll = RagdollPrefab.Clone( _deathLocation );
			ragdoll.NetworkSpawn();
		}
	}

	public void Respawn()
	{
		var manager = Scene.GetAllComponents<OrionGameManager>().FirstOrDefault();

		if ( manager.IsValid() )
		{
			Log.Info( "[RESPAWN] Resetting existing player instance." );
			manager.ResetPlayer( this );
		}
		else
		{
			Game.ActiveScene.Load( Game.ActiveScene.Source );
		}
	}

	private void HandleInteraction()
	{
		var cam = Components.GetInChildren<CameraComponent>();
		if ( cam == null ) return;

		var tr = Scene.Trace.Ray( cam.WorldPosition, cam.WorldPosition + cam.WorldRotation.Forward * 150f )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( tr.Hit && tr.GameObject.Components.Get<OrionDoor>( FindMode.EverythingInSelfAndAncestors ) is { } door )
		{
			door.OnUse( GameObject );
		}
	}

	public void UpdateClearance()
	{
		ClearanceLevel = CurrentRole switch
		{
			PlayerRole.Guard => 1,
			PlayerRole.Researcher => 2,
			_ => 0
		};
	}

	public void SaveGame()
	{
		var data = new PlayerData
		{
			Level = PlayerLevel,
			Experience = Experience
		};

		FileSystem.Data.WriteJson( "player_stats.json", data );
		Log.Info( $"[SAVE SYSTEM] Progress saved - Level: {PlayerLevel}, Experience: {Experience:F1}" );
	}

	public void LoadGame()
	{
		if ( FileSystem.Data.FileExists( "player_stats.json" ) )
		{
			var data = FileSystem.Data.ReadJson<PlayerData>( "player_stats.json" );

			if ( data != null )
			{
				PlayerLevel = data.Level;
				Experience = data.Experience;
				Log.Info( $"[SAVE SYSTEM] Loaded Level: {PlayerLevel} & Loaded Experience: {Experience:F1}" );
			}
			else
			{
				Log.Warning( "[SAVE SYSTEM] Save file found but could not be read. Resetting stats." );
				PlayerLevel = 1;
				Experience = 0f;
			}
		}
		else
		{
			Log.Info( "[SAVE SYSTEM] No save file found. Initializing new player data (Level 1)." );
			PlayerLevel = 1;
			Experience = 0f;
		}
	}

	protected override void OnDisabled()
	{
		SaveGame();
	}
}

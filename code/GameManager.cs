using System;
using Sandbox.Network;
using System.Threading.Tasks;

namespace Scenebox;

public sealed class GameManager : Component, Component.INetworkListener
{
    public static GameManager Instance { get; private set; }

    [Property] public GameObject PlayerPrefab { get; set; }
    [Property] public List<GameObject> SpawnPoints { get; set; }

    protected override void OnAwake()
    {
        Instance = this;
    }

    protected override void OnUpdate()
    {
        ThumbnailCache.CheckTextureQueue();
    }

    protected override async Task OnLoad()
    {
        if ( Scene.IsEditor )
            return;

        if ( !GameNetworkSystem.IsActive )
        {
            LoadingScreen.Title = "Creating Lobby";
            await Task.DelayRealtimeSeconds( 0.1f );
            GameNetworkSystem.CreateLobby();
        }
    }

    public void OnActive( Connection channel )
    {
        Log.Info( $"Player '{channel.DisplayName}' has joined the game" );

        if ( PlayerPrefab is null )
            return;

        var startLocation = FindSpawnLocation().WithScale( 1 );

        var player = PlayerPrefab.Clone( startLocation, name: $"Player - {channel.DisplayName}" );
        player.NetworkSpawn( channel );
    }

    Transform FindSpawnLocation()
    {
        if ( SpawnPoints is not null && SpawnPoints.Count > 0 )
        {
            return Random.Shared.FromList( SpawnPoints, default ).Transform.World;
        }

        var spawnPoints = Scene.GetAllComponents<SpawnPoint>().ToArray();
        if ( spawnPoints.Length > 0 )
        {
            return Random.Shared.FromArray( spawnPoints ).Transform.World;
        }

        return Transform.World;
    }

    public void SpawnModel( Model model, Vector3 position, Rotation rotation )
    {
        var gameObject = new GameObject();
        gameObject.Name = model.ResourceName;

        gameObject.Transform.Position = position;
        gameObject.Transform.Rotation = rotation;

        if ( model.Physics?.Parts.Count() > 0 )
        {
            var prop = gameObject.Components.Create<Prop>();
            prop.Model = model;
            gameObject.Components.Create<PropHelper>();
        }
        else
        {
            var renderer = gameObject.Components.Create<ModelRenderer>();
            renderer.Model = model;
            var collider = gameObject.Components.Create<BoxCollider>();
            collider.Center = model.Bounds.Center;
            collider.Scale = model.Bounds.Size;
            gameObject.Components.Create<Rigidbody>();
        }

        gameObject.NetworkSpawn( null );
    }

    [Broadcast]
    public void BroadcastAddTag( Guid objectId, string tag )
    {
        Scene.Directory.FindByGuid( objectId )?.Tags?.Add( tag );
    }

    [Broadcast]
    public void BroadcastRemoveTag( Guid objectId, string tag )
    {
        Scene.Directory.FindByGuid( objectId )?.Tags?.Remove( tag );
    }

    [Broadcast]
    public void BroadcastAddHighlight( Guid objectId, Color color, Color obscuredColor, float width )
    {
        var obj = Scene.Directory.FindByGuid( objectId );
        if ( obj.IsValid() )
        {
            var outline = obj.Components.GetOrCreate<HighlightOutline>();
            outline.Color = color;
            outline.ObscuredColor = obscuredColor;
            outline.Width = width;
        }
    }

    [Broadcast]
    public void BroadcastRemoveHighlight( Guid objectId )
    {
        var obj = Scene.Directory.FindByGuid( objectId );
        if ( obj.IsValid() )
        {
            obj.Components.Get<HighlightOutline>()?.Destroy();
        }
    }
}

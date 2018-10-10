using CarryCapacity.Client;
using CarryCapacity.Handler;
using CarryCapacity.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

[assembly: ModInfo("CarryCapacity",
	Description = "Adds the capability to carry various things",
	Website     = "https://github.com/copygirl/CarryCapacity",
	Authors     = new []{ "copygirl" })]

namespace CarryCapacity
{
	/// <summary> Main system for the "CarryCapacity" mod, which allows certain
	///           blocks such as chests to be picked up and carried around. </summary>
	public class CarrySystem : ModSystem
	{
		public static string MOD_ID = "carrycapacity";
		
		public override bool AllowRuntimeReload() => true;
		
		// Client
		public ICoreClientAPI ClientAPI { get; private set; }
		public IClientNetworkChannel ClientChannel { get; private set; }
		public CustomMouseHandler MouseHandler { get; private set; }
		public EntityCarryRenderer EntityCarryRenderer { get; private set; }
		public HudOverlayRenderer HudOverlayRenderer { get; private set; }
		
		// Server
		public ICoreServerAPI ServerAPI { get; private set; }
		public IServerNetworkChannel ServerChannel { get; private set; }
		public DeathHandler DeathHandler { get; private set; }
		public BackwardCompatHandler BackwardCompatHandler { get; private set; }
		
		// Common
		public CarryHandler CarryHandler { get; private set; }
		
		
		public override void Start(ICoreAPI api)
		{
			api.Register<BlockBehaviorCarryable>();
			
			CarryHandler = new CarryHandler(this);
		}
		
		public override void StartClientSide(ICoreClientAPI api)
		{
			ClientAPI     = api;
			ClientChannel = api.Network.RegisterChannel(MOD_ID)
				.RegisterMessageType(typeof(PickUpMessage))
				.RegisterMessageType(typeof(PlaceDownMessage))
				.RegisterMessageType(typeof(SwapBackMessage));
			
			MouseHandler        = new CustomMouseHandler(api);
			EntityCarryRenderer = new EntityCarryRenderer(api);
			HudOverlayRenderer  = new HudOverlayRenderer(api);
			
			CarryHandler.InitClient();
		}
		
		public override void StartServerSide(ICoreServerAPI api)
		{
			ServerAPI     = api;
			ServerChannel = api.Network.RegisterChannel(MOD_ID)
				.RegisterMessageType(typeof(PickUpMessage))
				.RegisterMessageType(typeof(PlaceDownMessage))
				.RegisterMessageType(typeof(SwapBackMessage));
			
			DeathHandler          = new DeathHandler(api);
			BackwardCompatHandler = new BackwardCompatHandler(api);
			
			CarryHandler.InitServer();
		}
	}
}

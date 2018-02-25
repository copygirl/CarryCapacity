using CarryCapacity.Client;
using CarryCapacity.Handler;
using CarryCapacity.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CarryCapacity
{
	/// <summary> Main class for the "Carry Capacity" mod, which allows certain
	///           blocks such as chests to be picked up and carried around. </summary>
	public class CarryCapacityMod : ModBase
	{
		public static ModInfo MOD_INFO { get; } = new ModInfo {
			Name        = "CarryCapacity",
			Description = "Adds the capability to carry various things",
			Website     = "https://github.com/copygirl/CarryCapacity",
			Author      = "copygirl",
		};
		
		public static string MOD_ID => MOD_INFO.Name.ToLowerInvariant();
		
		public override ModInfo GetModInfo() => MOD_INFO;
		public override bool AllowRuntimeReload() => true;
		
		
		// Client
		public ICoreClientAPI ClientAPI { get; private set; }
		public IClientNetworkChannel ClientChannel { get; private set; }
		public CustomMouseHandler MouseHandler { get; private set; }
		public EntityCarryRenderer EntityCarryRenderer { get; private set; }
		
		// Server
		public ICoreServerAPI ServerAPI { get; private set; }
		public IServerNetworkChannel ServerChannel { get; private set; }
		public DeathHandler DeathHandler { get; private set; }
		
		// Common
		public CarryHandler CarryHandler { get; private set; }
		
		
		public override void Start(ICoreAPI api)
		{
			api.Register<BlockBehaviorCarryable>();
			
			CarryHandler = new CarryHandler(this);
			
			base.Start(api);
		}
		
		public override void StartClientSide(ICoreClientAPI api)
		{
			ClientAPI     = api;
			ClientChannel = api.Network.RegisterChannel(MOD_ID)
				.RegisterMessageType(typeof(PickUpMessage))
				.RegisterMessageType(typeof(PlaceDownMessage));
			
			MouseHandler        = new CustomMouseHandler(api);
			EntityCarryRenderer = new EntityCarryRenderer(api);
			
			CarryHandler.InitClient();
		}
		
		public override void StartServerSide(ICoreServerAPI api)
		{
			ServerAPI     = api;
			ServerChannel = api.Network.RegisterChannel(MOD_ID)
				.RegisterMessageType(typeof(PickUpMessage))
				.RegisterMessageType(typeof(PlaceDownMessage));
			
			DeathHandler = new DeathHandler(api);
			
			CarryHandler.InitServer();
		}
	}
}

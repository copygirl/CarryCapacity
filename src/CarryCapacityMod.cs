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
		public ICoreClientAPI CLIENT_API { get; private set; }
		public IClientNetworkChannel CLIENT_CHANNEL { get; private set; }
		public CustomMouseHandler MOUSE_HANDLER { get; private set; }
		public CarryRenderer RENDERER { get; private set; }
		
		// Server
		public ICoreServerAPI SERVER_API { get; private set; }
		public IServerNetworkChannel SERVER_CHANNEL { get; private set; }
		public DeathHandler DEATH_HANDLER { get; private set; }
		
		// Common
		public CarryHandler CARRY_HANDLER { get; private set; }
		
		
		public override void Start(ICoreAPI api)
		{
			api.RegisterBlockBehavior(BlockBehaviorCarryable.NAME,
				typeof(BlockBehaviorCarryable));
			
			CARRY_HANDLER = new CarryHandler(this);
			
			base.Start(api);
		}
		
		public override void StartClientSide(ICoreClientAPI api)
		{
			CLIENT_API     = api;
			CLIENT_CHANNEL = api.Network.RegisterChannel(MOD_ID)
				.RegisterMessageType(typeof(PickUpMessage))
				.RegisterMessageType(typeof(PlaceDownMessage));
			
			MOUSE_HANDLER = new CustomMouseHandler(api);
			RENDERER      = new CarryRenderer(api);
			
			CARRY_HANDLER.InitClient();
		}
		
		public override void StartServerSide(ICoreServerAPI api)
		{
			SERVER_API     = api;
			SERVER_CHANNEL = api.Network.RegisterChannel(MOD_ID)
				.RegisterMessageType(typeof(PickUpMessage))
				.RegisterMessageType(typeof(PlaceDownMessage));
			
			DEATH_HANDLER = new DeathHandler(api);
			
			CARRY_HANDLER.InitServer();
		}
	}
}

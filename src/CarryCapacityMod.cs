using Vintagestory.API.Client;
using Vintagestory.API.Common;
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
		
		public static IClientNetworkChannel CLIENT_CHANNEL { get; private set; }
		
		
		public override ModInfo GetModInfo() => MOD_INFO;
		
		public override bool AllowRuntimeReload() => true;
		
		public override void Start(ICoreAPI api)
		{
			api.RegisterBlockBehavior(BlockCarryable.NAME, typeof(BlockCarryable));
			
			base.Start(api);
		}
		
		public override void StartClientSide(ICoreClientAPI api)
		{
			CLIENT_CHANNEL = api.Network.RegisterChannel(MOD_ID)
				.RegisterMessageType(typeof(PlaceDownMessage));
			
			api.World.RegisterGameTickListener((delta)
				=> BlockCarryable.OnClientPlayerUpdate(api.World.Player), 0);
			
			CarryRenderer.Register(api);
		}
		
		public override void StartServerSide(ICoreServerAPI api)
		{
			api.Network.RegisterChannel(MOD_ID)
				.RegisterMessageType(typeof(PlaceDownMessage))
				.SetMessageHandler<PlaceDownMessage>(BlockCarryable.OnPlaceDownMessage);
			
			api.Event.PlayerDeath((player, cause, source)
				=> BlockCarryable.OnPlayerDeath(player));
		}
	}
}

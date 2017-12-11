using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace CarryCapacity
{
	/// <summary> Main class for the "Carry Capacity" mod, which allows certain
	///           blocks such as chests to be picked up and carried around. </summary>
	public class CarryCapacityMod : ModBase
	{
		public static CarryCapacityMod INSTANCE { get; private set; }
		
		public static ModInfo MOD_INFO { get; } = new ModInfo {
			Name        = "CarryCapacity",
			Description = "Adds the capability to carry various things",
			Website     = "https://github.com/copygirl/CarryCapacity",
			Author      = "copygirl",
			Version     = "0.1.0",
		};
		
		
		public override ModInfo GetModInfo() { return MOD_INFO; }
		
		public override void Start(ICoreAPI api)
		{
			base.Start(api);
			INSTANCE = this;
			
			api.RegisterBlockBehavior(BlockCarryable.NAME, typeof(BlockCarryable));
		}
	}
}

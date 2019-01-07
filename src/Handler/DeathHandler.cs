using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CarryCapacity.Handler
{
	public class DeathHandler
	{
		public DeathHandler(ICoreServerAPI api)
			=> api.Event.PlayerDeath += OnPlayerDeath;
		
		private void OnPlayerDeath(IPlayer player, DamageSource source)
		{
			var carried = new HashSet<CarriedBlock>(player.Entity.GetCarried());
			if (carried.Count == 0) return;
			
			var centerBlock  = player.Entity.Pos.AsBlockPos;
			var nearbyBlocks = new List<BlockPos>(5 * 5);
			for (int x = -2; x <= 2; x++)
				for (int z = -2; z <= 2; z++)
					nearbyBlocks.Add(centerBlock.AddCopy(x, 0, z));
			nearbyBlocks = nearbyBlocks.OrderBy(b => b.DistanceTo(centerBlock)).ToList();
			
			var accessor    = player.Entity.World.BlockAccessor;
			var blockIndex  = 0;
			var distance    = 0;
			while (carried.Count > 0) {
				var pos = nearbyBlocks[blockIndex];
				if (Math.Abs(pos.Y - centerBlock.Y) < 6) {
					var sign = Math.Sign(pos.Y - centerBlock.Y);
					var testBlock   = accessor.GetBlock(pos);
					var placeable   = carried.FirstOrDefault(c => testBlock.IsReplacableBy(c.Block));
					var isPlaceable = (placeable != null);
					if (sign == 0) {
						sign = (isPlaceable ? -1 : 1);
					} else if (sign > 0) {
						if (isPlaceable) {
							player.Entity.DropCarried(pos, placeable.Slot);
							carried.Remove(placeable);
						}
					} else if (!isPlaceable) {
						var above = pos.UpCopy();
						testBlock = accessor.GetBlock(above);
						placeable = carried.FirstOrDefault(c => testBlock.IsReplacableBy(c.Block));
						if (placeable != null) {
							player.Entity.DropCarried(above, placeable.Slot);
							carried.Remove(placeable);
						}
					}
					pos.Add(0, sign, 0);
				} else if (blockIndex >= 9) break;
				
				if (++distance > 2) {
					distance = 0;
					blockIndex++;
					if (blockIndex % 4 == 4)
					if (++blockIndex >= nearbyBlocks.Count)
						blockIndex = 0;
				}
			}
			
			// FIXME: Drop container contents if blocks could not be placed.
			//        Right now, the player just gets to keep them equipped.
		}
	}
}

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CarryCapacity.Handler
{
	public class DeathHandler
	{
		public DeathHandler(ICoreServerAPI api)
			=> api.Event.PlayerDeath(OnPlayerDeath);
		
		private void OnPlayerDeath(IPlayer player, DamageSource source)
		{
			var carried = player.Entity.GetCarried();
			if (carried == null) return;
			
			var testPos = player.Entity.Pos.AsBlockPos;
			BlockPos firstEmpty  = null;
			BlockPos groundEmpty = null;
			// Test up to 10 blocks up and down, find
			// a solid ground to place the block on.
			for (var i = 0; i < 10; i++) {
				var testBlock = player.Entity.World.BlockAccessor.GetBlock(testPos);
				if (testBlock.IsReplacableBy(carried.Block)) {
					if (firstEmpty == null) {
						if (i > 0) {
							groundEmpty = testPos;
							break;
						} else firstEmpty = testPos;
					}
				} else if (firstEmpty != null) {
					groundEmpty = testPos.Add(BlockFacing.UP);
					break;
				}
			}
			
			var placedPos = groundEmpty ?? firstEmpty;
			if (placedPos == null) return;
			
			// FIXME: Drop container contents if block could not be placed.
			player.Entity.DropCarried(placedPos);
		}
	}
}

using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace CarryCapacity
{
	/// <summary> Block behavior which, when added to a block, will allow
	///           said block to be picked up by players and carried around. </summary>
	public class BlockCarryable : BlockBehavior
	{
		public static string NAME { get; } = "Carryable";
		
		
		public BlockCarryable(Block block)
			: base(block) {  }
		
		public override bool OnPlayerPlacedBlockInteract(
			IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
		{
			var isSneaking    = byPlayer.Entity.Controls.Sneak;
			var isEmptyHanded = byPlayer.Entity.HeldItemSlot.Empty;
			
			// Only activate this block behavior if sneaking with an empty hand.
			if (!isSneaking || !isEmptyHanded) return false;
			
			if (world.Side == EnumAppSide.Server) {
				var pos    = blockSel.Position;
				var block  = world.BlockAccessor.GetBlockId(pos);
				var entity = world.BlockAccessor.GetBlockEntity(pos);
				
				byPlayer.WorldData.MoveSpeedMultiplier -= 0.5F;
				
				world.BlockAccessor.SetBlock(0, pos);
			}
			
			handling = EnumHandling.PreventDefault;
			return true;
		}
		
		/* TODO: Add a delay to picking up things.
		
		public override bool OnInteracting(
			float secondsUsed, IItemSlot slot, IEntityAgent byEntity,
			BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
		{
			handling = EnumHandling.PreventDefault;
			return true;
		}
		
		public override void OnInteractOver(
			float secondsUsed, IItemSlot slot, IEntityAgent byEntity,
			BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
		{
			
		}
		*/
	}
}

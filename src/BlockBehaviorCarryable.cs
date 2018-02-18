using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace CarryCapacity
{
	/// <summary> Block behavior which, when added to a block, will allow
	///           said block to be picked up by players and carried around. </summary>
	public class BlockBehaviorCarryable : BlockBehavior
	{
		public static string NAME { get; } = "Carryable";
		
		public BlockBehaviorCarryable(Block block)
			: base(block) {  }
		
		public override bool OnPlayerPlacedBlockInteract(
			IWorldAccessor world, IPlayer byPlayer,
			BlockSelection blockSel, ref EnumHandling handling)
		{
			var isSneaking    = byPlayer.Entity.Controls.Sneak;
			var isEmptyHanded = byPlayer.Entity.RightHandItemSlot.Empty;
			// Prevent default action if sneaking and empty handed,
			// as we want to handle block pickup in this case.
			if (isSneaking && isEmptyHanded) {
				handling = EnumHandling.PreventDefault;
				return false;
			} else return true;
		}
	}
}

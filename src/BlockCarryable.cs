using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CarryCapacity
{
	/// <summary> Block behavior which, when added to a block, will allow
	///           said block to be picked up by players and carried around. </summary>
	public class BlockCarryable : BlockBehavior
	{
		public static string NAME { get; } = "Carryable";
		
		public static string ATTRIBUTE_ID { get; } =
			$"{ CarryCapacityMod.MOD_ID }:CarriedBlock";
		
		
		public BlockCarryable(Block block)
			: base(block) {  }
		
		public override bool OnPlayerPlacedBlockInteract(
			IWorldAccessor world, IPlayer byPlayer,
			BlockSelection blockSel, ref EnumHandling handling)
		{
			var entity = byPlayer.Entity;
			
			var isSneaking    = entity.Controls.Sneak;
			var isEmptyHanded = entity.RightHandItemSlot.Empty;
			var isCarrying    = entity.WatchedAttributes.HasAttribute(ATTRIBUTE_ID);
			
			// Only activate this block behavior if sneaking with an empty hand.
			if (!isSneaking || !isEmptyHanded || isCarrying) return false;
			
			if (world.Side == EnumAppSide.Server) {
				var pos = blockSel.Position;
				var blockCode   = world.BlockAccessor.GetBlock(pos).Code;
				var blockEntity = world.BlockAccessor.GetBlockEntity(pos);
				
				if (blockEntity != null) {
					// Save the block entity data to TreeAttribute and
					// then on the non-synced attributes of the player.
					var tree = new TreeAttribute();
					blockEntity.ToTreeAttributes(tree);
					entity.Attributes[ATTRIBUTE_ID] = tree;
					
					// Remove the block entity, don't want containers dropping their contents.
					world.BlockAccessor.RemoveBlockEntity(pos);
				}
				
				// Save the carried block code to the synced attributes of the player.
				entity.WatchedAttributes.SetString(ATTRIBUTE_ID, blockCode.ToString());
				
				// Remove the block from the world.
				world.BlockAccessor.SetBlock(0, pos);
			}
			
			handling = EnumHandling.PreventDefault;
			return true;
		}
		
		private static bool _prevInteracting;
		public static void OnClientPlayerUpdate(IClientPlayer player)
		{
			var entity = player.Entity;
			var world  = entity.World;
			
			// Check if right mouse was pressed since last call.
			var isInteracting = entity.Controls.RightMouseDown;
			if (_prevInteracting != isInteracting) {
				_prevInteracting = isInteracting;
				if (!isInteracting) return;
			} else return;
			
			var selection = player.CurrentBlockSelection;
			if ((selection == null) || !PlaceDown(player, selection)) return;
			CarryCapacityMod.CLIENT_CHANNEL.SendPacket(new PlaceDownMessage(selection));
		}
		
		public static void OnPlaceDownMessage(IPlayer player, PlaceDownMessage message)
		{
			if (!PlaceDown(player, message.Selection)) {
				player.Entity.World.BlockAccessor.MarkBlockDirty(message.Position);
				player.Entity.WatchedAttributes.MarkPathDirty(ATTRIBUTE_ID);
			}
		}
		
		public static bool PlaceDown(IPlayer player, BlockSelection selection)
		{
			var entity = player.Entity;
			var world  = entity.World;
			
			var isSneaking    = entity.Controls.Sneak;
			var isEmptyHanded = entity.RightHandItemSlot.Empty;
			var isCarrying    = entity.WatchedAttributes.HasAttribute(ATTRIBUTE_ID);
			
			if (!isSneaking || !isEmptyHanded || !isCarrying
				|| (selection.Face != BlockFacing.UP)) return false;
			
			var clickedBlock = world.BlockAccessor.GetBlock(selection.Position);
			if (!clickedBlock.SideSolid[selection.Face.Index]) return false;
			
			var carryingBlockCode = entity.WatchedAttributes.GetString(ATTRIBUTE_ID);
			var carryingBlock     = world.GetBlock(new AssetLocation(carryingBlockCode));
			
			var emptyBlockPos = selection.Position.AddCopy(selection.Face);
			var emptyBlock    = world.BlockAccessor.GetBlock(emptyBlockPos);
			if (!emptyBlock.IsReplacableBy(carryingBlock)) return false;
			
			world.BlockAccessor.SetBlock((ushort)carryingBlock.Id, emptyBlockPos);
			entity.WatchedAttributes.RemoveAttribute(ATTRIBUTE_ID);
			
			if ((world.Side == EnumAppSide.Server)
				&& entity.Attributes.HasAttribute(ATTRIBUTE_ID)) {
				
				var blockEntityData = entity.Attributes.GetTreeAttribute(ATTRIBUTE_ID);
				blockEntityData.SetInt("posx", emptyBlockPos.X);
				blockEntityData.SetInt("posy", emptyBlockPos.Y);
				blockEntityData.SetInt("posz", emptyBlockPos.Z);
				entity.Attributes.RemoveAttribute(ATTRIBUTE_ID);
				
				var blockEntity = world.BlockAccessor.GetBlockEntity(emptyBlockPos);
				blockEntity.FromTreeAtributes(blockEntityData);
				
			}
			
			return true;
		}
	}
}

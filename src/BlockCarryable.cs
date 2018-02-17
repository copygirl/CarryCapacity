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
			
			var pos       = blockSel.Position;
			var block     = world.BlockAccessor.GetBlock(pos);
			var blockCode = block.OnPickBlock(world, pos)?.Block?.Code;
			// If block code can't be found, abort!
			if (blockCode == null) return false;
			
			if (world.Side == EnumAppSide.Server) {
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
			// FIXME: Do at least some validation of this data.
			if (!PlaceDown(player, message.Selection)) {
				player.Entity.World.BlockAccessor.MarkBlockDirty(message.Selection.Position);
				player.Entity.WatchedAttributes.MarkPathDirty(ATTRIBUTE_ID);
			}
		}
		
		public static bool PlaceDown(IPlayer player, BlockSelection selection)
		{
			// Clone the selection, because we don't
			// want to affect what is sent to the server.
			selection = selection.Clone();
			
			var entity = player.Entity;
			var world  = entity.World;
			
			var isSneaking    = entity.Controls.Sneak;
			var isEmptyHanded = entity.RightHandItemSlot.Empty;
			var isCarrying    = entity.WatchedAttributes.HasAttribute(ATTRIBUTE_ID);
			if (!isSneaking || !isEmptyHanded || !isCarrying) return false;
			
			var carryingBlockCode = entity.WatchedAttributes.GetString(ATTRIBUTE_ID);
			var carryingBlock     = world.GetBlock(new AssetLocation(carryingBlockCode));
			if (carryingBlock == null) return false;
			
			var clickedBlock = world.BlockAccessor.GetBlock(selection.Position);
			// If clicked block is replacable, check block below instead.
			if (clickedBlock.IsReplacableBy(carryingBlock)) {
				selection.Face = BlockFacing.UP;
				clickedBlock   = world.BlockAccessor.GetBlock(selection.Position.DownCopy());
			// Otherwise make sure that the block was clicked on the top side.
			} else if (selection.Face == BlockFacing.UP) {
				selection.Position.Up();
				selection.DidOffset = true;
			} else return false;
			
			// And also that the clicked block is solid on top.
			if (!clickedBlock.SideSolid[BlockFacing.UP.Index]) return false;
			
			// Now try placing the block. Just going to utilize the default
			// block placement using an item stack. Let's hope nothing breaks!
			var stack = new ItemStack(carryingBlock);
			if (!carryingBlock.TryPlaceBlock(world, player, stack, selection)) return false;
			entity.WatchedAttributes.RemoveAttribute(ATTRIBUTE_ID);
			
			RestoreBlockEntityData(entity, selection.Position);
			
			return true;
		}
		
		public static void OnPlayerDeath(IServerPlayer player)
		{
			var entity = player.Entity;
			var world  = entity.World;
			
			var isSneaking    = entity.Controls.Sneak;
			var isEmptyHanded = entity.RightHandItemSlot.Empty;
			var isCarrying    = entity.WatchedAttributes.HasAttribute(ATTRIBUTE_ID);
			if (!isSneaking || !isEmptyHanded || !isCarrying) return;
			
			var carryingBlockCode = entity.WatchedAttributes.GetString(ATTRIBUTE_ID);
			var carryingBlock     = world.GetBlock(new AssetLocation(carryingBlockCode));
			if (carryingBlock == null) return;
			
			var testPos = player.Entity.Pos.AsBlockPos;
			BlockPos firstEmpty  = null;
			BlockPos groundEmpty = null;
			// Test up to 10 blocks up and down, find
			// a solid ground to place the block on.
			for (var i = 0; i < 10; i++) {
				var testBlock = world.BlockAccessor.GetBlock(testPos);
				if (testBlock.IsReplacableBy(carryingBlock)) {
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
			if (!world.BlockAccessor.IsValidPos(placedPos)) return;
			
			world.BlockAccessor.SetBlock((ushort)carryingBlock.Id, placedPos);
			entity.WatchedAttributes.RemoveAttribute(ATTRIBUTE_ID);
			RestoreBlockEntityData(entity, placedPos);
		}
		
		
		private static void RestoreBlockEntityData(
			EntityPlayer entity, BlockPos position)
		{
			if ((entity.World.Side != EnumAppSide.Server)
				|| !entity.Attributes.HasAttribute(ATTRIBUTE_ID)) return;
			
			var blockEntityData = entity.Attributes.GetTreeAttribute(ATTRIBUTE_ID);
			// Set the block entity's position to the new position.
			// Without this, we get some funny behavior.
			blockEntityData.SetInt("posx", position.X);
			blockEntityData.SetInt("posy", position.Y);
			blockEntityData.SetInt("posz", position.Z);
			
			var blockEntity = entity.World.BlockAccessor.GetBlockEntity(position);
			blockEntity.FromTreeAtributes(blockEntityData);
			
			entity.Attributes.RemoveAttribute(ATTRIBUTE_ID);
		}
	}
}

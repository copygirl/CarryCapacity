using CarryCapacity.Network;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace CarryCapacity.Handler
{
	/// <summary>
	///   Takes care of core CarryCapacity handling, such as listening to input events,
	///   picking up, placing and swapping blocks, as well as sending and handling messages.
	/// </summary>
	public class CarryHandler
	{
		public const float PLACE_SPEED_MODIFIER = 0.75F;
		public const float SWAP_SPEED_MODIFIER  = 1.5F;
		
		private CurrentAction _action   = CurrentAction.None;
		private BlockPos _selectedBlock = null;
		
		private CarrySystem Mod { get; }
		
		public CarryHandler(CarrySystem mod)
			=> Mod = mod;
		
		public void InitClient()
		{
			Mod.MouseHandler.OnRightMousePressed  += OnPress;
			Mod.MouseHandler.OnRightMouseHeld     += OnHold;
			Mod.MouseHandler.OnRightMouseReleased += OnRelease;
		}
		
		public void InitServer()
		{
			Mod.ServerChannel
				.SetMessageHandler<PickUpMessage>(OnPickUpMessage)
				.SetMessageHandler<PlaceDownMessage>(OnPlaceDownMessage)
				.SetMessageHandler<SwapBackMessage>(OnSwapBackMessage);
			
			Mod.ServerAPI.Event.OnEntitySpawn += OnEntitySpawn;
		}
		
		
		public void OnEntitySpawn(Entity entity)
		{
			// Set this again so walk speed modifiers and animations can be applied.
			foreach (var carried in entity.GetCarried())
				carried.Set(entity, carried.Slot);
		}
		
		
		public void OnPress()
		{
			var world        = Mod.ClientAPI.World;
			var player       = world.Player;
			var selection    = player.CurrentBlockSelection;
			var carriedHands = player.Entity.GetCarried(CarrySlot.Hands);
			var carriedBack  = player.Entity.GetCarried(CarrySlot.Back);
			
			if (carriedHands != null) {
			  // If something's being carried in-hand and aiming at block, try to place it.
				if (selection != null) {
					// Make sure it's put on a solid top face of a block.
					if (!CanPlace(world, selection, carriedHands)) return;
					_action        = CurrentAction.PlaceDown;
					_selectedBlock = GetPlacedPosition(world, selection, carriedHands.Block);
				}
				// If something's being carried in-hand and aiming at nothing, try to put held block on back.
				else if ((carriedBack == null) && (carriedHands.Behavior.Slots[CarrySlot.Back] != null))
					_action = CurrentAction.SwapBack;
			}
			// If nothing's being carried in-hand and aiming at carryable block, try to pick it up.
			else if ((selection != null) && world.BlockAccessor.GetBlock(selection.Position).IsCarryable(CarrySlot.Hands)) {
				_action        = CurrentAction.PickUp;
				_selectedBlock = selection.Position;
			}
			// If nothing's being carried in-hand and aiming at nothing or non-carryable block, try to grab block on back.
			else if ((carriedBack != null) && (carriedBack.Behavior.Slots[CarrySlot.Hands] != null))
				_action = CurrentAction.SwapBack;
			
			OnHold(0.0F);
		}
		
		public void OnHold(float time)
		{
			if (_action == CurrentAction.None) return;
			var world  = Mod.ClientAPI.World;
			var player = world.Player;
			
			// TODO: Don't run any of this while in a GUI.
			// TODO: Only allow close blocks to be picked up.
			// TODO: Don't allow the block underneath to change?
			
			var isSneaking    = player.Entity.Controls.Sneak;
			var isEmptyHanded = player.Entity.RightHandItemSlot.Empty;
			// Only perform action if sneaking with empty hands.
			if (!isSneaking || !isEmptyHanded)
				{ OnRelease(); return; }
			
			var carriedHands = player.Entity.GetCarried(CarrySlot.Hands);
			BlockSelection selection = null;
			BlockBehaviorCarryable behavior;
			
			switch (_action) {
				case CurrentAction.PickUp:
				case CurrentAction.PlaceDown:
					
					// Ensure the player hasn't in the meantime
					// picked up / placed down something somehow.
					if ((_action == CurrentAction.PickUp) == (carriedHands != null))
						{ OnRelease(); return; }
					
					selection     = player.CurrentBlockSelection;
					var position  = (_action == CurrentAction.PlaceDown)
						? GetPlacedPosition(world, selection, carriedHands.Block)
						: selection?.Position;
					// Make sure the player is still looking at the same block.
					if (!_selectedBlock.Equals(position)) { OnRelease(); return; }
					
					// Get the block behavior from either the block
					// to be picked up or the currently carried block.
					behavior = (_action == CurrentAction.PickUp)
						? world.BlockAccessor.GetBlock(selection.Position)
							.GetBehaviorOrDefault(BlockBehaviorCarryable.DEFAULT)
						: carriedHands.Behavior;
					break;
				
				case CurrentAction.SwapBack:
					
					var carriedBack = player.Entity.GetCarried(CarrySlot.Back);
					// Ensure the player hasn't in the meantime
					// put something in their hands / back.
					if ((carriedHands != null) == (carriedBack != null))
						{ OnRelease(); return; }
					
					var targetSlot = (carriedHands != null) ? CarrySlot.Back : CarrySlot.Hands;
					behavior       = (carriedHands != null) ? carriedHands.Behavior : carriedBack.Behavior;
					// Make sure the block to swap can still be put in that slot.
					if (behavior.Slots[targetSlot] == null) return;
					
					break;
				
				default: return;
			}
			
			var requiredTime = behavior.InteractDelay;
			switch (_action) {
				case CurrentAction.PlaceDown: requiredTime *= PLACE_SPEED_MODIFIER; break;
				case CurrentAction.SwapBack:  requiredTime *= SWAP_SPEED_MODIFIER;  break;
			}
			
			var progress = (time / requiredTime);
			Mod.HudOverlayRenderer.CircleProgress = progress;
			if (progress <= 1.0F) return;
			
			switch (_action) {
				case CurrentAction.PickUp:
					if (player.Entity.Carry(selection.Position, CarrySlot.Hands))
						Mod.ClientChannel.SendPacket(new PickUpMessage(selection.Position));
					break;
				case CurrentAction.PlaceDown:
					if (PlaceDown(player, carriedHands, selection))
						Mod.ClientChannel.SendPacket(new PlaceDownMessage(selection));
					break;
				case CurrentAction.SwapBack:
					if (player.Entity.SwapCarriedHandsWithBack())
						Mod.ClientChannel.SendPacket(new SwapBackMessage());
					break;
			}
			
			OnRelease();
		}
		
		public void OnRelease()
		{
			_action = CurrentAction.None;
			Mod.HudOverlayRenderer.CircleVisible = false;
		}
		
		
		public static void OnPickUpMessage(IPlayer player, PickUpMessage message)
		{
			// FIXME: Do at least some validation of this data.
			
			var isSneaking    = player.Entity.Controls.Sneak;
			var isEmptyHanded = player.Entity.RightHandItemSlot.Empty;
			var carried       = player.Entity.GetCarried(CarrySlot.Hands);
			
			if (!isSneaking || !isEmptyHanded || (carried != null) ||
			    !player.Entity.Carry(message.Position, CarrySlot.Hands))
					InvalidCarry(player, message.Position);
		}
		
		public static void OnPlaceDownMessage(IPlayer player, PlaceDownMessage message)
		{
			// FIXME: Do at least some validation of this data.
			
			var isSneaking    = player.Entity.Controls.Sneak;
			var isEmptyHanded = player.Entity.RightHandItemSlot.Empty;
			var carried       = player.Entity.GetCarried(CarrySlot.Hands);
			
			if (!isSneaking || !isEmptyHanded || (carried == null) ||
			    !PlaceDown(player, carried, message.Selection))
					InvalidCarry(player, message.Selection.Position);
		}
		
		public static void OnSwapBackMessage(IPlayer player, SwapBackMessage message)
		{
			var isSneaking    = player.Entity.Controls.Sneak;
			var isEmptyHanded = player.Entity.RightHandItemSlot.Empty;
			if (!isSneaking || !isEmptyHanded ||
			    !player.Entity.SwapCarriedHandsWithBack())
				player.Entity.WatchedAttributes.MarkPathDirty(CarriedBlock.ATTRIBUTE_ID);
		}
		
		public static bool CanPlace(IWorldAccessor world, BlockSelection selection,
		                            CarriedBlock carried)
		{
			var clickedBlock = world.BlockAccessor.GetBlock(selection.Position);
			return clickedBlock.IsReplacableBy(carried.Block)
				// If clicked block is replacable, check block below instead.
				? world.BlockAccessor.GetBlock(selection.Position.DownCopy())
					.SideSolid[BlockFacing.UP.Index]
				// Otherwise, just make sure the clicked side is solid.
				: clickedBlock.SideSolid[selection.Face.Index];
		}
		
		public static bool PlaceDown(IPlayer player, CarriedBlock carried,
		                             BlockSelection selection)
		{
			if (!CanPlace(player.Entity.World, selection, carried)) return false;
			var clickedBlock = player.Entity.World.BlockAccessor.GetBlock(selection.Position);
			
			// Clone the selection, because we don't
			// want to affect what is sent to the server.
			selection = selection.Clone();
			
			if (clickedBlock.IsReplacableBy(carried.Block)) {
				selection.Face = BlockFacing.UP;
				selection.HitPosition.Y = 1.0;
			} else {
				selection.Position.Offset(selection.Face);
				selection.DidOffset = true;
			}
			
			return player.PlaceCarried(selection, CarrySlot.Hands);
		}
		
		/// <summary> Called when a player picks up or places down an invalid block,
		///           requiring it to get notified about the action being rejected. </summary>
		private static void InvalidCarry(IPlayer player, BlockPos pos)
		{
			player.Entity.World.BlockAccessor.MarkBlockDirty(pos);
			player.Entity.WatchedAttributes.MarkPathDirty(CarriedBlock.ATTRIBUTE_ID);
		}
		
		/// <summary> Returns the position that the specified block would
		///           be placed at for the specified block selection. </summary>
		private static BlockPos GetPlacedPosition(
			IWorldAccessor world, BlockSelection selection, Block block)
		{
			if (selection == null) return null;
			var position     = selection.Position.Copy();
			var clickedBlock = world.BlockAccessor.GetBlock(position);
			if (!clickedBlock.IsReplacableBy(block)) {
				if (clickedBlock.SideSolid[selection.Face.Index])
					position.Offset(selection.Face);
				else return null;
			}
			return position;
		}
		
		private enum CurrentAction
		{
			None,
			PickUp,
			PlaceDown,
			SwapBack
		}
	}
}

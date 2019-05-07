using CarryCapacity.Common.Network;
using CarryCapacity.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace CarryCapacity.Common
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
		private CarrySlot? _targetSlot  = null;
		private BlockPos _selectedBlock = null;
		private float _timeHeld         = 0.0F;
		
		private CarrySystem System { get; }
		
		public CarryHandler(CarrySystem system)
			=> System = system;
		
		public void InitClient()
		{
			System.ClientAPI.Event.MouseDown += OnMouseDown;
			System.ClientAPI.Event.MouseUp   += OnMouseUp;
			System.ClientAPI.Event.RegisterGameTickListener(OnGameTick, 0);
			
			// FIXME: Disabled this portion of the code, since the updated event
			//        isn't available because my merge request wasn't accepted yet.
			// Mod.ClientAPI.Event.BeforeActiveSlotChanged +=
			// 	(ev) => OnBeforeActiveSlotChanged(Mod.ClientAPI.World.Player.Entity, ev);
		}
		
		public void InitServer()
		{
			System.ServerChannel
				.SetMessageHandler<PickUpMessage>(OnPickUpMessage)
				.SetMessageHandler<PlaceDownMessage>(OnPlaceDownMessage)
				.SetMessageHandler<SwapSlotsMessage>(OnSwapSlotsMessage);
			
			System.ServerAPI.Event.OnEntitySpawn += OnEntitySpawn;
			
			// Mod.ServerAPI.Event.BeforeActiveSlotChanged +=
			// 	(player, ev) => OnBeforeActiveSlotChanged(player.Entity, ev);
		}
		
		
		public void OnEntitySpawn(Entity entity)
		{
			// Set this again so walk speed modifiers and animations can be applied.
			foreach (var carried in entity.GetCarried())
				carried.Set(entity, carried.Slot);
		}
		
		
		/// <summary> Returns the first "action" slot (either Hands or Shoulder)
		///           that satisfies the specified function, or null if none. </summary>
		private static CarrySlot? FindActionSlot(System.Func<CarrySlot, bool> func)
		{
			if (func(CarrySlot.Hands   )) return CarrySlot.Hands;
			if (func(CarrySlot.Shoulder)) return CarrySlot.Shoulder;
			return null;
		}
		
		public void OnMouseDown(MouseEvent ev)
		{
			var world     = System.ClientAPI.World;
			var player    = world.Player;
			var selection = player.CurrentBlockSelection;
			var block     = (selection != null) ? world.BlockAccessor.GetBlock(selection.Position) : null;
			
			var carriedHands    = player.Entity.GetCarried(CarrySlot.Hands);
			var carriedBack     = player.Entity.GetCarried(CarrySlot.Back);
			var carriedShoulder = player.Entity.GetCarried(CarrySlot.Shoulder);
			var carriedAny      = carriedHands ?? carriedShoulder;
			
			// If something is being carried in-hand, make sure to prevent the default action.
			if (carriedHands != null) ev.Handled = true;
			// FIXME: This prevents interactions in the GUI. We need dedicated events for block/entity interactions.
			
			// Only continue if the right (interact) mouse button is held and player is sneaking with an empty hand.
			if ((ev.Button != EnumMouseButton.Right) || !CanInteract(player.Entity)) return;
			
			if (carriedAny != null) {
				// If something's being carried in-hand or on shoulder and aiming at block, try to place it.
				if (selection != null) {
					// Make sure it's put on a solid top face of a block.
					if (!CanPlace(world, selection, carriedAny)) return;
					_action        = CurrentAction.PlaceDown;
					_targetSlot    = carriedAny.Slot;
					_selectedBlock = GetPlacedPosition(world, selection, carriedAny.Block);
				}
				// If something's being carried in-hand and aiming at nothing, try to put held block on back.
				else if ((carriedBack == null) && (carriedAny.Behavior.Slots[CarrySlot.Back] != null)) {
					_action     = CurrentAction.SwapBack;
					_targetSlot = carriedAny.Slot;
				}
			}
			// If nothing's being carried in-hand and aiming at carryable block, try to pick it up.
			else if ((selection != null) && ((_targetSlot = FindActionSlot(slot => block.IsCarryable(slot))) != null)) {
				_action        = CurrentAction.PickUp;
				_selectedBlock = selection.Position;
			}
			// If nothing's being carried in-hand and aiming at nothing or non-carryable block, try to grab block on back.
			else if ((carriedBack != null) && ((_targetSlot = FindActionSlot(slot => (carriedBack.Behavior.Slots[slot] != null))) != null))
				_action = CurrentAction.SwapBack;
			
			OnGameTick(0.0F);  // Run this once to for validation. May reset action to None.
			ev.Handled = true; // Prevent default action. Don't want to interact with blocks.
		}
		
		public void OnGameTick(float deltaTime)
		{
			if (_action == CurrentAction.None) return;
			var world  = System.ClientAPI.World;
			var player = world.Player;
			
			// TODO: Don't run any of this while in a GUI.
			// TODO: Only allow close blocks to be picked up.
			// TODO: Don't allow the block underneath to change?
			
			// Only perform action if sneaking with empty hands.
			if (!CanInteract(player.Entity))
				{ OnMouseUp(); return; }
			
			var carriedTarget = _targetSlot.HasValue ? player.Entity.GetCarried(_targetSlot.Value) : null;
			var carriedAny    = player.Entity.GetCarried(CarrySlot.Hands)
			                 ?? player.Entity.GetCarried(CarrySlot.Shoulder);
			BlockSelection selection = null;
			BlockBehaviorCarryable behavior;
			
			switch (_action) {
				case CurrentAction.PickUp:
				case CurrentAction.PlaceDown:
					
					// Ensure the player hasn't in the meantime
					// picked up / placed down something somehow.
					if ((_action == CurrentAction.PickUp) == (carriedAny != null))
						{ OnMouseUp(); return; }
					
					selection    = player.CurrentBlockSelection;
					var position = (_action == CurrentAction.PlaceDown)
						? GetPlacedPosition(world, selection, carriedTarget.Block)
						: selection?.Position;
					// Make sure the player is still looking at the same block.
					if (!_selectedBlock.Equals(position))
						{ OnMouseUp(); return; }
					
					// Get the block behavior from either the block
					// to be picked up or the currently carried block.
					behavior = (_action == CurrentAction.PickUp)
						? world.BlockAccessor.GetBlock(selection.Position)
							.GetBehaviorOrDefault(BlockBehaviorCarryable.DEFAULT)
						: carriedTarget.Behavior;
					break;
				
				case CurrentAction.SwapBack:
					
					var carriedBack = player.Entity.GetCarried(CarrySlot.Back);
					// Ensure that the player hasn't in the meantime
					// put something in that slot / on their back.
					if ((carriedTarget != null) == (carriedBack != null))
						{ OnMouseUp(); return; }
					
					behavior = (carriedTarget != null) ? carriedTarget.Behavior : carriedBack.Behavior;
					// Make sure the block to swap can still be put in that slot.
					if (behavior.Slots[_targetSlot.Value] == null) return;
					
					break;
				
				default: return;
			}
			
			var requiredTime = behavior.InteractDelay;
			switch (_action) {
				case CurrentAction.PlaceDown: requiredTime *= PLACE_SPEED_MODIFIER; break;
				case CurrentAction.SwapBack:  requiredTime *= SWAP_SPEED_MODIFIER;  break;
			}
			
			_timeHeld += deltaTime;
			var progress = (_timeHeld / requiredTime);
			System.HudOverlayRenderer.CircleProgress = progress;
			if (progress <= 1.0F) return;
			
			switch (_action) {
				case CurrentAction.PickUp:
					if (player.Entity.Carry(selection.Position, _targetSlot.Value))
						System.ClientChannel.SendPacket(new PickUpMessage(selection.Position, _targetSlot.Value));
					break;
				case CurrentAction.PlaceDown:
					if (PlaceDown(player, carriedTarget, selection))
						System.ClientChannel.SendPacket(new PlaceDownMessage(_targetSlot.Value, selection));
					break;
				case CurrentAction.SwapBack:
					if (player.Entity.Swap(_targetSlot.Value, CarrySlot.Back))
						System.ClientChannel.SendPacket(new SwapSlotsMessage(CarrySlot.Back, _targetSlot.Value));
					break;
			}
			
			OnMouseUp();
		}
		
		public void OnMouseUp(MouseEvent ev = null)
		{
			_action     = CurrentAction.None;
			_targetSlot = null;
			_timeHeld   = 0.0F;
			System.HudOverlayRenderer.CircleVisible = false;
		}
		
		
		// public EnumHandling OnBeforeActiveSlotChanged(EntityAgent entity, ActiveSlotChangeEventArgs ev)
		// {
		// 	// If the player is carrying something in their hands,
		// 	// prevent them from changing their active hotbar slot.
		// 	return (entity.GetCarried(CarrySlot.Hands) != null)
		// 		? EnumHandling.PreventDefault
		// 		: EnumHandling.NotHandled;
		// }
		
		
		public static void OnPickUpMessage(IPlayer player, PickUpMessage message)
		{
			// FIXME: Do at least some validation of this data.
			
			var carried = player.Entity.GetCarried(message.Slot);
			if ((message.Slot == CarrySlot.Back) || !CanInteract(player.Entity) ||
			    (carried != null) || !player.Entity.Carry(message.Position, message.Slot))
				InvalidCarry(player, message.Position);
		}
		
		public static void OnPlaceDownMessage(IPlayer player, PlaceDownMessage message)
		{
			// FIXME: Do at least some validation of this data.
			
			var carried = player.Entity.GetCarried(message.Slot);
			if ((message.Slot == CarrySlot.Back) || !CanInteract(player.Entity) ||
			    (carried == null) || !PlaceDown(player, carried, message.Selection))
				InvalidCarry(player, message.Selection.Position);
		}
		
		public static void OnSwapSlotsMessage(IPlayer player, SwapSlotsMessage message)
		{
			if ((message.First == message.Second) || (message.First != CarrySlot.Back) ||
			    !CanInteract(player.Entity) || !player.Entity.Swap(message.First, message.Second))
				player.Entity.WatchedAttributes.MarkPathDirty(CarriedBlock.ATTRIBUTE_ID);
		}
		
		
		/// <summary>
		///   Returns whether the specified entity has the required prerequisites
		///   to interact using CarryCapacity: Must be sneaking with an empty hand.
		/// </summary>
		public static bool CanInteract(EntityAgent entity)
		{
			var isSneaking    = entity.Controls.Sneak;
			var isEmptyHanded = entity.RightHandItemSlot.Empty && entity.LeftHandItemSlot.Empty;
			return (isSneaking && isEmptyHanded);
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
			
			return player.PlaceCarried(selection, carried.Slot);
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

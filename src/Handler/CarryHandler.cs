using CarryCapacity.Network;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace CarryCapacity.Handler
{
	/// <summary>
	///   Takes care of core CarryCapacity handling, such
	///   as listening to events, picking up and placing
	///   blocks, as well as sending and handling messages.
	/// </summary>
	public class CarryHandler
	{
		private CarryCapacityMod Mod { get; }
		
		public CarryHandler(CarryCapacityMod mod)
			=> Mod = mod;
		
		public void InitClient()
			=> Mod.MOUSE_HANDLER.OnRightMousePressed += OnRightClick;
		
		public void InitServer()
		{
			Mod.SERVER_CHANNEL
				.SetMessageHandler<PickUpMessage>(OnPickUpMessage)
				.SetMessageHandler<PlaceDownMessage>(OnPlaceDownMessage);
		}
		
		
		public void OnRightClick()
		{
			var player = Mod.CLIENT_API.World.Player;
			// TODO: Don't run any of this while in a GUI.
			// TODO: Only allow close blocks to be picked up.
			
			var isSneaking    = player.Entity.Controls.Sneak;
			var isEmptyHanded = player.Entity.RightHandItemSlot.Empty;
			// Only pick up or place down if sneaking and empty handed.
			if (!isSneaking || !isEmptyHanded) return;
			
			var selection = player.CurrentBlockSelection;
			if (selection == null) return; // Not pointing at any block.
			
			var carried = player.Entity.GetCarried();
			if (carried == null) {
				// If not currently carrying a block, see if we can pick one up.
				if (player.Entity.Carry(selection.Position))
					Mod.CLIENT_CHANNEL.SendPacket(new PickUpMessage(selection.Position));
			} else {
				// If already carrying a block, see if we can place it down.
				if (PlaceDown(player, carried, selection))
					Mod.CLIENT_CHANNEL.SendPacket(new PlaceDownMessage(selection));
			}
		}
		
		public static void OnPickUpMessage(IPlayer player, PickUpMessage message)
		{
			// FIXME: Do at least some validation of this data.
			
			var isSneaking    = player.Entity.Controls.Sneak;
			var isEmptyHanded = player.Entity.RightHandItemSlot.Empty;
			var carried       = player.Entity.GetCarried();
			
			if (!isSneaking || !isEmptyHanded || (carried != null)
				|| !player.Entity.Carry(message.Position))
					InvalidCarry(player, message.Position);
		}
		
		public static void OnPlaceDownMessage(IPlayer player, PlaceDownMessage message)
		{
			// FIXME: Do at least some validation of this data.
			
			var isSneaking    = player.Entity.Controls.Sneak;
			var isEmptyHanded = player.Entity.RightHandItemSlot.Empty;
			var carried       = player.Entity.GetCarried();
			
			if (!isSneaking || !isEmptyHanded || (carried == null)
				|| !PlaceDown(player, carried, message.Selection))
					InvalidCarry(player, message.Selection.Position);
		}
		
		public static bool PlaceDown(IPlayer player, CarriedBlock carried,
		                             BlockSelection selection)
		{
			// Clone the selection, because we don't
			// want to affect what is sent to the server.
			selection = selection.Clone();
			
			var blocks = player.Entity.World.BlockAccessor;
			var clickedBlock = blocks.GetBlock(selection.Position);
			// If clicked block is replacable, check block below instead.
			if (clickedBlock.IsReplacableBy(carried.Block)) {
				selection.Face = BlockFacing.UP;
				clickedBlock   = blocks.GetBlock(selection.Position.DownCopy());
			// Otherwise make sure that the block was clicked on the top side.
			} else if (selection.Face == BlockFacing.UP) {
				selection.Position.Up();
				selection.DidOffset = true;
			} else return false;
			
			// And also that the clicked block is solid on top.
			if (!clickedBlock.SideSolid[BlockFacing.UP.Index]) return false;
			
			return player.PlaceCarried(selection);
		}
		
		
		/// <summary> Called when a player picks up or places down an invalid block,
		///           requiring it to get notified about the action being rejected. </summary>
		private static void InvalidCarry(IPlayer player, BlockPos pos)
		{
			// FIXME: This is problematic :(
			// https://gist.github.com/copygirl/a29cfbdb49ed25fcf7e1afdf6b3a4018
			//player.Entity.World.BlockAccessor.MarkBlockDirty(pos);
			//player.Entity.WatchedAttributes.MarkPathDirty(CarriedBlock.ATTRIBUTE_ID);
		}
	}
}

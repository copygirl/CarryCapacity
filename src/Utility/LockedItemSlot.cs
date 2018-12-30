using System;
using System.Linq;
using Vintagestory.API.Common;

namespace CarryCapacity.Utility
{
	public class LockedItemSlot : ItemSlot
	{
		public ItemSlot Original { get; }
		public int SlotID { get; }
		
		public LockedItemSlot(ItemSlot original)
			: base(original.Inventory)
		{
			if (original == null) throw new ArgumentNullException(nameof(original));
			
			Original       = original;
			Itemstack      = original.Itemstack;
			BackgroundIcon = original.BackgroundIcon;
			StorageType    = default(EnumItemStorageFlags);
			
			SlotID = -1;
			for (var i = 0; i < original.Inventory.Count; i++)
				if (original.Inventory[i] == original) { SlotID = i; break; }
			if (SlotID == -1) throw new Exception("Couldn't find original slot in its own inventory!");
		}
		
		public static LockedItemSlot Lock(ItemSlot slot)
		{
			if (!(slot is LockedItemSlot locked)) {
				locked = new LockedItemSlot(slot);
				slot.Inventory[locked.SlotID] = locked;
			}
			return locked;
		}
		
		public static void Restore(ItemSlot slot)
		{
			if (slot is LockedItemSlot locked)
				slot.Inventory[locked.SlotID] = locked.Original;
		}
		
		
		// These should be the only methods we have to override.
		
		public override bool CanHold(IItemSlot sourceSlot) => false;
		public override bool CanTake() => false;
		
		// Unfortunately, only some of ItemSlot's method make use of them.
		
		public override bool CanTakeFrom(IItemSlot sourceSlot) => false;
		public override ItemStack TakeOutWhole() => null;
		public override ItemStack TakeOut(int quantity) => null;
		protected override void ActivateSlotLeftClick(ItemSlot sourceSlot, ref ItemStackMoveOperation op) {  }
		protected override void ActivateSlotRightClick(ItemSlot sourceSlot, ref ItemStackMoveOperation op) {  }
	}
}

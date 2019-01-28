using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace CarryCapacity.Common
{
  /// <summary> Handles upgrading old attributes and other
  ///           data to newer versions of CarryCapacity. </summary>
  public class BackwardCompatHandler
	{
		public BackwardCompatHandler(ICoreServerAPI api)
			=> api.Event.OnEntitySpawn += OnEntitySpawn;
		
		private void OnEntitySpawn(Entity entity)
		{
			UpgradeFrom034(entity);
			UpgradeFrom032(entity);
		}
		
		private void UpgradeFrom034(Entity entity)
		{
			var oldStackAttributeID = $"{ CarrySystem.MOD_ID }:CarriedBlock/Stack";
			var oldDataAttributeID = $"{ CarrySystem.MOD_ID }:CarriedBlock/Data";
			
			var oldItemStack = entity.WatchedAttributes.GetItemstack(oldStackAttributeID);
			if (oldItemStack == null) return;
			oldItemStack.ResolveBlockOrItem(entity.World);
			var oldBlockEntityData = entity.Attributes.GetTreeAttribute(oldDataAttributeID);
			
			entity.WatchedAttributes.RemoveAttribute(oldStackAttributeID);
			entity.Attributes.RemoveAttribute(oldDataAttributeID);
			
			CarriedBlock.Set(entity, CarrySlot.Back, oldItemStack, oldBlockEntityData);
		}
		
		private void UpgradeFrom032(Entity entity)
		{
			var oldAttributeID = $"{ CarrySystem.MOD_ID }:CarriedBlock";
			
			var oldBlockCode = entity.WatchedAttributes.GetString(oldAttributeID);
			if (oldBlockCode == null) return;
			var oldBlock = entity.World.GetBlock(new AssetLocation(oldBlockCode));
			if (oldBlock == null) return;
			var oldBlockEntityData = entity.Attributes.GetTreeAttribute(oldAttributeID);
			
			entity.WatchedAttributes.RemoveAttribute(oldAttributeID);
			entity.Attributes.RemoveAttribute(oldAttributeID);
			
			CarriedBlock.Set(entity, CarrySlot.Back, new ItemStack(oldBlock), oldBlockEntityData);
		}
	}
}

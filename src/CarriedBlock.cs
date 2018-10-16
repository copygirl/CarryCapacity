using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CarryCapacity
{
	/// <summary>
	///   Represents a block that has been picked up from
	///   the world as is being carried around by an entity.
	///   
	///   While being carried the data is stored as such:
	///   <see cref="Block"/> is stored in <see cref="IEntity.WatchedAttributes"/>, meaning it will get syncronized to other players.
	///   <see cref="BlockEntityData"/> is stored in <see cref="IEntity.Attributes"/>, and only server-side.
	/// </summary>
	public class CarriedBlock
	{
		/// <summary> Root tree attribute on an entity which stores carried data. </summary>
		public static string ATTRIBUTE_ID { get; }
			= $"{ CarrySystem.MOD_ID }:Carried";
		
		
		public CarrySlot Slot { get; }
		
		public ItemStack ItemStack { get; }
		public Block Block => ItemStack.Block;
		public BlockBehaviorCarryable Behavior
			=> Block.GetBehaviorOrDefault(BlockBehaviorCarryable.DEFAULT);
		
		public ITreeAttribute BlockEntityData { get; }
		
		public CarriedBlock(CarrySlot slot, ItemStack stack, ITreeAttribute blockEntityData)
		{
			if (stack == null) throw new ArgumentNullException(nameof(stack));
			Slot            = slot;
			ItemStack       = stack;
			BlockEntityData = blockEntityData;
		}
		
		
		/// <summary> Gets the <see cref="CarriedBlock"/> currently
		///           carried by the specified entity, or null if none. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity is null. </exception>
		public static CarriedBlock Get(IEntity entity, CarrySlot slot)
		{
			if (entity == null) throw new ArgumentNullException(nameof(entity));
			
			var attribute = entity.WatchedAttributes.GetTreeAttribute(ATTRIBUTE_ID);
			if (attribute == null) return null;
			
			var slotAttribute = attribute.GetTreeAttribute(slot.ToString());
			if (slotAttribute == null) return null;
			
			var stack = slotAttribute.GetItemstack("Stack");
			if (stack?.Class != EnumItemClass.Block) return null;
			// The ItemStack returned by TreeAttribute.GetItemstack
			// may not have Block set, so we have to resolve it.
			if (stack.Block == null) {
				stack.ResolveBlockOrItem(entity.World);
				if (stack.Block == null) return null; // Can't resolve block?
			}
			
			var blockEntityData = (entity.World.Side == EnumAppSide.Server)
				? entity.Attributes.GetTreeAttribute(ATTRIBUTE_ID)
					?.GetTreeAttribute(slot.ToString())?.GetTreeAttribute("Data") : null;
			
			return new CarriedBlock(slot, stack, blockEntityData);
		}
		
		/// <summary> Stores the specified stack and blockEntityData (may be null)
		///           as the <see cref="CarriedBlock"/> of the entity in that slot. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity is null. </exception>
		public static void Set(IEntity entity, CarrySlot slot, ItemStack stack, ITreeAttribute blockEntityData)
		{
			if (entity == null) throw new ArgumentNullException(nameof(entity));
			
			entity.WatchedAttributes
				.GetOrAddTreeAttribute(ATTRIBUTE_ID)
				.GetOrAddTreeAttribute(slot.ToString())
				.SetItemstack("Stack", stack);
			
			if ((entity.World.Side == EnumAppSide.Server) && (blockEntityData != null))
				entity.Attributes
					.GetOrAddTreeAttribute(ATTRIBUTE_ID)
					.GetOrAddTreeAttribute(slot.ToString())
					["Data"] = blockEntityData;
			
			var behavior     = stack.Block.GetBehaviorOrDefault(BlockBehaviorCarryable.DEFAULT);
			var slotSettings = behavior.Slots[slot];
			
			if (entity is IEntityAgent agent) {
				var speed = slotSettings?.WalkSpeedModifier ?? 1.0F;
				if (speed != 1.0F) agent.SetWalkSpeedModifier($"{ CarrySystem.MOD_ID }:{ slot }", speed, false);
			}
			
			if (slotSettings?.Animation != null)
				entity.StartAnimation(slotSettings.Animation);
		}
		
		/// <summary> Stores this <see cref="CarriedBlock"/> as the
		///           specified entity's carried block in that slot. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity is null. </exception>
		public void Set(IEntity entity, CarrySlot slot)
			=> Set(entity, slot, ItemStack, BlockEntityData);
		
		/// <summary> Removes the <see cref="CarriedBlock"/>
		///           carried by the specified entity in that slot. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity is null. </exception>
		public static void Remove(IEntity entity, CarrySlot slot)
		{
			if (entity == null) throw new ArgumentNullException(nameof(entity));
			
			if (entity is IEntityAgent agent)
				agent.RemoveWalkSpeedModifier($"{ CarrySystem.MOD_ID }:{ slot }");
			
			var animation = entity.GetCarried(slot)?.Behavior?.Slots?[slot]?.Animation;
			if (animation != null) entity.StopAnimation(animation);
			
			entity.WatchedAttributes.GetTreeAttribute(ATTRIBUTE_ID)?.RemoveAttribute(slot.ToString());
			entity.Attributes.GetTreeAttribute(ATTRIBUTE_ID)?.RemoveAttribute(slot.ToString());
		}
		
		
		/// <summary> Creates a <see cref="CarriedBlock"/> from the specified world
		///           and position, but doesn't remove it. Returns null if unsuccessful. </summary>
		/// <example cref="ArgumentNullException"> Thrown if world or pos is null. </exception>
		public static CarriedBlock Get(IWorldAccessor world, BlockPos pos, CarrySlot slot)
		{
			if (world == null) throw new ArgumentNullException(nameof(world));
			if (pos == null) throw new ArgumentNullException(nameof(pos));
			
			var block = world.BlockAccessor.GetBlock(pos);
			if (block.Id == 0) return null; // Can't pick up air.
			var stack = block.OnPickBlock(world, pos) ?? new ItemStack(block);
			
			ITreeAttribute blockEntityData = null;
			if (world.Side == EnumAppSide.Server) {
				var blockEntity = world.BlockAccessor.GetBlockEntity(pos);
				if (blockEntity != null) {
					blockEntityData = new TreeAttribute();
					blockEntity.ToTreeAttributes(blockEntityData);
					blockEntityData = blockEntityData.Clone();
					// We don't need to keep the position.
					blockEntityData.RemoveAttribute("posx");
					blockEntityData.RemoveAttribute("posy");
					blockEntityData.RemoveAttribute("posz");
				}
			}
			
			return new CarriedBlock(slot, stack, blockEntityData);
		}
		
		/// <summary> Attempts to pick up a <see cref="CarriedBlock"/> from the specified
		///           world and position, removing it. Returns null if unsuccessful. </summary>
		/// <example cref="ArgumentNullException"> Thrown if world or pos is null. </exception>
		public static CarriedBlock PickUp(IWorldAccessor world, BlockPos pos,
		                                  CarrySlot slot, bool checkIsCarryable = false)
		{
			var carried = Get(world, pos, slot);
			if (carried == null) return null;
			
			if (checkIsCarryable && !carried.Block.IsCarryable(slot)) return null;
			
			world.BlockAccessor.RemoveBlockEntity(pos);
			world.BlockAccessor.SetBlock(0, pos);
			return carried;
		}
		
		/// <summary> Attempts to place down a <see cref="CarriedBlock"/> at the specified world,
		///           selection and by the entity (if any), returning whether it was successful. </summary>
		/// <example cref="ArgumentNullException"> Thrown if world or pos is null. </exception>
		public bool PlaceDown(IWorldAccessor world, BlockSelection selection, IEntity entity = null)
		{
			if (world == null) throw new ArgumentNullException(nameof(world));
			if (selection == null) throw new ArgumentNullException(nameof(selection));
			if (!world.BlockAccessor.IsValidPos(selection.Position)) return false;
			
			if (entity is IEntityPlayer playerEntity) {
				var player = world.PlayerByUid(playerEntity.PlayerUID);
				if (!Block.TryPlaceBlock(world, player, ItemStack, selection)) return false;
			} else {
				world.BlockAccessor.SetBlock((ushort)Block.Id, selection.Position);
				// TODO: Handle type attribute.
			}
			
			RestoreBlockEntityData(world, selection.Position);
			PlaySound(selection.Position, entity);
			if (entity != null) Remove(entity, Slot);
			
			return true;
		}
		
		
		/// <summary>
		///   Restores the <see cref="BlockEntityData"/> to the
		///   block entity at the specified world and position.
		///   
		///   Does nothing if executed on client side,
		///   <see cref="BlockEntityData"/> is null, or there's
		///   no entity at the specified location.
		/// </summary>
		public void RestoreBlockEntityData(IWorldAccessor world, BlockPos pos)
		{
			if ((world.Side != EnumAppSide.Server)
				|| (BlockEntityData == null)) return;
			
			// Set the block entity's position to the new position.
			// Without this, we get some funny behavior.
			BlockEntityData.SetInt("posx", pos.X);
			BlockEntityData.SetInt("posy", pos.Y);
			BlockEntityData.SetInt("posz", pos.Z);
			
			var blockEntity = world.BlockAccessor.GetBlockEntity(pos);
			blockEntity?.FromTreeAtributes(BlockEntityData, world);
		}
		
		
		internal void PlaySound(BlockPos pos, IEntity entity = null)
		{
			const float SOUND_RANGE  = 16.0F;
			const float SOUND_VOLUME = 0.8F;
			
			// TODO: In 1.7.0, Block.Sounds should not be null anymore.
			if (Block.Sounds?.Place == null) return;
			
			var player = (entity.World.Side == EnumAppSide.Server)
					&& (entity is IEntityPlayer entityPlayer)
				? entity.World.PlayerByUid(entityPlayer.PlayerUID)
				: null;
			
			entity.World.PlaySoundAt(Block.Sounds.Place,
				pos.X + 0.5, pos.Y + 0.25, pos.Z + 0.5, player,
				range: SOUND_RANGE, volume: SOUND_VOLUME);
		}
	}
	
	public static class CarriedBlockExtensions
	{
		/// <summary> Returns whether the specified block can be carried in the specified slot.
		///           Checks if <see cref="BlockBehaviorCarryable"/> is present and has slot enabled. </summary>
		public static bool IsCarryable(this Block block, CarrySlot slot)
			=> (block.GetBehavior<BlockBehaviorCarryable>()?.Slots?[slot] != null);
		
		
		/// <summary> Returns the <see cref="CarriedBlock"/> this entity
		///           is carrying in the specified slot, or null of none. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity or pos is null. </exception>
		public static CarriedBlock GetCarried(this IEntity entity, CarrySlot slot)
			=> CarriedBlock.Get(entity, slot);
		
		/// <summary> Returns all the <see cref="CarriedBlock"/>s this entity is carrying. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity or pos is null. </exception>
		public static IEnumerable<CarriedBlock> GetCarried(this IEntity entity)
		{
			foreach (var slot in Enum.GetValues(typeof(CarrySlot)).Cast<CarrySlot>()) {
				var carried = entity.GetCarried(slot);
				if (carried != null) yield return carried;
			}
		}
		
		/// <summary>
		///   Attempts to get this entity to pick up the block the
		///   specified position as a <see cref="CarriedBlock"/>,
		///   returning whether it was successful.
		/// </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity or pos is null. </exception>
		public static bool Carry(this IEntity entity, BlockPos pos,
		                         CarrySlot slot, bool checkIsCarryable = true)
		{
			if (CarriedBlock.Get(entity, slot) != null) return false;
			var carried = CarriedBlock.PickUp(entity.World, pos, slot, checkIsCarryable);
			if (carried == null) return false;
			
			carried.Set(entity, slot);
			carried.PlaySound(pos, entity);
			return true;
		}
		
		/// <summary>
		///   Attempts to get this player to place down its
		///   <see cref="CarriedBlock"/> (if any) at the specified
		///   selection, returning whether it was successful.
		/// </summary>
		/// <example cref="ArgumentNullException"> Thrown if player or selection is null. </exception>
		public static bool PlaceCarried(this IPlayer player, BlockSelection selection, CarrySlot slot)
		{
			if (player == null) throw new ArgumentNullException(nameof(player));
			if (selection == null) throw new ArgumentNullException(nameof(selection));
			
			var carried = CarriedBlock.Get(player.Entity, slot);
			if (carried == null) return false;
			
			return carried.PlaceDown(player.Entity.World, selection, player.Entity);
		}
		
		/// <summary> Attempts to make this entity drop its <see cref="CarriedBlock"/>
		///           (if any) at the specified position, returning whether it was successful. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity or pos is null. </exception>
		public static bool DropCarried(this IEntity entity, BlockPos pos, CarrySlot slot)
		{
			if (pos == null) throw new ArgumentNullException(nameof(pos));
			
			var carried = CarriedBlock.Get(entity, slot);
			if (carried == null) return false;
			
			var selection = new BlockSelection {
				Position    = pos,
				Face        = BlockFacing.UP,
				HitPosition = new Vec3d(0.5, 0.5, 0.5),
			};
			
			return carried.PlaceDown(entity.World, selection, entity);
		}
		
		/// <summary>
		///   Attempts to swap the <see cref="CarriedBlock"/> currently carried
		///   in the entity's Hands slot with the one that's in its Back slot.
		/// </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity is null. </exception>
		public static bool SwapCarriedHandsWithBack(this IEntity entity)
		{
			var carriedHands = CarriedBlock.Get(entity, CarrySlot.Hands);
			var carriedBack  = CarriedBlock.Get(entity, CarrySlot.Back);
			if ((carriedHands == null) && (carriedBack == null)) return false;
			
			CarriedBlock.Remove(entity, CarrySlot.Hands);
			CarriedBlock.Remove(entity, CarrySlot.Back);
			
			if (carriedHands != null) carriedHands.Set(entity, CarrySlot.Back);
			if (carriedBack != null) carriedBack.Set(entity, CarrySlot.Hands);
			
			return true;
		}
	}
}

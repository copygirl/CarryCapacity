using System;
using System.Collections.Generic;
using System.Linq;
using CarryCapacity.Common;
using CarryCapacity.Utility;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CarryCapacity
{
	/// <summary>
	///   Represents a block that has been picked up from
	///   the world as is being carried around by an entity.
	///   
	///   While being carried the data is stored as such:
	///   <see cref="Block"/> is stored in <see cref="Entity.WatchedAttributes"/>, meaning it will get syncronized to other players.
	///   <see cref="BlockEntityData"/> is stored in <see cref="Entity.Attributes"/>, and only server-side.
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
			Slot            = slot;
			ItemStack       = stack ?? throw new ArgumentNullException(nameof(stack));;
			BlockEntityData = blockEntityData;
		}
		
		
		/// <summary> Gets the <see cref="CarriedBlock"/> currently
		///           carried by the specified entity, or null if none. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity is null. </exception>
		public static CarriedBlock Get(Entity entity, CarrySlot slot)
		{
			if (entity == null) throw new ArgumentNullException(nameof(entity));
			
			var slotAttribute = entity.WatchedAttributes
				.TryGet<ITreeAttribute>(ATTRIBUTE_ID, slot.ToString());
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
				? entity.Attributes.TryGet<ITreeAttribute>(ATTRIBUTE_ID, slot.ToString(), "Data")
				: null;
			
			return new CarriedBlock(slot, stack, blockEntityData);
		}
		
		/// <summary> Stores the specified stack and blockEntityData (may be null)
		///           as the <see cref="CarriedBlock"/> of the entity in that slot. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity is null. </exception>
		public static void Set(Entity entity, CarrySlot slot, ItemStack stack, ITreeAttribute blockEntityData)
		{
			if (entity == null) throw new ArgumentNullException(nameof(entity));
			
			entity.WatchedAttributes.Set(stack, ATTRIBUTE_ID, slot.ToString(), "Stack");
			((SyncedTreeAttribute)entity.WatchedAttributes).MarkPathDirty(ATTRIBUTE_ID);
			
			if ((entity.World.Side == EnumAppSide.Server) && (blockEntityData != null))
				entity.Attributes.Set(blockEntityData, ATTRIBUTE_ID, slot.ToString(), "Data");
			
			var behavior     = stack.Block.GetBehaviorOrDefault(BlockBehaviorCarryable.DEFAULT);
			var slotSettings = behavior.Slots[slot];
			
			if (slotSettings?.Animation != null)
				entity.StartAnimation(slotSettings.Animation);
			
			if (entity is EntityAgent agent) {
				var speed = slotSettings?.WalkSpeedModifier ?? 0.0F;
				if (speed != 0.0F) agent.Stats.Set("walkspeed",
					$"{ CarrySystem.MOD_ID }:{ slot }", speed, false);
				
				if (slot == CarrySlot.Hands) LockedItemSlot.Lock(agent.RightHandItemSlot);
				if (slot != CarrySlot.Back ) LockedItemSlot.Lock(agent.LeftHandItemSlot);
			}
		}
		
		/// <summary> Stores this <see cref="CarriedBlock"/> as the
		///           specified entity's carried block in that slot. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity is null. </exception>
		public void Set(Entity entity, CarrySlot slot)
			=> Set(entity, slot, ItemStack, BlockEntityData);
		
		/// <summary> Removes the <see cref="CarriedBlock"/>
		///           carried by the specified entity in that slot. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity is null. </exception>
		public static void Remove(Entity entity, CarrySlot slot)
		{
			if (entity == null) throw new ArgumentNullException(nameof(entity));
			
			var animation = entity.GetCarried(slot)?.Behavior?.Slots?[slot]?.Animation;
			if (animation != null) entity.StopAnimation(animation);
			
			if (entity is EntityAgent agent) {
				agent.Stats.Remove("walkspeed", $"{ CarrySystem.MOD_ID }:{ slot }");
				
				if (slot == CarrySlot.Hands) LockedItemSlot.Restore(agent.RightHandItemSlot);
				if (slot != CarrySlot.Back ) LockedItemSlot.Restore(agent.LeftHandItemSlot);
			}
			
			entity.WatchedAttributes.Remove(ATTRIBUTE_ID, slot.ToString());
			((SyncedTreeAttribute)entity.WatchedAttributes).MarkPathDirty(ATTRIBUTE_ID);
			entity.Attributes.Remove(ATTRIBUTE_ID, slot.ToString());
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
					// And angle needs to be removed, or else it will
					// override the angle set from block placement.
					blockEntityData.RemoveAttribute("meshAngle");
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
			
			world.BlockAccessor.SetBlock(0, pos);
			world.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>()?.ClearReinforcement(pos);
			return carried;
		}
		
		/// <summary> Attempts to place down a <see cref="CarriedBlock"/> at the specified world,
		///           selection and by the entity (if any), returning whether it was successful. </summary>
		/// <example cref="ArgumentNullException"> Thrown if world or pos is null. </exception>
		public bool PlaceDown(IWorldAccessor world, BlockSelection selection, Entity entity = null)
		{
			if (world == null) throw new ArgumentNullException(nameof(world));
			if (selection == null) throw new ArgumentNullException(nameof(selection));
			if (!world.BlockAccessor.IsValidPos(selection.Position)) return false;
			
			if (entity is EntityPlayer playerEntity) {
				var player = world.PlayerByUid(playerEntity.PlayerUID);
				var failureCode = "__ignore__";
				if (!Block.TryPlaceBlock(world, player, ItemStack, selection, ref failureCode)) return false;
			} else {
				world.BlockAccessor.SetBlock(Block.Id, selection.Position);
				// TODO: Handle type attribute.
			}
			
			RestoreBlockEntityData(world, selection.Position);
			if (entity != null) Remove(entity, Slot);
			PlaySound(selection.Position, world, (entity as EntityPlayer));
			
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
			blockEntity?.FromTreeAttributes(BlockEntityData, world);
		}
		
		
		internal void PlaySound(BlockPos pos, IWorldAccessor world,
		                        EntityPlayer entityPlayer = null)
		{
			const float SOUND_RANGE  = 16.0F;
			const float SOUND_VOLUME = 0.8F;
			
			// TODO: In 1.7.0, Block.Sounds should not be null anymore.
			if (Block.Sounds?.Place == null) return;
			
			var player = (entityPlayer != null) && (world.Side == EnumAppSide.Server)
				? world.PlayerByUid(entityPlayer.PlayerUID) : null;
			
			world.PlaySoundAt(Block.Sounds.Place,
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
		public static CarriedBlock GetCarried(this Entity entity, CarrySlot slot)
			=> CarriedBlock.Get(entity, slot);
		
		/// <summary> Returns all the <see cref="CarriedBlock"/>s this entity is carrying. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity or pos is null. </exception>
		public static IEnumerable<CarriedBlock> GetCarried(this Entity entity)
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
		public static bool Carry(this Entity entity, BlockPos pos,
		                         CarrySlot slot, bool checkIsCarryable = true)
		{
			if (!HasPermissionToCarry(entity, pos)) return false;
			if (CarriedBlock.Get(entity, slot) != null) return false;
			var carried = CarriedBlock.PickUp(entity.World, pos, slot, checkIsCarryable);
			if (carried == null) return false;
			
			carried.Set(entity, slot);
			carried.PlaySound(pos, entity.World, (entity as EntityPlayer));
			return true;
		}
		
		private static bool HasPermissionToCarry(Entity entity, BlockPos pos)
		{
			var isReinforced = entity.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>()?.IsReinforced(pos) ?? false;
			if (entity is EntityPlayer playerEntity) {
				var isCreative = (playerEntity.Player.WorldData.CurrentGameMode == EnumGameMode.Creative);
				if (!isCreative && isReinforced) return false; // Can't pick up when reinforced unless in creative mode.
				var hasClaimsAccess = entity.World.Claims.TryAccess(playerEntity.Player, pos, EnumBlockAccessFlags.BuildOrBreak);
				return hasClaimsAccess; // Can pick up if has access to any claims that might be present.
			} else return !isReinforced; // If not a player entity, can pick up if not reinforced.
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
			
			if (!player.Entity.World.Claims.TryAccess(
				player, selection.Position, EnumBlockAccessFlags.BuildOrBreak)) return false;
			var carried = CarriedBlock.Get(player.Entity, slot);
			if (carried == null) return false;
			
			return carried.PlaceDown(player.Entity.World, selection, player.Entity);
		}
		
		/// <summary> Attempts to make this entity drop its carried blocks from the
		///           specified slots around its current position in the specified area. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity or slots is null. </exception>
		/// <example cref="ArgumentOutOfRangeException"> Thrown if hSize or vSize is negative. </exception>
		public static void DropCarried(this Entity entity, IEnumerable<CarrySlot> slots,
		                               int hSize = 2, int vSize = 4)
		{
			if (entity == null) throw new ArgumentNullException(nameof(entity));
			if (slots == null) throw new ArgumentNullException(nameof(slots));
			if (hSize < 0) throw new ArgumentOutOfRangeException(nameof(hSize));
			if (vSize < 0) throw new ArgumentOutOfRangeException(nameof(vSize));
			
			var remaining = new HashSet<CarriedBlock>(
				slots.Select(s => entity.GetCarried(s))
				     .Where(c => (c != null)));
			if (remaining.Count == 0) return;
			
			bool Drop(BlockPos pos, CarriedBlock block)
			{
				if (!block.PlaceDown(entity.World, new BlockSelection { Position = pos }, null)) return false;
				CarriedBlock.Remove(entity, block.Slot);
				return true;
			}
			
			var centerBlock  = entity.Pos.AsBlockPos;
			var nearbyBlocks = new List<BlockPos>((hSize * 2 + 1) * (hSize * 2 + 1));
			for (int x = -hSize; x <= hSize; x++)
				for (int z = -hSize; z <= hSize; z++)
					nearbyBlocks.Add(centerBlock.AddCopy(x, 0, z));
			nearbyBlocks = nearbyBlocks.OrderBy(b => b.DistanceTo(centerBlock)).ToList();
			
			var accessor    = entity.World.BlockAccessor;
			var blockIndex  = 0;
			var distance    = 0;
			while (remaining.Count > 0) {
				var pos = nearbyBlocks[blockIndex];
				if (Math.Abs(pos.Y - centerBlock.Y) <= vSize) {
					var sign = Math.Sign(pos.Y - centerBlock.Y);
					var testBlock   = accessor.GetBlock(pos);
					var placeable   = remaining.FirstOrDefault(c => testBlock.IsReplacableBy(c.Block));
					if (sign == 0) {
						sign = ((placeable != null) ? -1 : 1);
					} else if (sign > 0) {
						if ((placeable != null) && Drop(pos, placeable))
							remaining.Remove(placeable);
					} else if ((placeable == null)) {
						var above = pos.UpCopy();
						testBlock = accessor.GetBlock(above);
						placeable = remaining.FirstOrDefault(c => testBlock.IsReplacableBy(c.Block));
						if ((placeable != null) && Drop(above, placeable))
							remaining.Remove(placeable);
					}
					pos.Add(0, sign, 0);
				} else if (blockIndex >= nearbyBlocks.Count) break;
				
				if (++distance > 2) {
					distance = 0;
					blockIndex++;
					if (blockIndex % 4 == 4)
					if (++blockIndex >= nearbyBlocks.Count)
						blockIndex = 0;
				}
			}
			
			// FIXME: Drop container contents if blocks could not be placed.
			//        Right now, the player just gets to keep them equipped.
		}
		
		/// <summary> Attempts to make this entity drop all of its carried
		///           blocks around its current position in the specified area. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity is null. </exception>
		/// <example cref="ArgumentOutOfRangeException"> Thrown if hSize or vSize is negative. </exception>
		public static void DropAllCarried(this Entity entity, int hSize = 2, int vSize = 4)
			=> DropCarried(entity, Enum.GetValues(typeof(CarrySlot)).Cast<CarrySlot>(), hSize, vSize);
		
		/// <summary>
		///   Attempts to swap the <see cref="CarriedBlock"/>s currently carried in the
		///   entity's <paramref name="first"/> and <paramref name="second"/> slots.
		/// </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity is null. </exception>
		public static bool Swap(this Entity entity, CarrySlot first, CarrySlot second)
		{
			if (first == second) throw new ArgumentException("Slots can't be the same");
			
			var carriedFirst  = CarriedBlock.Get(entity, first);
			var carriedSecond = CarriedBlock.Get(entity, second);
			if ((carriedFirst == null) && (carriedSecond == null)) return false;
			
			CarriedBlock.Remove(entity, first);
			CarriedBlock.Remove(entity, second);
			
			if (carriedFirst != null) carriedFirst.Set(entity, second);
			if (carriedSecond != null) carriedSecond.Set(entity, first);
			
			return true;
		}
	}
}

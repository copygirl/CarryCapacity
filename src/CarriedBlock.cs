using System;
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
	///   While being carried the data is stored as such: <see cref="Block"/>
	///   is stored in <see cref="IEntity.WatchedAttributes"/>, meaning it
	///   will get syncronized to other players. <see cref="BlockEntityData"/>
	///   is stored in <see cref="IEntity.Attributes"/>, and only server-side.
	/// </summary>
	public class CarriedBlock
	{
		public static string ATTRIBUTE_ID_STACK { get; } =
			$"{ CarrySystem.MOD_ID }:CarriedBlock/Stack";
		public static string ATTRIBUTE_ID_DATA { get; } =
			$"{ CarrySystem.MOD_ID }:CarriedBlock/Data";
		
		/// <summary> Before version 0.3.2, attribute held only a block ID. </summary>
		public static string ATTRIBUTE_ID_OLD { get; } =
			$"{ CarrySystem.MOD_ID }:CarriedBlock";
		
		
		public ItemStack ItemStack { get; }
		public Block Block => ItemStack.Block;
		
		public ITreeAttribute BlockEntityData { get; }
		
		public CarriedBlock(ItemStack stack, ITreeAttribute blockEntityData)
		{
			if (stack == null) throw new ArgumentNullException(nameof(stack));
			ItemStack = stack;
			BlockEntityData = blockEntityData;
		}
		
		
		/// <summary> Gets the <see cref="CarriedBlock"/> currently
		///           carried by the specified entity, or null if none. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity is null. </exception>
		public static CarriedBlock Get(IEntity entity)
		{
			if (entity == null) throw new ArgumentNullException(nameof(entity));
			
			var stack = entity.WatchedAttributes.GetItemstack(ATTRIBUTE_ID_STACK);
			ITreeAttribute blockEntityData;
			if (stack != null) {
				// The ItemStack returned by TreeAttribute.GetItemstack
				// does not have Block set, so we have to resolve it.
				stack.ResolveBlockOrItem(entity.World);
				blockEntityData = (entity.World.Side == EnumAppSide.Server)
					? entity.Attributes.GetTreeAttribute(ATTRIBUTE_ID_DATA) : null;
			} else {
				// Code to handle versions before 0.3.2.
				var blockCode = entity.WatchedAttributes.GetString(ATTRIBUTE_ID_OLD);
				if (blockCode == null) return null;
				var block = entity.World.GetBlock(new AssetLocation(blockCode));
				if (block == null) return null;
				
				stack = new ItemStack(block);
				blockEntityData = (entity.World.Side == EnumAppSide.Server)
					? entity.Attributes.GetTreeAttribute(ATTRIBUTE_ID_OLD) : null;
			}
			
			return new CarriedBlock(stack, blockEntityData);
		}
		
		/// <summary> Stores this <see cref="CarriedBlock"/>
		///           as the specified entity's carried block. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity is null. </exception>
		public void Set(IEntity entity)
		{
			if (entity == null) throw new ArgumentNullException(nameof(entity));
			
			entity.WatchedAttributes.SetItemstack(ATTRIBUTE_ID_STACK, ItemStack);
			
			if ((entity.World.Side == EnumAppSide.Server)
				&& (BlockEntityData != null))
				entity.Attributes[ATTRIBUTE_ID_DATA] = BlockEntityData;
		}
		
		/// <summary> Removes any <see cref="CarriedBlock"/>
		///           carried by the specified entity. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity is null. </exception>
		public static void Remove(IEntity entity)
		{
			if (entity == null) throw new ArgumentNullException(nameof(entity));
			
			entity.WatchedAttributes.RemoveAttribute(ATTRIBUTE_ID_STACK);
			entity.Attributes.RemoveAttribute(ATTRIBUTE_ID_DATA);
			
			// Code to handle versions before 0.3.2.
			entity.WatchedAttributes.RemoveAttribute(ATTRIBUTE_ID_OLD);
			entity.Attributes.RemoveAttribute(ATTRIBUTE_ID_OLD);
		}
		
		
		/// <summary>
		///   Creates a <see cref="CarriedBlock"/> from the specified world
		///   and position, but doesn't remove it. Returns null if unsuccessful.
		/// </summary>
		/// <example cref="ArgumentNullException"> Thrown if world or pos is null. </exception>
		public static CarriedBlock Get(IWorldAccessor world, BlockPos pos)
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
			
			return new CarriedBlock(stack, blockEntityData);
		}
		
		/// <summary>
		///   Attempts to pick up a <see cref="CarriedBlock"/> from the specified
		///   world and position, removing it. Returns null if unsuccessful.
		/// </summary>
		/// <example cref="ArgumentNullException"> Thrown if world or pos is null. </exception>
		public static CarriedBlock PickUp(IWorldAccessor world, BlockPos pos,
		                                  bool checkIsCarryable = false)
		{
			var carried = Get(world, pos);
			if (carried == null) return null;
			
			if (checkIsCarryable && !carried.Block.IsCarryable()) return null;
			
			world.BlockAccessor.RemoveBlockEntity(pos);
			world.BlockAccessor.SetBlock(0, pos);
			return carried;
		}
		
		/// <summary>
		///   Attempts to place down a <see cref="CarriedBlock"/> at the specified world,
		///   selection and by the entity (if any), returning whether it was successful.
		/// </summary>
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
			if (entity != null) Remove(entity);
			
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
		/// <summary>
		///   Returns whether the specified block can be carried.
		///   Checks if <see cref="BlockBehaviorCarryable"/> is present.
		/// </summary>
		public static bool IsCarryable(this Block block)
			=> block.HasBehavior<BlockBehaviorCarryable>();
		
		
		/// <summary> Returns the <see cref="CarriedBlock"/>
		///           this entity is carrying, or null of none. </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity or pos is null. </exception>
		public static CarriedBlock GetCarried(this IEntity entity)
			=> CarriedBlock.Get(entity);
		
		/// <summary>
		///   Attempts to get this entity to pick up the block the
		///   specified position as a <see cref="CarriedBlock"/>,
		///   returning whether it was successful.
		/// </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity or pos is null. </exception>
		public static bool Carry(this IEntity entity, BlockPos pos,
		                         bool checkIsCarryable = true)
		{
			if (CarriedBlock.Get(entity) != null) return false;
			var carried = CarriedBlock.PickUp(entity.World, pos, checkIsCarryable);
			if (carried == null) return false;
			
			carried.Set(entity);
			carried.PlaySound(pos, entity);
			return true;
		}
		
		/// <summary>
		///   Attempts to get this player to place down its
		///   <see cref="CarriedBlock"/> (if any) at the specified
		///   selection, returning whether it was successful.
		/// </summary>
		/// <example cref="ArgumentNullException"> Thrown if player or selection is null. </exception>
		public static bool PlaceCarried(this IPlayer player, BlockSelection selection)
		{
			if (player == null) throw new ArgumentNullException(nameof(player));
			if (selection == null) throw new ArgumentNullException(nameof(selection));
			
			var carried = CarriedBlock.Get(player.Entity);
			if (carried == null) return false;
			
			return carried.PlaceDown(player.Entity.World, selection, player.Entity);
		}
		
		/// <summary>
		///   Attempts to make this entity drop its <see cref="CarriedBlock"/>
		///   (if any) at the specified position, returning whether it was successful.
		/// </summary>
		/// <example cref="ArgumentNullException"> Thrown if entity or pos is null. </exception>
		public static bool DropCarried(this IEntity entity, BlockPos pos)
		{
			if (pos == null) throw new ArgumentNullException(nameof(pos));
			
			var carried = CarriedBlock.Get(entity);
			if (carried == null) return false;
			
			var selection = new BlockSelection {
				Position    = pos,
				Face        = BlockFacing.UP,
				HitPosition = new Vec3d(0.5, 0.5, 0.5),
			};
			
			return carried.PlaceDown(entity.World, selection, entity);
		}
	}
}

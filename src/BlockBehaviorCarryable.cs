using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace CarryCapacity
{
	/// <summary> Block behavior which, when added to a block, will allow
	///           said block to be picked up by players and carried around. </summary>
	public class BlockBehaviorCarryable : BlockBehavior
	{
		public static string NAME { get; } = "Carryable";
		
		public static BlockBehaviorCarryable DEFAULT { get; }
			= new BlockBehaviorCarryable(null);
		
		public static ModelTransform DEFAULT_BLOCK_TRANSFORM
			=> new ModelTransform {
				Translation = new Vec3f(0.0F, 0.0F, 0.0F),
				Rotation    = new Vec3f(0.0F, 0.0F, 0.0F),
				Origin      = new Vec3f(0.5F, 0.5F, 0.5F),
				ScaleXYZ    = new Vec3f(0.5F, 0.5F, 0.5F)
			};
		
		public static readonly IReadOnlyDictionary<CarrySlot, float> DEFAULT_WALKSPEED
			= new Dictionary<CarrySlot, float> {
				{ CarrySlot.Hands, 0.60F }, // Default slowdown while carrying in hands: 40%
				{ CarrySlot.Back , 0.85F }, // Default slowdown while carrying on back:  15%
			};
		
		public static string DEFAULT_HANDS_ANIMATION = $"{ CarrySystem.MOD_ID }:holdheavy";
		
		
		public float InteractDelay { get; private set; } = 0.8F;
		
		public ModelTransform DefaultTransform { get; private set; } = DEFAULT_BLOCK_TRANSFORM;
		
		public SlotStorage Slots { get; private set; } = new SlotStorage();
		
		
		public BlockBehaviorCarryable(Block block)
			: base(block) {  }
		
		
		public override void Initialize(JsonObject properties)
		{
			if (TryGetFloat(properties, "interactDelay", out var d)) InteractDelay = d;
			DefaultTransform = GetTransform(properties, DEFAULT_BLOCK_TRANSFORM);
			Slots.Initialize(properties["slots"]);
		}
		
		public override bool OnBlockInteractStart(
			IWorldAccessor world, IPlayer byPlayer,
			BlockSelection blockSel, ref EnumHandling handling)
		{
			var isSneaking    = byPlayer.Entity.Controls.Sneak;
			var isEmptyHanded = byPlayer.Entity.RightHandItemSlot.Empty;
			// Prevent default action if sneaking and empty handed,
			// as we want to handle block pickup in this case.
			if (isSneaking && isEmptyHanded) {
				handling = EnumHandling.PreventDefault;
				return false;
			} else return true;
		}
		
		
		private static bool TryGetFloat(JsonObject json, string key, out float result)
		{
				result = json[key].AsFloat(float.NaN);
				return !float.IsNaN(result);
		}
		private static bool TryGetVec3f(JsonObject json, string key, out Vec3f result)
		{
			var floats  = json[key].AsFloatArray();
			var success = (floats?.Length == 3);
			result = success ? new Vec3f(floats) : null;
			return success;
		}
		
		private static ModelTransform GetTransform(JsonObject json, ModelTransform baseTransform)
		{
			var trans = baseTransform.Clone();
			if (TryGetVec3f(json, "translation", out var t)) trans.Translation = t;
			if (TryGetVec3f(json, "rotation"   , out var r)) trans.Rotation = r;
			if (TryGetVec3f(json, "origin"     , out var o)) trans.Origin = o;
			// Try to get scale both as a Vec3f and single float - for compatibility reasons.
			if (TryGetVec3f(json, "scale", out var sv)) trans.ScaleXYZ = sv;
			if (TryGetFloat(json, "scale", out var sf)) trans.ScaleXYZ = new Vec3f(sf, sf, sf);
			return trans;
		}
		
		
		public class SlotSettings
		{
			public ModelTransform Transform { get; set; }
			
			public string Animation { get; set; }
			
			public float WalkSpeedModifier { get; set; } = 1.0F;
		}
		
		public class SlotStorage
		{
			private readonly Dictionary<CarrySlot, SlotSettings> _dict
				= new Dictionary<CarrySlot, SlotSettings>();
			
			public SlotSettings this[CarrySlot slot]
				=> _dict.TryGetValue(slot, out var settings) ? settings : null;
			
			public void Initialize(JsonObject properties)
			{
				_dict.Clear();
				_dict.Add(CarrySlot.Hands, new SlotSettings { Animation = DEFAULT_HANDS_ANIMATION });
				
				if (properties?.Exists != true) return;
				foreach (var slot in Enum.GetValues(typeof(CarrySlot)).Cast<CarrySlot>()) {
					var slotProperties = properties[slot.ToString()];
					if (slotProperties == null) continue;
					
					if (!_dict.TryGetValue(slot, out var settings))
						_dict.Add(slot, settings = new SlotSettings());
					
					if (slotProperties.KeyExists("translation") || slotProperties.KeyExists("rotation") ||
							slotProperties.KeyExists("origin") || slotProperties.KeyExists("scale"))
						settings.Transform = GetTransform(slotProperties, DEFAULT_BLOCK_TRANSFORM);
					
					settings.Animation = slotProperties["animation"].AsString(settings.Animation);
					
					if (!BlockBehaviorCarryable.DEFAULT_WALKSPEED.TryGetValue(slot, out var speed)) speed = 1.0F;
					settings.WalkSpeedModifier = slotProperties["walkSpeedModifier"].AsFloat(speed);
				}
			}
		}
	}
}

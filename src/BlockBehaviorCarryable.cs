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
		
		
		public ModelTransform Transform { get; }
			= new ModelTransform {
				Translation = new Vec3f(0.0F, 0.0F, 0.0F),
				Rotation    = new Vec3f(0.0F, 0.0F, 0.0F),
				Origin      = new Vec3f(0.5F, 0.5F, 0.5F),
				ScaleXYZ    = new Vec3f(0.5F, 0.5F, 0.5F)
			};
		
		public float InteractDelay { get; private set; } = 0.8F;
		
		public BlockBehaviorCarryable(Block block)
			: base(block) {  }
		
		
		public override void Initialize(JsonObject properties)
		{
			bool TryGetFloat(string key, out float result) {
				result = properties[key].AsFloat(float.NaN);
				return !float.IsNaN(result);
			}
			bool TryGetVec3f(string key, out Vec3f result) {
				var floats  = properties[key].AsFloatArray();
				var success = (floats?.Length == 3);
				result = success ? new Vec3f(floats) : null;
				return success;
			}
			
			if (TryGetVec3f("translation" , out var t)) Transform.Translation = t;
			if (TryGetVec3f("rotation"    , out var r)) Transform.Rotation = r;
			if (TryGetVec3f("origin"      , out var o)) Transform.Origin = o;
			
			// Try to get scale both as a Vec3f and single float - for compatibility reasons.
			if (TryGetVec3f("scale", out var sv)) Transform.ScaleXYZ = sv;
			if (TryGetFloat("scale", out var sf)) Transform.ScaleXYZ = new Vec3f(sf, sf, sf);
			
			if (TryGetFloat("interactDelay" , out var d)) InteractDelay = d;
		}
		
		public override bool OnPlayerBlockInteract(
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
	}
}

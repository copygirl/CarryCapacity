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
				Scale       = 0.5F
			};
		
		public BlockBehaviorCarryable(Block block)
			: base(block) {  }
		
		
		public override void Initialize(JsonObject properties)
		{
			void TryGetVec3f(ref Vec3f value, JsonObject obj) {
				var floats = obj.AsFloatArray();
				if (floats?.Length == 3) value = new Vec3f(floats);
			}
			TryGetVec3f(ref Transform.Translation , properties["translation"]);
			TryGetVec3f(ref Transform.Rotation    , properties["rotation"]);
			TryGetVec3f(ref Transform.Origin      , properties["origin"]);
			var scale = properties["scale"].AsFloat();
			if (scale > 0) Transform.Scale = scale;
		}
		
		public override bool OnPlayerPlacedBlockInteract(
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

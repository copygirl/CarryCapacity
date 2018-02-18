using Vintagestory.API.Common;

namespace CarryCapacity
{
	public static class Extensions
	{
		public static T GetBehavior<T>(this Block block)
			where T : BlockBehavior
				=> (T)block.GetBehavior(typeof(T));
		
		public static T GetBehaviorOrDefault<T>(this Block block, T @default)
			where T : BlockBehavior
				=> block.GetBehavior<T>() ?? @default;
	}
}

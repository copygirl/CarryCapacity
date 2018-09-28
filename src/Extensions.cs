using Vintagestory.API.Common;

namespace CarryCapacity
{
	public static class Extensions
	{
		public static bool HasBehavior<T>(this Block block)
			where T : BlockBehavior
				=> block.HasBehavior(typeof(T));
		
		public static T GetBehaviorOrDefault<T>(this Block block, T @default)
			where T : BlockBehavior
				=> (T)block.GetBehavior<T>() ?? @default;
		
		public static void Register<T>(this ICoreAPI api)
			where T : BlockBehavior
				=> api.RegisterBlockBehaviorClass(
					(string)typeof(T).GetProperty("NAME").GetValue(null), typeof(T));
	}
}

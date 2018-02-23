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
		
		public static void Register<T>(this ICoreAPI api)
			where T : BlockBehavior
				=> api.RegisterBlockBehaviorClass(
					(string)typeof(T).GetProperty("NAME").GetValue(null), typeof(T));
	}
}

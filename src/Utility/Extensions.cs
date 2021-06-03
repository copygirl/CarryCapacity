using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace CarryCapacity.Utility
{
	public static class Extensions
	{
		public static void Register<T>(this ICoreAPI api)
		{
			var name = (string)typeof(T).GetProperty("NAME").GetValue(null);
			if (typeof(BlockBehavior).IsAssignableFrom(typeof(T)))
				api.RegisterBlockBehaviorClass(name, typeof(T));
			else if (typeof(EntityBehavior).IsAssignableFrom(typeof(T)))
				api.RegisterEntityBehaviorClass(name, typeof(T));
			else throw new ArgumentException("T is not a block or entity behavior", nameof(T));
		}
		
		
		public static bool HasBehavior<T>(this Block block)
			where T : BlockBehavior
				=> block.HasBehavior(typeof(T));
		
		public static T GetBehaviorOrDefault<T>(this Block block, T @default)
			where T : BlockBehavior
				=> (T)block.GetBehavior<T>() ?? @default;
		
		
		public static IAttribute TryGet(this IAttribute attr, params string[] keys)
		{
			foreach (var key in keys) {
				if (attr is not ITreeAttribute tree) return null;
				attr = tree[key];
			}
			return attr;
		}
		
		public static T TryGet<T>(this IAttribute attr, params string[] keys)
				where T : class, IAttribute
			=> TryGet(attr, keys) as T;
		
		
		public static void Set(this IAttribute attr, IAttribute value, params string[] keys)
		{
			if (attr == null) throw new ArgumentNullException(nameof(attr));
			for (var i = 0; i < keys.Length; i++) {
				var key = keys[i];
				if (attr is not ITreeAttribute tree) {
					if ((attr == null) && (value == null)) return; // If removing value, return on missing tree nodes.
					var getter = $"attr{ keys.Take(i).Select(k => $"[\"{ k }\"]") }";
					var type   = attr?.GetType()?.ToString() ?? "null";
					throw new ArgumentException($"{ getter } is { type }, not TreeAttribute.", nameof(attr));
				}
				if (i == keys.Length - 1) {
					if (value != null) tree[key] = value;
					else tree.RemoveAttribute(key);
				}	else attr = tree[key] ?? (tree[key] = new TreeAttribute());
			}
		}
		
		public static void Remove(this IAttribute attr, params string[] keys)
			=> Set(attr, (IAttribute)null, keys);
		
		public static void Set(this IAttribute attr, ItemStack value, params string[] keys)
			=> Set(attr, (value != null) ? new ItemstackAttribute(value) : null, keys);
	}
}

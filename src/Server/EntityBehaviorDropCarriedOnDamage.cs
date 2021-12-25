using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace CarryCapacity.Server
{
	public class EntityBehaviorDropCarriedOnDamage : EntityBehavior
	{
		public static string NAME { get; }
			= $"{ CarrySystem.MOD_ID }:dropondamage";
		
		private static readonly CarrySlot[] DROP_FROM
			= new []{ CarrySlot.Hands, CarrySlot.Shoulder };
		
		
		public override string PropertyName() => NAME;
		
		public EntityBehaviorDropCarriedOnDamage(Entity entity)
			: base(entity) {  }
		
		public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
		{
			if (damageSource.Type != EnumDamageType.Heal)
				entity.DropCarried(DROP_FROM, 1, 2);
		}
	}
}

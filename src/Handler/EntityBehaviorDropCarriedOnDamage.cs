using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace CarryCapacity
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
		
		public override void OnEntityReceiveDamage(DamageSource damageSource, float damage)
			=> entity.DropCarried(DROP_FROM, 1, 2);
	}
}

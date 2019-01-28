using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace CarryCapacity.Common
{
  public class DeathHandler
	{
		public DeathHandler(ICoreServerAPI api)
			=> api.Event.PlayerDeath += OnPlayerDeath;
		
		private void OnPlayerDeath(IPlayer player, DamageSource source)
			=> player.Entity.DropAllCarried();
	}
}

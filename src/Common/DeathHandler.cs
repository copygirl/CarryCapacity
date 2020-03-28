using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace CarryCapacity.Common
{
  public class DeathHandler
	{
		public DeathHandler(ICoreServerAPI api)
			=> api.Event.PlayerDeath += OnPlayerDeath;
		
		/// <summary>
		///   Only drop carried blocks if "deathPunishment" isn't set to "keep".
		///   This is how the vanilla game checks whether it should drop inventory contents.
		///   
		///   NOTE: Taking damage will still drop blocks not carried on back.
		///   See <see cref="Server.EntityBehaviorDropCarriedOnDamage"/> for that.
		/// </summary>
		private void OnPlayerDeath(IPlayer player, DamageSource source)
		{
			if (player.Entity.Properties.Server?.Attributes?.GetBool("keepContents", false) != true)
				player.Entity.DropAllCarried();
		}
	}
}

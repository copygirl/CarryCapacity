using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace CarryCapacity.Client
{
	/// <summary> Utility class which is meant to syncronize animations for
	///           the local player to actually reflect what is being carried. </summary>
	public class AnimationFixer
	{
		private HashSet<string> _previous = new HashSet<string>();
		
		public void Update(EntityPlayer player)
		{
			var current = new HashSet<string>(player.GetCarried()
				.Select(carried => carried.Behavior?.Slots[carried.Slot]?.Animation)
				.Where(animation => (animation != null)));
			
			var added   = current.Except(_previous);
			var removed = _previous.Except(current);
			
			foreach (var anim in added) player.StartAnimation(anim);
			foreach (var anim in removed) player.StopAnimation(anim);
			
			_previous = current;
		}
	}
}

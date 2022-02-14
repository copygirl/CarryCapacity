using System.Collections.Generic;
using ProtoBuf;

namespace CarryCapacity.Common.Network
{
	[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
	public class LockSlotsMessage
	{
		public List<int> HotbarSlots { get; }
		
		private LockSlotsMessage() {  }
		
		public LockSlotsMessage(List<int> hotbarSlots)
			=> HotbarSlots = hotbarSlots;
	}
}

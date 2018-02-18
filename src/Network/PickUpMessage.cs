using ProtoBuf;
using Vintagestory.API.MathTools;

namespace CarryCapacity.Network
{
	[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
	public class PickUpMessage
	{
		public BlockPos Position { get; }
		
		private PickUpMessage() {  }
		
		public PickUpMessage(BlockPos position)
			{ Position = position; }
	}
}

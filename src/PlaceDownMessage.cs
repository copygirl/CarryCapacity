using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace CarryCapacity
{
	[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
	public class PlaceDownMessage
	{
		private byte _face;
		
		public BlockPos Position { get; set; }
		
		public BlockFacing Face {
			get => BlockFacing.ALLFACES[_face];
			set => _face = (byte)value.Index;
		}
		
		public BlockSelection Selection => new BlockSelection
			{ Position = Position, Face = Face };
		
		private PlaceDownMessage() {  }
		public PlaceDownMessage(BlockPos position, BlockFacing face)
			{ Position = position; Face = face; }
		public PlaceDownMessage(BlockSelection selection)
			: this(selection.Position, selection.Face) {  }
	}
}

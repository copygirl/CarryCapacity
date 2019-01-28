using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace CarryCapacity.Common.Network
{
	[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
	public class PlaceDownMessage
	{
		public CarrySlot Slot { get; }
		
		private readonly BlockPos _pos;
		private readonly byte _face;
		private readonly float _x, _y, _z;
		
		public BlockSelection Selection
			=> new BlockSelection {
				Position    = _pos,
				Face        = BlockFacing.ALLFACES[_face],
				HitPosition = new Vec3d(_x, _y, _z),
			};
		
		
		private PlaceDownMessage() {  }
		
		public PlaceDownMessage(CarrySlot slot, BlockSelection selection)
		{
			Slot  = slot;
			_pos  = selection.Position;
			_face = (byte)selection.Face.Index;
			_x = (float)selection.HitPosition.X;
			_y = (float)selection.HitPosition.Y;
			_z = (float)selection.HitPosition.Z;
		}
	}
}

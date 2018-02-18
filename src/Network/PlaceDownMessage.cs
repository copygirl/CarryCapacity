using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace CarryCapacity.Network
{
	[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
	public class PlaceDownMessage
	{
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
		
		public PlaceDownMessage(BlockSelection selection)
		{
			_pos  = selection.Position;
			_face = (byte)selection.Face.Index;
			_x = (float)selection.HitPosition.X;
			_y = (float)selection.HitPosition.Y;
			_z = (float)selection.HitPosition.Z;
		}
	}
}

using System;
using Vintagestory.API.Client;

namespace CarryCapacity.Handler
{
	/// <summary> Handles custom mouse interactions because the API
	///           currently doesn't expose the necessary events. </summary>
	public class CustomMouseHandler
	{
		private bool _prevMouseDown = false;
		private float _timeHeldDown = 0.0F;
		
		private ICoreClientAPI API { get; }
		private IClientPlayer Player => API.World.Player;
		
		public event Action OnRightMousePressed;
		public event Action OnRightMouseReleased;
		public event Action<float> OnRightMouseHeld;
		
		public CustomMouseHandler(ICoreClientAPI api)
		{
			API = api;
			API.Event.RegisterGameTickListener(Update, 0);
		}
		
		private void Update(float delta)
		{
			var mouseDown = Player.Entity.Controls.RightMouseDown;
			if (mouseDown != _prevMouseDown) {
				_timeHeldDown = 0.0F;
				if (mouseDown) OnRightMousePressed?.Invoke();
				else OnRightMouseReleased?.Invoke();
			} else OnRightMouseHeld?.Invoke(_timeHeldDown += delta);
			_prevMouseDown = mouseDown;
		}
	}
}

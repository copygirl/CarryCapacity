using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace CarryCapacity.Client
{
	public class HudOverlayRenderer : IRenderer
	{
		private const int CIRCLE_COLOR = 0xCCCCCC;
		private const float CIRCLE_ALPHA_IN  = 0.2F; // How quickly circle fades
		private const float CIRCLE_ALPHA_OUT = 0.4F; // in and out in seconds.
		
		private const int CIRCLE_MAX_STEPS = 16;
		private const float OUTER_RADIUS = 24;
		private const float INNER_RADIUS = 18;
		
		
		private MeshRef _circleMesh = null;
		
		private ICoreClientAPI API { get; }
		
		private float _circleAlpha    = 0.0F;
		private float _circleProgress = 0.0F;
		
		public bool CircleVisible { get; set; }
		public float CircleProgress {
			get => _circleProgress;
			set {
				_circleProgress = GameMath.Clamp(value, 0.0F, 1.0F);
				CircleVisible = true;
			}
		}
		
		public HudOverlayRenderer(ICoreClientAPI api)
		{
			API = api;
			API.Event.RegisterRenderer(this, EnumRenderStage.Ortho);
			UpdateCirceMesh(1);
		}
		
		private void UpdateCirceMesh(float progress)
		{
			var ringSize = (float)INNER_RADIUS / OUTER_RADIUS;
			var stepSize = 1.0F / CIRCLE_MAX_STEPS;
			
			var steps = 1 + (int)Math.Ceiling(CIRCLE_MAX_STEPS * progress);
			var data = new MeshData(steps * 2, steps * 6, false, false, true, false, false);
			
			for (var i = 0; i < steps; i++) {
				var p = Math.Min(progress, i * stepSize) * Math.PI * 2;
				var x =  (float)Math.Sin(p);
				var y = -(float)Math.Cos(p);
				
				data.AddVertex(x           , y           , 0, ColorUtil.WhiteArgb);
				data.AddVertex(x * ringSize, y * ringSize, 0, ColorUtil.WhiteArgb);
				
				if (i > 0) {
					data.AddIndices(new []{ i * 2 - 2, i * 2 - 1, i * 2 + 0 });
					data.AddIndices(new []{ i * 2 + 0, i * 2 - 1, i * 2 + 1 });
				}
			}
			
			if (_circleMesh != null) API.Render.UpdateMesh(_circleMesh, data);
			else _circleMesh = API.Render.UploadMesh(data);
		}
		
		// IRenderer implementation
		
		public double RenderOrder => 0;
		public int RenderRange => 10;
		
		public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
		{
			var rend   = API.Render;
			var shader = rend.CurrentActiveShader;
			
			_circleAlpha = Math.Max(0.0F, Math.Min(1.0F, _circleAlpha
				+ deltaTime / (CircleVisible ? CIRCLE_ALPHA_IN : -CIRCLE_ALPHA_OUT)));
			
			// TODO: Do some smoothing between frames?
			if ((CircleProgress <= 0.0F) || (_circleAlpha <= 0.0F)) return;
			UpdateCirceMesh(CircleProgress);
			
			var r = ((CIRCLE_COLOR >> 16) & 0xFF) / 255.0F;
			var g = ((CIRCLE_COLOR >>  8) & 0xFF) / 255.0F;
			var b = ((CIRCLE_COLOR      ) & 0xFF) / 255.0F;
			var color = new Vec4f(r, g, b, _circleAlpha);
			
			shader.Uniform("rgbaIn", color);
			shader.Uniform("extraGlow", 0);
			shader.Uniform("applyColor", 0);
			shader.Uniform("tex2d", 0);
			shader.Uniform("noTexture", 1.0F);
			shader.UniformMatrix("projectionMatrix", rend.CurrentProjectionMatrix);
			
			// TODO: Render at mouse cursor, not center of screen.
			//       Gotta wait for the API to add MouseMove event.
			var x = API.Render.FrameWidth / 2;
			var y = API.Render.FrameHeight / 2;
			
			rend.GlPushMatrix();
				rend.GlTranslate(x, y, 0);
				rend.GlScale(OUTER_RADIUS, OUTER_RADIUS, 0);
				shader.UniformMatrix("modelViewMatrix", rend.CurrentModelviewMatrix);
			rend.GlPopMatrix();
			
			rend.RenderMesh(_circleMesh);
		}
		
		public void Dispose()
		{
			if (_circleMesh != null)
				API.Render.DeleteMesh(_circleMesh);
		}
	}
}

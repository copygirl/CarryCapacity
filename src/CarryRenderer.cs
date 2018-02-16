using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CarryCapacity
{
	public class CarryRenderer : IRenderer
	{
		private static readonly float[] _tmpMat = Mat4f.Create();
		private static readonly Vec3f _impliedOffset
			= new Vec3f(0.0F, -0.6F, -0.5F);
		private static readonly ModelTransform _defaultTransform
			= new ModelTransform {
				Translation = new Vec3f(0.0F, 0.0F, 0.0F),
				Rotation    = new Vec3f(0.0F, 0.0F, 0.0F),
				Origin      = new Vec3f(0.5F, 0.5F, 0.5F),
				Scale       = 0.5F
			};
		
		private readonly ICoreClientAPI _api;
		private readonly Dictionary<string, CachedCarryableBlock> _cachedBlocks
			= new Dictionary<string, CachedCarryableBlock>();
		
		public CarryRenderer(ICoreClientAPI api) => _api = api;
		
		public static void Register(ICoreClientAPI api)
		{
			var renderer = new CarryRenderer(api);
			
			api.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque);
			api.Event.RegisterRenderer(renderer, EnumRenderStage.ShadowFar);
			api.Event.RegisterRenderer(renderer, EnumRenderStage.ShadowNear);
		}
		
		public class CachedCarryableBlock
		{
			public MeshRef Mesh { get; }
			public int TextureID { get; }
			public ModelTransform Transform { get; }
			
			public CachedCarryableBlock(MeshRef mesh, int textureID, ModelTransform transform)
				{ Mesh = mesh; TextureID = textureID; Transform = transform; }
		}
		
		public CachedCarryableBlock GetCachedBlock(string code)
		{
			if (code == null) return null;
			if (_cachedBlocks.TryGetValue(code, out var cached)) return cached;
			
			var block    = _api.World.GetBlock(new AssetLocation(code));
			var behavior = block.GetBehavior(typeof(BlockCarryable));
			if (behavior != null) {
				
				var meshData  = _api.Tesselator.GetDefaultBlockMesh(block);
				var mesh      = _api.Render.UploadMesh(meshData);
				var textureID = _api.BlockTextureAtlas.Positions[0].atlasTextureId;
				
				var transform = _defaultTransform.Clone();
				// Load transform from behavior properties (or use default).
				if (behavior.properties != null) {
					TryGetVec3f(behavior.properties["translation"], ref transform.Translation);
					TryGetVec3f(behavior.properties["rotation"], ref transform.Rotation);
					TryGetVec3f(behavior.properties["origin"], ref transform.Origin);
					var scale = behavior.properties["scale"].AsFloat();
					if (scale > 0) transform.Scale = scale;
				}
				
				// FIXME: AsFloatArray() currently has an issue, simplify with next version!
				void TryGetVec3f(JsonObject obj, ref Vec3f result) {
					var array = obj.AsArray();
					if (array?.Length != 3) return;
					var floats = new float[3];
					for (var i = 0; i < floats.Length; i++) {
						var floatVal = array[i].AsFloat(float.NaN);
						var intVal   = array[i].AsInt(int.MinValue);
						if (!float.IsNaN(floatVal)) floats[i]  = floatVal;
						else if (intVal > int.MinValue) floats[i] = intVal;
						else return;
					}
					result = new Vec3f(floats);
				}
				
				cached = new CachedCarryableBlock(mesh, textureID, transform);
				
			}
			
			_cachedBlocks.Add(code, cached);
			return cached;
		}
		
		// IRenderer implementation
		
		public double RenderOrder => 1.0;
		public int RenderRange => 99;
		
		public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
		{
			foreach (var player in _api.World.AllPlayers) {
				// Don't render anything on the client player if they're in first person.
				if ((_api.World.Player.CameraMode == EnumCameraMode.FirstPerson)
					&& (player == _api.World.Player)) continue;
				
				var entity = player.Entity;
				var code   = entity.WatchedAttributes.GetString(BlockCarryable.ATTRIBUTE_ID);
				var cached = GetCachedBlock(code);
				if (cached == null) continue;
				
				var renderer     = (EntityShapeRenderer)entity.Renderer;
				var isShadowPass = (stage != EnumRenderStage.Opaque);
				
				var renderApi = _api.Render;
				var animator  = (BlendEntityAnimator)renderer.animator;
				if (!animator.AttachmentPointByCode.TryGetValue("Back", out var pose)) return;
				
				Mat4f.Copy(_tmpMat, renderer.ModelMat);
				
				var attach = pose.AttachPoint;
				
				var mat  = pose.Pose.AnimModelMatrix;
				var orig = Mat4f.Create();
				for (int i = 0; i < 16; i++)
					orig[i] = (float)_api.Render.CameraMatrixOrigin[i];
				
				if (!isShadowPass) Mat4f.Mul(_tmpMat, orig, _tmpMat);
				Mat4f.Mul(_tmpMat, _tmpMat, mat);
				
				IStandardShaderProgram prog = null;
				
				if (!isShadowPass) {
					prog = renderApi.PreparedStandardShader((int)entity.Pos.X, (int)entity.Pos.Y, (int)entity.Pos.Z);
					prog.Tex2D = cached.TextureID;
					prog.AlphaTest = 0.01f;
				} else renderApi.CurrentActiveShader.BindTexture2D("tex2d", cached.TextureID, 0);
				
				// Apply attachment point transform.
				Mat4f.Translate(_tmpMat, _tmpMat, (float)(attach.PosX / 16), (float)(attach.PosY / 16), (float)(attach.PosZ / 16));
				Mat4f.RotateX(_tmpMat, _tmpMat, (float)attach.RotationX * GameMath.DEG2RAD);
				Mat4f.RotateY(_tmpMat, _tmpMat, (float)attach.RotationY * GameMath.DEG2RAD);
				Mat4f.RotateZ(_tmpMat, _tmpMat, (float)attach.RotationZ * GameMath.DEG2RAD);
				
				// Apply carried block's behavior transform.
				var t = cached.Transform;
				Mat4f.Scale(_tmpMat, _tmpMat, t.Scale, t.Scale, t.Scale);
				Mat4f.Translate(_tmpMat, _tmpMat, _impliedOffset.X, _impliedOffset.Y, _impliedOffset.Z);
				Mat4f.Translate(_tmpMat, _tmpMat, t.Origin.X, t.Origin.Y, t.Origin.Z);
				Mat4f.RotateX(_tmpMat, _tmpMat, t.Rotation.X * GameMath.DEG2RAD);
				Mat4f.RotateY(_tmpMat, _tmpMat, t.Rotation.Y * GameMath.DEG2RAD);
				Mat4f.RotateZ(_tmpMat, _tmpMat, t.Rotation.Z * GameMath.DEG2RAD);
				Mat4f.Translate(_tmpMat, _tmpMat, -t.Origin.X, -t.Origin.Y, -t.Origin.Z);
				Mat4f.Translate(_tmpMat, _tmpMat, t.Translation.X, t.Translation.Y, t.Translation.Z);
				
				if (!isShadowPass)
					prog.ModelViewMatrix = _tmpMat;
				else {
					Mat4f.Mul(_tmpMat, _api.Render.CurrentShadowProjectionMatrix, _tmpMat);
					_api.Render.CurrentActiveShader.UniformMatrix("mvpMatrix", _tmpMat);
					_api.Render.CurrentActiveShader.Uniform("origin", renderer.OriginPos);
				}
				
				_api.Render.RenderMesh(cached.Mesh);
				
				prog?.Stop();
			}
		}
		
		public void Dispose()
		{
			foreach (var cached in _cachedBlocks.Values)
				_api.Render.DeleteMesh(cached.Mesh);
			_cachedBlocks.Clear();
		}
	}
}

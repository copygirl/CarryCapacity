using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CarryCapacity.Client
{
	public class EntityCarryRenderer : IRenderer
	{
		private static readonly float[] _tmpMat = Mat4f.Create();
		private static readonly Vec3f _impliedOffset
			= new Vec3f(0.0F, -0.6F, -0.5F);
		
		
		private readonly Dictionary<int, CachedCarryableBlock> _cachedBlocks
			= new Dictionary<int, CachedCarryableBlock>();
		
		private ICoreClientAPI API { get; }
		
		public EntityCarryRenderer(ICoreClientAPI api)
		{
			API = api;
			api.Event.RegisterRenderer(this, EnumRenderStage.Opaque);
			api.Event.RegisterRenderer(this, EnumRenderStage.ShadowFar);
			api.Event.RegisterRenderer(this, EnumRenderStage.ShadowNear);
		}
		
		
		private class CachedCarryableBlock
		{
			public MeshRef Mesh { get; }
			public int TextureID { get; }
			public ModelTransform Transform { get; }
			
			public CachedCarryableBlock(MeshRef mesh, int textureID, ModelTransform transform)
				{ Mesh = mesh; TextureID = textureID; Transform = transform; }
		}
		
		private CachedCarryableBlock GetCachedBlock(CarriedBlock carried)
		{
			if (carried == null) return null;
			if (_cachedBlocks.TryGetValue(carried.Block.Id, out var cached)) return cached;
			
			API.Tesselator.TesselateBlock(carried.Block, out var meshData);
			var mesh      = API.Render.UploadMesh(meshData);
			var textureID = API.BlockTextureAtlas.Positions[0].atlasTextureId;
			var transform = carried.Block.GetBehaviorOrDefault(
				BlockBehaviorCarryable.DEFAULT).Transform;
			
			cached = new CachedCarryableBlock(mesh, textureID, transform);
			_cachedBlocks.Add(carried.Block.Id, cached);
			return cached;
		}
		
		
		// IRenderer implementation
		
		public double RenderOrder => 1.0;
		public int RenderRange => 99;
		
		public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
		{
			foreach (var player in API.World.AllPlayers) {
				// Leaving the additional, more detailed exceptions in just in case other things end up breaking.
				if (player == null) throw new Exception("null player in API.World.AllPlayers!");
				
				// Player entity may be null in some circumstances.
				// Maybe the other player is too far away, so there's
				// no entity spawned for them on the client's side?
				if (player.Entity == null) continue;
				
				if (API.World == null) throw new Exception("API.World is null!");
				if (API.World.Player == null) throw new Exception("API.World.Player is null!");
				
				// Don't render anything on the client player if they're in first person.
				if ((API.World.Player.CameraMode == EnumCameraMode.FirstPerson)
					&& (player == API.World.Player)) continue;
				
				var entity  = player.Entity;
				var carried = entity.GetCarried();
				var cached  = GetCachedBlock(carried);
				if (cached == null) continue;
				
				var renderer = (EntityShapeRenderer)entity.Renderer;
				if (renderer == null) continue; // Apparently this can end up being null?
				// Reported to Tyron, so it might be fixed. Leaving it in for now just in case.
				
				var animator = (BlendEntityAnimator)renderer.curAnimator;
				if (animator == null) throw new Exception("renderer.curAnimator is null!");
				if (!animator.AttachmentPointByCode.TryGetValue("Back", out var pose)) return;
				
				var renderApi    = API.Render;
				var isShadowPass = (stage != EnumRenderStage.Opaque);
				
				Mat4f.Copy(_tmpMat, renderer.ModelMat);
				
				var attach = pose.AttachPoint;
				
				var mat  = pose.Pose.AnimModelMatrix;
				var orig = Mat4f.Create();
				for (int i = 0; i < 16; i++)
					orig[i] = (float)API.Render.CameraMatrixOrigin[i];
				
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
				Mat4f.Scale(_tmpMat, _tmpMat, t.ScaleXYZ.X, t.ScaleXYZ.Y, t.ScaleXYZ.Z);
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
					Mat4f.Mul(_tmpMat, API.Render.CurrentShadowProjectionMatrix, _tmpMat);
					API.Render.CurrentActiveShader.UniformMatrix("mvpMatrix", _tmpMat);
					API.Render.CurrentActiveShader.Uniform("origin", renderer.OriginPos);
				}
				
				API.Render.RenderMesh(cached.Mesh);
				
				prog?.Stop();
			}
		}
		
		public void Dispose()
		{
			foreach (var cached in _cachedBlocks.Values)
				API.Render.DeleteMesh(cached.Mesh);
			_cachedBlocks.Clear();
		}
	}
}

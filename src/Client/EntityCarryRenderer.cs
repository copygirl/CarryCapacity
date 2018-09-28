using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CarryCapacity.Client
{
  public class EntityCarryRenderer : IRenderer
	{
		private static readonly Vec3f _impliedOffset
			= new Vec3f(0.0F, -0.6F, -0.5F);
		
		
		private ICoreClientAPI API { get; }
		
		public EntityCarryRenderer(ICoreClientAPI api)
		{
			API = api;
			api.Event.RegisterRenderer(this, EnumRenderStage.Opaque);
			api.Event.RegisterRenderer(this, EnumRenderStage.ShadowFar);
			api.Event.RegisterRenderer(this, EnumRenderStage.ShadowNear);
		}
		
		public void Dispose() {  }
		
		
		private class CachedCarryableBlock
		{
			public MeshRef Mesh { get; }
			public int TextureID { get; }
			public ModelTransform Transform { get; }
			
			public CachedCarryableBlock(MeshRef mesh, int textureID, ModelTransform transform)
				{ Mesh = mesh; TextureID = textureID; Transform = transform; }
		}
		
		private ItemRenderInfo GetRenderInfo(CarriedBlock carried)
		{
			// Alternative: Cache API.TesselatorManager.GetDefaultBlockMesh manually.
			var renderInfo = API.Render.GetItemStackRenderInfo(
				carried.ItemStack, EnumItemRenderTarget.Ground);
			renderInfo.Transform = carried.Block.GetBehaviorOrDefault(
				BlockBehaviorCarryable.DEFAULT).Transform;
			return renderInfo;
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
				if (carried == null) continue;
				
				var renderInfo = GetRenderInfo(carried);
				var renderer = (EntityShapeRenderer)entity.Properties.Client.Renderer;
				if (renderer == null) continue; // Apparently this can end up being null?
				// Reported to Tyron, so it might be fixed. Leaving it in for now just in case.
				
				var animator = (BlendEntityAnimator)renderer.curAnimator;
				if (animator == null) throw new Exception("renderer.curAnimator is null!");
				if (!animator.AttachmentPointByCode.TryGetValue("Back", out var pose)) return;
				
				var renderApi    = API.Render;
				var isShadowPass = (stage != EnumRenderStage.Opaque);
				
				var modelMat = Mat4f.CloneIt(renderer.ModelMat);
				var viewMat  = Array.ConvertAll(API.Render.CameraMatrixOrigin, i => (float)i);
				
				var animModelMat = pose.Pose.AnimModelMatrix;
				Mat4f.Mul(modelMat, modelMat, animModelMat);
				
				IStandardShaderProgram prog = null;
				
				if (isShadowPass) {
					renderApi.CurrentActiveShader.BindTexture2D("tex2d", renderInfo.TextureId, 0);
				} else {
					prog = renderApi.PreparedStandardShader((int)entity.Pos.X, (int)entity.Pos.Y, (int)entity.Pos.Z);
					prog.Tex2D = renderInfo.TextureId;
					prog.AlphaTest = 0.01f;
				}
				
				// Apply attachment point transform.
				var attach = pose.AttachPoint;
				Mat4f.Translate(modelMat, modelMat, (float)(attach.PosX / 16), (float)(attach.PosY / 16), (float)(attach.PosZ / 16));
				Mat4f.RotateX(modelMat, modelMat, (float)attach.RotationX * GameMath.DEG2RAD);
				Mat4f.RotateY(modelMat, modelMat, (float)attach.RotationY * GameMath.DEG2RAD);
				Mat4f.RotateZ(modelMat, modelMat, (float)attach.RotationZ * GameMath.DEG2RAD);
				
				// Apply carried block's behavior transform.
				var t = renderInfo.Transform;
				Mat4f.Scale(modelMat, modelMat, t.ScaleXYZ.X, t.ScaleXYZ.Y, t.ScaleXYZ.Z);
				Mat4f.Translate(modelMat, modelMat, _impliedOffset.X, _impliedOffset.Y, _impliedOffset.Z);
				Mat4f.Translate(modelMat, modelMat, t.Origin.X, t.Origin.Y, t.Origin.Z);
				Mat4f.RotateX(modelMat, modelMat, t.Rotation.X * GameMath.DEG2RAD);
				Mat4f.RotateY(modelMat, modelMat, t.Rotation.Y * GameMath.DEG2RAD);
				Mat4f.RotateZ(modelMat, modelMat, t.Rotation.Z * GameMath.DEG2RAD);
				Mat4f.Translate(modelMat, modelMat, -t.Origin.X, -t.Origin.Y, -t.Origin.Z);
				Mat4f.Translate(modelMat, modelMat, t.Translation.X, t.Translation.Y, t.Translation.Z);
				
				if (isShadowPass) {
					Mat4f.Mul(modelMat, API.Render.CurrentShadowProjectionMatrix, modelMat);
					API.Render.CurrentActiveShader.UniformMatrix("mvpMatrix", modelMat);
					API.Render.CurrentActiveShader.Uniform("origin", renderer.OriginPos);
				} else {
					prog.ModelMatrix = modelMat;
					prog.ViewMatrix  = viewMat;
				}
				
				API.Render.RenderMesh(renderInfo.ModelRef);
				
				prog?.Stop();
			}
		}
	}
}

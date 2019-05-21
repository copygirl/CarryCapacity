using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CarryCapacity.Client
{
	public class EntityCarryRenderer : IRenderer
	{
		private static readonly Dictionary<CarrySlot, SlotRenderSettings> _renderSettings
			= new Dictionary<CarrySlot, SlotRenderSettings> {
				{ CarrySlot.Hands    , new SlotRenderSettings("carrycapacity:FrontCarry", 0.05F, -0.5F, -0.5F) },
				{ CarrySlot.Back     , new SlotRenderSettings("Back", 0.0F, -0.6F, -0.5F) },
				{ CarrySlot.Shoulder , new SlotRenderSettings("carrycapacity:ShoulderL", -0.5F, 0.0F, -0.5F) },
			};
		
		private class SlotRenderSettings
		{
			public string AttachmentPoint { get; }
			public Vec3f Offset { get; }
			public SlotRenderSettings(string attachmentPoint, float xOffset, float yOffset, float zOffset)
				{ AttachmentPoint = attachmentPoint; Offset = new Vec3f(xOffset, yOffset, zOffset); }
		}
		
		
		private ICoreClientAPI API { get; set; }
		private AnimationFixer AnimationFixer { get; set; }
		
		public EntityCarryRenderer(ICoreClientAPI api)
		{
			API = api;
			API.Event.RegisterRenderer(this, EnumRenderStage.Opaque);
			API.Event.RegisterRenderer(this, EnumRenderStage.ShadowFar);
			API.Event.RegisterRenderer(this, EnumRenderStage.ShadowNear);
			AnimationFixer = new AnimationFixer();
		}
		
		public void Dispose()
		{
			API.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
			API.Event.UnregisterRenderer(this, EnumRenderStage.ShadowFar);
			API.Event.UnregisterRenderer(this, EnumRenderStage.ShadowNear);
			API = null;
			AnimationFixer = null;
		}
		
		
		private ItemRenderInfo GetRenderInfo(CarriedBlock carried)
		{
			// Alternative: Cache API.TesselatorManager.GetDefaultBlockMesh manually.
			var renderInfo = API.Render.GetItemStackRenderInfo(carried.ItemStack, EnumItemRenderTarget.Ground);
			var behavior   = carried.Behavior;
			renderInfo.Transform = behavior.Slots[carried.Slot]?.Transform ?? behavior.DefaultTransform;
			return renderInfo;
		}
		
		
		// IRenderer implementation
		
		public double RenderOrder => 1.0;
		public int RenderRange => 99;
		
		private float moveWobble;
		
		public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
		{
			foreach (var player in API.World.AllPlayers) {
				// Player entity may be null in some circumstances.
				// Maybe the other player is too far away, so there's
				// no entity spawned for them on the client's side?
				if (player.Entity == null) continue;
				
				// Fix up animations that should/shouldn't be playing.
				var isLocalPlayer = (player.Entity == API.World.Player);
				if (isLocalPlayer) AnimationFixer.Update(player.Entity);
				
				RenderAllCarried(player.Entity, deltaTime, stage);
			}
		}
		
		
		/// <summary> Renders all carried blocks of the specified entity. </summary>
		private void RenderAllCarried(EntityAgent entity, float deltaTime, EnumRenderStage stage)
		{
			var allCarried = entity.GetCarried().ToList();
			if (allCarried.Count == 0) return; // Entity is not carrying anything.
			
			var isLocalPlayer = (entity == API.World.Player.Entity);
			var isFirstPerson = isLocalPlayer && (API.World.Player.CameraMode == EnumCameraMode.FirstPerson);
			var isShadowPass  = (stage != EnumRenderStage.Opaque);
			
			var renderer = (EntityShapeRenderer)entity.Properties.Client.Renderer;
			var animator = entity.AnimManager.Animator;
			
			foreach (var carried in allCarried)
				RenderCarried(entity, carried, deltaTime, stage,
				              isLocalPlayer, isFirstPerson, isShadowPass,
				              renderer, animator);
		}
		
		/// <summary> Renders the specified carried block on the specified entity. </summary>
		private void RenderCarried(EntityAgent entity, CarriedBlock carried,
		                           float deltaTime, EnumRenderStage stage,
		                           bool isLocalPlayer, bool isFirstPerson, bool isShadowPass,
		                           EntityShapeRenderer renderer, IAnimator animator)
		{
			var inHands = (carried.Slot == CarrySlot.Hands);
			if (!inHands && isFirstPerson && !isShadowPass) return; // Only Hands slot is rendered in first person.
			
			var viewMat        = Array.ConvertAll(API.Render.CameraMatrixOrigin, i => (float)i);
			var renderSettings = _renderSettings[carried.Slot];
			var renderInfo     = GetRenderInfo(carried);
			
			float[] modelMat;
			if (inHands && isFirstPerson && !isShadowPass) {
				modelMat = GetFirstPersonHandsMatrix(entity, viewMat, deltaTime);
			} else {
				var attachPointAndPose = animator.GetAttachmentPointPose(renderSettings.AttachmentPoint);
				if (attachPointAndPose == null) return; // Couldn't find attachment point.
				modelMat = GetAttachmentPointMatrix(renderer, attachPointAndPose);
			}
			
			// Apply carried block's behavior transform.
			var t = renderInfo.Transform;
			Mat4f.Scale(modelMat, modelMat, t.ScaleXYZ.X, t.ScaleXYZ.Y, t.ScaleXYZ.Z);
			Mat4f.Translate(modelMat, modelMat, renderSettings.Offset.X, renderSettings.Offset.Y, renderSettings.Offset.Z);
			Mat4f.Translate(modelMat, modelMat, t.Origin.X, t.Origin.Y, t.Origin.Z);
			Mat4f.RotateX(modelMat, modelMat, t.Rotation.X * GameMath.DEG2RAD);
			Mat4f.RotateZ(modelMat, modelMat, t.Rotation.Z * GameMath.DEG2RAD);
			Mat4f.RotateY(modelMat, modelMat, t.Rotation.Y * GameMath.DEG2RAD);
			Mat4f.Translate(modelMat, modelMat, -t.Origin.X, -t.Origin.Y, -t.Origin.Z);
			Mat4f.Translate(modelMat, modelMat, t.Translation.X, t.Translation.Y, t.Translation.Z);
			
			if (isShadowPass) {
				var prog = API.Render.CurrentActiveShader;
				Mat4f.Mul(modelMat, API.Render.CurrentShadowProjectionMatrix, modelMat);
				prog.BindTexture2D("tex2d", renderInfo.TextureId, 0);
				prog.UniformMatrix("mvpMatrix", modelMat);
				prog.Uniform("origin", renderer.OriginPos);
				
				API.Render.RenderMesh(renderInfo.ModelRef);
			} else {
				var prog = API.Render.PreparedStandardShader((int)entity.Pos.X, (int)entity.Pos.Y, (int)entity.Pos.Z);
				prog.Tex2D            = renderInfo.TextureId;
				prog.AlphaTest        = 0.01f;
				prog.ViewMatrix       = viewMat;
				prog.ModelMatrix      = modelMat;
				prog.DontWarpVertices = 1;
				
				API.Render.RenderMesh(renderInfo.ModelRef);
				
				prog.Stop();
			}
		}
		
		/// <summary> Returns a model view matrix for rendering a carried block on the specified attachment point. </summary>
		private float[] GetAttachmentPointMatrix(EntityShapeRenderer renderer, AttachmentPointAndPose attachPointAndPose)
		{
			var modelMat     = Mat4f.CloneIt(renderer.ModelMat);
			var animModelMat = attachPointAndPose.AnimModelMatrix;
			Mat4f.Mul(modelMat, modelMat, animModelMat);
			
			// Apply attachment point transform.
			var attach = attachPointAndPose.AttachPoint;
			Mat4f.Translate(modelMat, modelMat, (float)(attach.PosX / 16), (float)(attach.PosY / 16), (float)(attach.PosZ / 16));
			Mat4f.RotateX(modelMat, modelMat, (float)attach.RotationX * GameMath.DEG2RAD);
			Mat4f.RotateY(modelMat, modelMat, (float)attach.RotationY * GameMath.DEG2RAD);
			Mat4f.RotateZ(modelMat, modelMat, (float)attach.RotationZ * GameMath.DEG2RAD);
			
			return modelMat;
		}
		
		private float[] GetFirstPersonHandsMatrix(EntityAgent entity, float[] viewMat, float deltaTime)
		{
			var modelMat = Mat4f.Invert(Mat4f.Create(), viewMat);
			
			if (entity.Controls.TriesToMove) {
				var moveSpeed = entity.Controls.MovespeedMultiplier * (float)entity.GetWalkSpeedMultiplier();
				moveWobble += moveSpeed * deltaTime * 5.0F;
			} else {
				var target = (float)(Math.Round(moveWobble / Math.PI) * Math.PI);
				var speed = deltaTime * (0.2F + Math.Abs(target - moveWobble) * 4);
				if (Math.Abs(target - moveWobble) < speed) moveWobble = target;
				else moveWobble += Math.Sign(target - moveWobble) * speed;
			}
			moveWobble = moveWobble % (GameMath.PI * 2);
			
			var moveWobbleOffsetX = GameMath.Sin((moveWobble + GameMath.PI)) * 0.03F;
			var moveWobbleOffsetY = GameMath.Sin(moveWobble * 2) * 0.02F;
			
			Mat4f.Translate(modelMat, modelMat, moveWobbleOffsetX, moveWobbleOffsetY - 0.25F, -0.25F);
			Mat4f.RotateY(modelMat, modelMat, 90.0F * GameMath.DEG2RAD);
			
			return modelMat;
		}
	}
}

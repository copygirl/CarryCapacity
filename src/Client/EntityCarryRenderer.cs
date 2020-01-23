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
		
		private long _renderTick = 0;
		
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
		
		public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
		{
			foreach (var player in API.World.AllPlayers) {
				// Player entity may be null in some circumstances..?
				if (player.Entity == null) continue;
				
				var isLocalPlayer = (player == API.World.Player);
				var isShadowPass  = (stage != EnumRenderStage.Opaque);
				
				// Fix up animations that should/shouldn't be playing.
				if (isLocalPlayer) AnimationFixer.Update(player.Entity);
				
				// Don't render if entity hasn't been rendered for the
				// specified render stage (unless it's the local player).
				else if (isShadowPass ? !player.Entity.IsShadowRendered
				                      : !player.Entity.IsRendered) continue;
				
				RenderAllCarried(player.Entity, deltaTime, isShadowPass);
			}
			_renderTick++;
		}
		
		
		/// <summary> Renders all carried blocks of the specified entity. </summary>
		private void RenderAllCarried(EntityAgent entity, float deltaTime, bool isShadowPass)
		{
			var allCarried = entity.GetCarried().ToList();
			if (allCarried.Count == 0) return; // Entity is not carrying anything.
			
			var isLocalPlayer = (entity == API.World.Player.Entity);
			var isFirstPerson = isLocalPlayer && (API.World.Player.CameraMode == EnumCameraMode.FirstPerson);
			
			var renderer = (EntityShapeRenderer)entity.Properties.Client.Renderer;
			var animator = entity.AnimManager.Animator;
			
			foreach (var carried in allCarried)
				RenderCarried(entity, carried, deltaTime,
				              isLocalPlayer, isFirstPerson, isShadowPass,
				              renderer, animator);
		}
		
		/// <summary> Renders the specified carried block on the specified entity. </summary>
		private void RenderCarried(EntityAgent entity, CarriedBlock carried, float deltaTime,
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
		
		// The most recent tick that the hands were rendered.
		private long _lastTickHandsRendered = 0;
		private float _moveWobble;
		private float _lastYaw;
		private float _yawDifference;
		
		private float[] GetFirstPersonHandsMatrix(EntityAgent entity, float[] viewMat, float deltaTime)
		{
			var modelMat = Mat4f.Invert(Mat4f.Create(), viewMat);
			
			// If the hands haven't been rendered in the last 10 render ticks, reset wobble and such.
			if (_renderTick - _lastTickHandsRendered > 10) {
				_moveWobble = 0;
				_lastYaw = entity.Pos.Yaw;;
				_yawDifference = 0;
			}
			_lastTickHandsRendered = _renderTick;
			
			
			if (entity.Controls.TriesToMove) {
				var moveSpeed = entity.Controls.MovespeedMultiplier * (float)entity.GetWalkSpeedMultiplier();
				_moveWobble += moveSpeed * deltaTime * 5.0F;
			} else {
				var target = (float)(Math.Round(_moveWobble / Math.PI) * Math.PI);
				var speed = deltaTime * (0.2F + Math.Abs(target - _moveWobble) * 4);
				if (Math.Abs(target - _moveWobble) < speed) _moveWobble = target;
				else _moveWobble += Math.Sign(target - _moveWobble) * speed;
			}
			_moveWobble = _moveWobble % (GameMath.PI * 2);
			
			var moveWobbleOffsetX = GameMath.Sin((_moveWobble + GameMath.PI)) * 0.03F;
			var moveWobbleOffsetY = GameMath.Sin(_moveWobble * 2) * 0.02F;
			
			
			_yawDifference += GameMath.AngleRadDistance(_lastYaw, entity.Pos.Yaw);
			_yawDifference *= (1 - 0.075F);
			_lastYaw = entity.Pos.Yaw;
			
			var yawRotation   = -_yawDifference / 2;
			var pitchRotation = (entity.Pos.Pitch - GameMath.PI) / 4;
			
			
			Mat4f.RotateY(modelMat, modelMat, yawRotation);
			Mat4f.Translate(modelMat, modelMat, 0.0F, -0.35F, -0.20F);
			Mat4f.RotateY(modelMat, modelMat, -yawRotation);
			Mat4f.RotateX(modelMat, modelMat, pitchRotation / 2);
			Mat4f.Translate(modelMat, modelMat, 0.0F, 0.0F, -0.20F);
			Mat4f.RotateX(modelMat, modelMat, pitchRotation);
			Mat4f.RotateY(modelMat, modelMat, yawRotation);
			
			Mat4f.Translate(modelMat, modelMat, moveWobbleOffsetX, moveWobbleOffsetY, 0.0F);
			Mat4f.RotateY(modelMat, modelMat, 90.0F * GameMath.DEG2RAD);
			
			return modelMat;
		}
	}
}

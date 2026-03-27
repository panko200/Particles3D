using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Particles3D;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Settings;
using static Particles3D.Particles3DEffect;

namespace Particles3D
{
    internal class VideoEffectChain : IDisposable
    {
        readonly IGraphicsDevicesAndContext devices;
        ID2D1Image? input;
        List<IVideoEffect>? oldVideoEffects;
        readonly Dictionary<IVideoEffect, IVideoEffectProcessor> processors = new();

        public List<IVideoEffect>? VideoEffects { get; set; }
        public ID2D1Image? Output { get; private set; }
        public DrawDescription? UpdatedDrawDescription { get; private set; }

        public VideoEffectChain(IGraphicsDevicesAndContext devices)
        {
            this.devices = devices;
        }

        public void Update(EffectDescription effectDescription)
        {
            if (VideoEffects is null) throw new InvalidOperationException("VideoEffects is null");

            var effects = VideoEffects.Where(e => e.IsEnabled).ToList();

            if (oldVideoEffects is not null)
            {
                foreach (var old in oldVideoEffects)
                {
                    if (!effects.Contains(old) && processors.TryGetValue(old, out var p))
                    {
                        p.ClearInput();
                        p.Dispose();
                        processors.Remove(old);
                    }
                }
            }

            ID2D1Image? image = input;
            var desc = effectDescription;

            foreach (var effect in effects)
            {
                if (!processors.TryGetValue(effect, out var processor))
                {
                    processor = effect.CreateVideoEffect(devices);
                    processors[effect] = processor;
                }

                processor.SetInput(image);
                desc = desc with { DrawDescription = processor.Update(desc) };
                image = processor.Output;
            }

            Output = image;
            UpdatedDrawDescription = desc.DrawDescription;
            oldVideoEffects = effects;
        }

        public void SetInput(ID2D1Image? input) => this.input = input;

        public void ClearInput()
        {
            // ▼追加：メモリリーク防止のため参照をクリア
            this.input = null;
            this.Output = null;
            foreach (var p in processors.Values) p.ClearInput();
        }

        public void Dispose()
        {
            ClearInput();
            foreach (var p in processors.Values) p.Dispose();
            processors.Clear();
        }
    }

    internal class ParticleEffectNodes : IDisposable
    {
        public Vortice.Direct2D1.Effects.AffineTransform2D centeringEffect;
        public Vortice.Direct2D1.Effects.ColorMatrix colorEffect;
        public Vortice.Direct2D1.Effects.GaussianBlur blurEffect;
        public Vortice.Direct2D1.Effects.Opacity opacityEffect;
        public Vortice.Direct2D1.Effects.Transform3D renderEffect;
        public VideoEffectChain effectChain;
        // ▼追加：個体ごとに色相エフェクトを独立して持たせる
        public Particles3DHueCustomEffect hueEffect;

        public ParticleEffectNodes(IGraphicsDevicesAndContext devices)
        {
            centeringEffect = new Vortice.Direct2D1.Effects.AffineTransform2D(devices.DeviceContext);
            colorEffect = new Vortice.Direct2D1.Effects.ColorMatrix(devices.DeviceContext);
            blurEffect = new Vortice.Direct2D1.Effects.GaussianBlur(devices.DeviceContext);
            opacityEffect = new Opacity(devices.DeviceContext);
            renderEffect = new Transform3D(devices.DeviceContext);
            effectChain = new VideoEffectChain(devices);
            hueEffect = new Particles3DHueCustomEffect(devices); // ▼追加
        }

        public void ClearInput()
        {
            centeringEffect?.SetInput(0, null, true);
            colorEffect?.SetInput(0, null, true);
            blurEffect?.SetInput(0, null, true);
            opacityEffect?.SetInput(0, null, true);
            renderEffect?.SetInput(0, null, true);
            effectChain?.ClearInput();
            hueEffect?.SetInput(0, null, true); // ▼追加
        }

        public void Dispose()
        {
            ClearInput();
            centeringEffect?.Dispose();
            colorEffect?.Dispose();
            blurEffect?.Dispose();
            opacityEffect?.Dispose();
            renderEffect?.Dispose();
            effectChain?.Dispose();
            hueEffect?.Dispose(); // ▼追加
        }
    }

    internal class Particles3DEffectProcessor : IVideoEffectProcessor, IDisposable
    {
        DisposeCollector disposer = new();

        int sourceType;
        string directory = "";
        string[]? imageFiles;
        IImageFileSource[]? imageSources;
        int[]? randomImageIndexArray;

        private readonly object _imageLoadLock = new object();

        Random? staticRng;
        float[]? targetXArray;
        float[]? startXArray;
        float[]? targetYArray;
        float[]? startYArray;
        float[]? targetZArray;
        float[]? startZArray;

        float[]? startScaleXArray;
        float[]? targetScaleXArray;
        float[]? startScaleYArray;
        float[]? targetScaleYArray;
        float[]? startScaleZArray;
        float[]? targetScaleZArray;

        float[]? startRotationXArray;
        float[]? targetRotationXArray;
        float[]? startRotationYArray;
        float[]? targetRotationYArray;
        float[]? startRotationZArray;
        float[]? targetRotationZArray;

        float[]? startOpacityArray;
        float[]? targetOpacityArray;

        float[]? curveFactorArray;
        float[]? fixedPositionStartXArray;
        float[]? fixedPositionStartYArray;
        float[]? fixedPositionStartZArray;
        float[]? fixedPositionEndXArray;
        float[]? fixedPositionEndYArray;
        float[]? fixedPositionEndZArray;
        float[]? fixedScaleStartXArray;
        float[]? fixedScaleStartYArray;
        float[]? fixedScaleStartZArray;
        float[]? fixedScaleEndXArray;
        float[]? fixedScaleEndYArray;
        float[]? fixedScaleEndZArray;
        float[]? fixedOpacityStartArray;
        float[]? fixedOpacityMidArray;
        float[]? fixedOpacityEndArray;
        float[]? fixedRotationStartXArray;
        float[]? fixedRotationStartYArray;
        float[]? fixedRotationStartZArray;
        float[]? fixedRotationEndXArray;
        float[]? fixedRotationEndYArray;
        float[]? fixedRotationEndZArray;
        float[]? fixedGravityXArray;
        float[]? fixedGravityYArray;
        float[]? fixedGravityZArray;

        float[]? randomHueOffsetArray;
        float[]? randomSatOffsetArray;
        float[]? randomLumOffsetArray;

        float[]? randomForcePitchArray;
        float[]? randomForceYawArray;
        float[]? randomForceRollArray;
        float[]? randomForceVelocityArray;

        float[]? fixedForcePitchArray;
        float[]? fixedForceYawArray;
        float[]? fixedForceRollArray;
        float[]? fixedForceVelocityArray;

        float[]? hitProgressArray;
        Vector3[]? hitVelocityArray;

        //List<Particles3DHueCustomEffect> hueEffects = new();

        readonly Particles3DEffect item;
        IGraphicsDevicesAndContext devices;

        ID2D1Image? input;
        ID2D1CommandList? commandList;

        List<ParticleEffectNodes> trailEffectPool = new();

        string oldSerializedEffects = string.Empty;

        float itemDrawX, itemDrawY, itemDrawZ;
        bool isFirst = true;
        bool fixedDraw;
        bool randomToggleX, randomSEToggleX, randomToggleY, randomSEToggleY, randomToggleZ, randomSEToggleZ;
        bool IsInputChanged;
        bool curveToggle;
        int count, frame, reverseDraw, randomXCount, randomYCount, randomZCount;
        int randomSeed;
        float startx, starty, startz, endx, endy, endz;
        float startRotationX, startRotationY, startRotationZ;
        float endRotationX, endRotationY, endRotationZ;
        float scaleStartx, scaleStarty, scaleStartz;
        float scalex, scaley, scalez;
        float endopacity, startopacity;
        float delaytime;
        float randomStartXRange, randomEndXRange, randomStartYRange, randomEndYRange, randomStartZRange, randomEndZRange;
        float cycleTime, travelTime;
        float gravityX, gravityY, gravityZ;
        float curveRange;
        bool fixedTrajectory;
        bool randomScaleToggle, randomRotXToggle, randomRotYToggle, randomRotZToggle, randomOpacityToggle;
        int randomScaleCount, randomRotXCount, randomRotYCount, randomRotZCount, randomOpacityCount;
        float randomStartScaleRange, randomEndScaleRange;
        float randomStartRotXRange, randomEndRotXRange;
        float randomStartRotYRange, randomEndRotYRange;
        float randomStartRotZRange, randomEndRotZRange;
        float randomStartOpacityRange, randomEndOpacityRange;
        bool randomSEScaleToggle, randomSERotXToggle, randomSERotYToggle, randomSERotZToggle;
        bool billboard, billboardXYZ;
        bool grTerminationToggle;
        bool randomSyScaleToggle;
        bool pSEToggleX, pSEToggleY, pSEToggleZ;
        bool autoOrient, autoOrient2D;
        float randomHueRange, randomSatRange, randomLumRange;
        int randomColorCount;
        bool randomColorToggle;
        int calculationType;
        float forcePitch, forceYaw, forceVelocity, forceRoll;
        int fps;
        float forceRandomPitch, forceRandomYaw, forceRandomRoll, forceRandomVelocity;
        int forceRandomCount;
        bool floorToggle;
        float floorY, floorWaitTime, floorFadeTime;
        bool zSortToggle;
        int floorJudgementType;
        int floorActionType;

        bool focusToggle, focusFadeToggle;
        float focusDepth, focusRange, focusMaxBlur, focusFadeMinOpacity, focusFallOffBlur;

        float airResistance;
        float bounceFactor, bounceEnergyLoss, bounceGravity;
        int bounceCount;
        bool loopToggle;
        bool opacityMapToggle;
        float opacityMapMidPoint, opacityMapEase;
        float trailInterval, trailFade, trailScale;
        int trailCount;
        bool trailToggle;
        bool cullingToggle;
        float cullingBuffer;
        float projectWidth = 1920;
        float projectHeight = 1080;
        float imageWidth = 100f;
        float imageHeight = 100f;

        System.Windows.Media.Color startColor;
        System.Windows.Media.Color endColor;

        Vector3 rotation;
        Matrix4x4 camera;

        private readonly List<ItemDrawData> _drawList = new List<ItemDrawData>();

        public ID2D1Image Output => commandList ?? throw new NullReferenceException(nameof(commandList) + " is null");

        public Particles3DEffectProcessor(IGraphicsDevicesAndContext devices, Particles3DEffect item)
        {
            this.item = item;
            this.devices = devices;
        }

        public DrawDescription Update(EffectDescription effectDescription)
        {
            // ===============================================
            // アイテムのX,Y,Z移動などを3D空間に完全追従させるための行列作成
            // ===============================================
            var drawDesc = effectDescription.DrawDescription;
            float d2r = (float)Math.PI / 180.0f;
            Matrix4x4 localRotation = Matrix4x4.CreateRotationZ(drawDesc.Rotation.Z * d2r) *
                                       Matrix4x4.CreateRotationY(drawDesc.Rotation.Y * d2r) *
                                       Matrix4x4.CreateRotationX(drawDesc.Rotation.X * d2r);

            Matrix4x4 worldTranslation = Matrix4x4.CreateTranslation(drawDesc.Draw.X, drawDesc.Draw.Y, drawDesc.Draw.Z);
            Matrix4x4 perspective = new Matrix4x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, -0.001f, 0, 0, 0, 1);
            Matrix4x4 m_fullProjection = localRotation * worldTranslation * drawDesc.Camera * perspective;

            // YMMの仕様に合わせて中心点座標を再構築
            Vector4 projectedCenter = Vector4.Transform(Vector3.Zero, m_fullProjection);
            float tx = 0, ty = 0, tz = 0;
            if (Math.Abs(projectedCenter.W) > 1e-6f)
            {
                tx = projectedCenter.X / projectedCenter.W;
                ty = projectedCenter.Y / projectedCenter.W;
                tz = projectedCenter.W;
            }
            Matrix4x4 m_adjustment = Matrix4x4.CreateTranslation(-tx, -ty, 0);
            Matrix4x4 m_internalDraw = m_fullProjection * m_adjustment;

            // ビルボード用のカメラ方向
            Matrix4x4 camForBillboard = localRotation * drawDesc.Camera;
            Vector3 camForward = new Vector3(camForBillboard.M31, camForBillboard.M32, camForBillboard.M33);
            if (camForward == Vector3.Zero) camForward = new Vector3(0, 0, 1);
            camForward = Vector3.Normalize(camForward);
            float cameraYaw = (float)Math.Atan2(camForward.X, camForward.Z);

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var count = (int)item.Count.GetValue(frame, length, fps);
            var reverseDraw = (int)item.ReverseDraw.GetValue(frame, length, fps);
            var fixedDraw = item.FixedDraw;

            var startx = (float)item.StartX.GetValue(frame, length, fps);
            var starty = (float)item.StartY.GetValue(frame, length, fps);
            var startz = (float)item.StartZ.GetValue(frame, length, fps);

            var endx = (float)item.EndX.GetValue(frame, length, fps);
            var endy = (float)item.EndY.GetValue(frame, length, fps);
            var endz = (float)item.EndZ.GetValue(frame, length, fps);

            var startRotationX = (float)item.StartRotationX.GetValue(frame, length, fps);
            var startRotationY = (float)item.StartRotationY.GetValue(frame, length, fps);
            var startRotationZ = (float)item.StartRotationZ.GetValue(frame, length, fps);

            var endRotationX = (float)item.EndRotationX.GetValue(frame, length, fps);
            var endRotationY = (float)item.EndRotationY.GetValue(frame, length, fps);
            var endRotationZ = (float)item.EndRotationZ.GetValue(frame, length, fps);

            var scaleStartx = (float)item.ScaleStartX.GetValue(frame, length, fps);
            var scaleStarty = (float)item.ScaleStartY.GetValue(frame, length, fps);
            var scaleStartz = (float)item.ScaleStartZ.GetValue(frame, length, fps);

            var scalex = (float)item.ScaleX.GetValue(frame, length, fps);
            var scaley = (float)item.ScaleY.GetValue(frame, length, fps);
            var scalez = (float)item.ScaleZ.GetValue(frame, length, fps);

            var startopacity = (float)item.StartOpacity.GetValue(frame, length, fps);
            var endopacity = (float)item.EndOpacity.GetValue(frame, length, fps);
            var opacityMapToggle = item.OpacityMapToggle;
            var opacityMapMidPoint = (float)item.OpacityMapMidPoint.GetValue(frame, length, fps);
            var opacityMapEase = (float)item.OpacityMapEase.GetValue(frame, length, fps);

            var delaytime = (float)item.DelayTime.GetValue(frame, length, fps);

            var randomToggleX = item.RandomToggleX;
            var randomSEToggleX = item.RandomSEToggleX;
            var randomXCount = (int)item.RandomXCount.GetValue(frame, length, fps);
            var randomStartXRange = (float)item.RandomStartXRange.GetValue(frame, length, fps);
            var randomEndXRange = (float)item.RandomEndXRange.GetValue(frame, length, fps);
            var randomToggleY = item.RandomToggleY;
            var randomSEToggleY = item.RandomSEToggleY;
            var randomYCount = (int)item.RandomYCount.GetValue(frame, length, fps);
            var randomStartYRange = (float)item.RandomStartYRange.GetValue(frame, length, fps);
            var randomEndYRange = (float)item.RandomEndYRange.GetValue(frame, length, fps);
            var randomToggleZ = item.RandomToggleZ;
            var randomSEToggleZ = item.RandomSEToggleZ;
            var randomZCount = (int)item.RandomZCount.GetValue(frame, length, fps);
            var randomStartZRange = (float)item.RandomStartZRange.GetValue(frame, length, fps);
            var randomEndZRange = (float)item.RandomEndZRange.GetValue(frame, length, fps);
            var randomScaleToggle = item.RandomScaleToggle;
            var randomScaleCount = (int)item.RandomScaleCount.GetValue(frame, length, fps);
            var randomStartScaleRange = (float)item.RandomStartScaleRange.GetValue(frame, length, fps);
            var randomEndScaleRange = (float)item.RandomEndScaleRange.GetValue(frame, length, fps);
            var randomSEScaleToggle = item.RandomSEScaleToggle;
            var randomRotXToggle = item.RandomRotXToggle;
            var randomRotXCount = (int)item.RandomRotXCount.GetValue(frame, length, fps);
            var randomStartRotXRange = (float)item.RandomStartRotXRange.GetValue(frame, length, fps);
            var randomEndRotXRange = (float)item.RandomEndRotXRange.GetValue(frame, length, fps);
            var randomSERotXToggle = item.RandomSERotXToggle;
            var randomRotYToggle = item.RandomRotYToggle;
            var randomRotYCount = (int)item.RandomRotYCount.GetValue(frame, length, fps);
            var randomStartRotYRange = (float)item.RandomStartRotYRange.GetValue(frame, length, fps);
            var randomEndRotYRange = (float)item.RandomEndRotYRange.GetValue(frame, length, fps);
            var randomSERotYToggle = item.RandomSERotYToggle;
            var randomRotZToggle = item.RandomRotZToggle;
            var randomRotZCount = (int)item.RandomRotZCount.GetValue(frame, length, fps);
            var randomStartRotZRange = (float)item.RandomStartRotZRange.GetValue(frame, length, fps);
            var randomEndRotZRange = (float)item.RandomEndRotZRange.GetValue(frame, length, fps);
            var randomSERotZToggle = item.RandomSERotZToggle;
            var randomOpacityToggle = item.RandomOpacityToggle;
            var randomOpacityCount = (int)item.RandomOpacityCount.GetValue(frame, length, fps);
            var randomStartOpacityRange = (float)item.RandomStartOpacityRange.GetValue(frame, length, fps);
            var randomEndOpacityRange = (float)item.RandomEndOpacityRange.GetValue(frame, length, fps);

            var randomSyScaleToggle = item.RandomSyScaleToggle;
            var randomSeed = (int)item.RandomSeed.GetValue(frame, length, fps);

            var rotation = effectDescription.DrawDescription.Rotation;
            var camera = effectDescription.DrawDescription.Camera;

            var cycleTime = (float)item.CycleTime.GetValue(frame, length, fps);
            var travelTime = (float)item.TravelTime.GetValue(frame, length, fps);

            float delayFramesPerItem = delaytime * fps / 1000.0f;

            var gravityX = (float)item.GravityX.GetValue(frame, length, fps);
            var gravityY = (float)item.GravityY.GetValue(frame, length, fps);
            var gravityZ = (float)item.GravityZ.GetValue(frame, length, fps);
            var grTerminationToggle = item.GrTerminationToggle;

            var curveRange = (float)item.CurveRange.GetValue(frame, length, fps);
            var curveToggle = item.CurveToggle;

            var fixedTrajectory = item.FixedTrajectory;

            var billboard = item.BillboardDraw;
            var billboardXYZ = item.BillboardXYZDraw;

            var pSEToggleX = item.PSEToggleX;
            var pSEToggleY = item.PSEToggleY;
            var pSEToggleZ = item.PSEToggleZ;

            var startColor = item.StartColor;
            var endColor = item.EndColor;

            var autoOrient = item.AutoOrient;
            var autoOrient2D = item.AutoOrient2D;

            var randomColorToggle = item.RandomColorToggle;
            var randomColorCount = (int)item.RandomColorCount.GetValue(frame, length, fps);
            var randomHueRange = (float)item.RandomHueRange.GetValue(frame, length, fps);
            var randomSatRange = (float)item.RandomSatRange.GetValue(frame, length, fps);
            var randomLumRange = (float)item.RandomLumRange.GetValue(frame, length, fps);

            var calculationType = (int)item.CalculationType;

            var forcePitch = (float)item.ForcePitch.GetValue(frame, length, fps);
            var forceYaw = (float)item.ForceYaw.GetValue(frame, length, fps);
            var forceRoll = (float)item.ForceRoll.GetValue(frame, length, fps);
            var forceVelocity = (float)item.ForceVelocity.GetValue(frame, length, fps);
            var forceRandomCount = (int)item.ForceRandomCount.GetValue(frame, length, fps);
            var forceRandomPitch = (float)item.ForceRandomPitch.GetValue(frame, length, fps);
            var forceRandomYaw = (float)item.ForceRandomYaw.GetValue(frame, length, fps);
            var forceRandomRoll = (float)item.ForceRandomRoll.GetValue(frame, length, fps);
            var forceRandomVelocity = (float)item.ForceRandomVelocity.GetValue(frame, length, fps);

            var floorToggle = item.FloorToggle;
            var floorY = (float)item.FloorY.GetValue(frame, length, fps);
            var floorWaitTime = (float)item.FloorWaitTime.GetValue(frame, length, fps);
            var floorFadeTime = (float)item.FloorFadeTime.GetValue(frame, length, fps);
            var floorJudgementType = (int)item.FloorJudgementType;
            var floorActionType = (int)item.FloorActionType;

            var focusToggle = item.FocusToggle;
            var focusFadeToggle = item.FocusFadeToggle;
            var focusDepth = (float)item.FocusDepth.GetValue(frame, length, fps);
            var focusRange = (float)item.FocusRange.GetValue(frame, length, fps);
            var focusMaxBlur = (float)item.FocusMaxBlur.GetValue(frame, length, fps);
            var focusFadeMinOpacity = (float)item.FocusFadeMinOpacity.GetValue(frame, length, fps);
            var focusFallOffBlur = (float)item.FocusFallOffBlur.GetValue(frame, length, fps);

            var bounceFactor = (float)item.BounceFactor.GetValue(frame, length, fps);
            var bounceEnergyLoss = (float)item.BounceEnergyLoss.GetValue(frame, length, fps);
            var bounceGravity = (float)item.BounceGravity.GetValue(frame, length, fps);
            var bounceCount = (int)item.BounceCount.GetValue(frame, length, fps);

            var zSortToggle = item.ZSortToggle;
            var airResistance = (float)item.AirResistance.GetValue(frame, length, fps);
            var loopToggle = item.LoopToggle;

            var trailToggle = item.TrailToggle;
            var trailCount = (int)item.TrailCount.GetValue(frame, length, fps);
            var trailInterval = (float)item.TrailInterval.GetValue(frame, length, fps);
            var trailFade = (float)item.TrailFade.GetValue(frame, length, fps);
            var trailScale = (float)item.TrailScale.GetValue(frame, length, fps);

            var cullingToggle = item.CullingToggle;
            var cullingBuffer = (float)item.CullingBuffer.GetValue(frame, length, fps);

            var serializedEffects = Json.GetJsonText(item.VideoEffects);

            var sourceType = (int)item.SourceType;
            var directory = item.Directory ?? "";

            int SafeRandomXCount = Math.Max(1, randomXCount);
            int numberOfGroups = (int)Math.Ceiling((double)count / SafeRandomXCount);
            int arraySize = numberOfGroups + 1;

            if (isFirst)
            {
                UpdateProjectInfo();
            }

            bool sourceChanged = this.sourceType != sourceType || this.directory != directory;

            if ((isFirst || IsInputChanged) && this.input != null)
            {
                var imageSize = this.devices.DeviceContext.GetImageLocalBounds(this.input);
                this.imageWidth = imageSize.Right - imageSize.Left;
                this.imageHeight = imageSize.Bottom - imageSize.Top;

                if (this.imageWidth <= 0) this.imageWidth = 100f;
                if (this.imageHeight <= 0) this.imageHeight = 100f;
            }

            bool arrayNeedsUpdate = isFirst || sourceChanged || this.randomSeed != randomSeed ||
                this.randomXCount != randomXCount || this.randomStartXRange != randomStartXRange || this.randomEndXRange != randomEndXRange || this.endx != endx || this.randomSEToggleX != randomSEToggleX ||
                this.randomYCount != randomYCount || this.randomStartYRange != randomStartYRange || this.randomEndYRange != randomEndYRange || this.endy != endy || this.randomSEToggleY != randomSEToggleY ||
                this.randomZCount != randomZCount || this.randomStartZRange != randomStartZRange || this.randomEndZRange != randomEndZRange || this.endz != endz || this.randomSEToggleZ != randomSEToggleZ ||
                this.curveRange != curveRange || this.curveToggle != curveToggle || this.fixedTrajectory != fixedTrajectory ||
                this.gravityX != gravityX || this.gravityY != gravityY || this.gravityZ != gravityZ || this.grTerminationToggle != grTerminationToggle ||
                this.endopacity != endopacity || this.startopacity != startopacity || this.startRotationX != startRotationX || this.startRotationY != startRotationY || this.startRotationZ != startRotationZ ||
                this.endRotationX != endRotationX || this.endRotationY != endRotationY || this.endRotationZ != endRotationZ ||
                this.scalex != scalex || this.scaley != scaley || this.scalez != scalez || this.randomSyScaleToggle != randomSyScaleToggle ||
                this.randomScaleCount != randomScaleCount || this.randomStartScaleRange != randomStartScaleRange || this.randomEndScaleRange != randomEndScaleRange || this.randomSEScaleToggle != randomSEScaleToggle ||
                this.randomRotXCount != randomRotXCount || this.randomStartRotXRange != randomStartRotXRange || this.randomEndRotXRange != randomEndRotXRange || this.randomSERotXToggle != randomSERotXToggle ||
                this.randomRotYCount != randomRotYCount || this.randomStartRotYRange != randomStartRotYRange || this.randomEndRotYRange != randomEndRotYRange || this.randomSERotYToggle != randomSERotYToggle ||
                this.randomRotZCount != randomRotZCount || this.randomStartRotZRange != randomStartRotZRange || this.randomEndRotZRange != randomEndRotZRange || this.randomSERotZToggle != randomSERotZToggle ||
                this.randomOpacityCount != randomOpacityCount || this.randomStartOpacityRange != randomStartOpacityRange || this.randomEndOpacityRange != randomEndOpacityRange ||
                this.billboard != billboard || this.billboardXYZ != billboardXYZ || this.startx != startx || this.starty != starty || this.startz != startz ||
                this.scaleStartz != scaleStartz || this.scaleStartx != scaleStartx || this.scaleStarty != scaleStarty ||
                this.pSEToggleX != pSEToggleX || this.pSEToggleY != pSEToggleY || this.pSEToggleZ != pSEToggleZ || this.autoOrient != autoOrient || this.autoOrient2D != autoOrient2D ||
                this.randomHueRange != randomHueRange || this.randomSatRange != randomSatRange || this.randomLumRange != randomLumRange || this.randomColorCount != randomColorCount || this.randomColorToggle != randomColorToggle ||
                this.calculationType != calculationType || this.forcePitch != forcePitch || this.forceYaw != forceYaw || this.forceVelocity != forceVelocity || this.forceRoll != forceRoll || this.fps != fps ||
                this.forceRandomCount != forceRandomCount || this.forceRandomPitch != forceRandomPitch || this.forceRandomRoll != forceRandomRoll || this.forceRandomYaw != forceRandomYaw || this.forceRandomVelocity != forceRandomVelocity ||
                this.floorActionType != floorActionType || this.floorJudgementType != floorJudgementType || this.floorToggle != floorToggle || this.floorY != floorY || this.floorWaitTime != floorWaitTime || this.floorFadeTime != floorFadeTime ||
                this.zSortToggle != zSortToggle || this.airResistance != airResistance || this.bounceFactor != bounceFactor || this.bounceEnergyLoss != bounceEnergyLoss || this.bounceGravity != bounceGravity ||
                this.focusToggle != focusToggle || this.focusFadeToggle != focusFadeToggle || this.focusDepth != focusDepth || this.focusRange != focusRange || this.focusMaxBlur != focusMaxBlur || this.focusFadeMinOpacity != focusFadeMinOpacity || this.focusFallOffBlur != focusFallOffBlur ||
                this.loopToggle != loopToggle || this.opacityMapMidPoint != opacityMapMidPoint || this.opacityMapToggle != opacityMapToggle || this.opacityMapEase != opacityMapEase || this.bounceCount != bounceCount;

            if (isFirst || IsInputChanged || sourceChanged || this.itemDrawX != drawDesc.Draw.X || this.itemDrawY != drawDesc.Draw.Y || this.itemDrawZ != drawDesc.Draw.Z ||
                this.frame != frame || this.count != count || this.startx != startx || this.starty != starty || this.startz != startz ||
                this.endx != endx || this.endy != endy || this.endz != endz ||
                this.rotation != rotation || this.camera != camera || this.scalex != scalex || this.scaley != scaley || this.scalez != scalez ||
                this.startopacity != startopacity || this.endopacity != endopacity ||
                this.startRotationX != startRotationX || this.startRotationY != startRotationY || this.startRotationZ != startRotationZ ||
                this.endRotationX != endRotationX || this.endRotationY != endRotationY || this.endRotationZ != endRotationZ || this.delaytime != delaytime ||
                this.reverseDraw != reverseDraw || this.fixedDraw != fixedDraw || this.randomSeed != randomSeed ||
                this.randomXCount != randomXCount || this.randomStartXRange != randomStartXRange || this.randomEndXRange != randomEndXRange || this.randomToggleX != randomToggleX || this.randomSEToggleX != randomSEToggleX ||
                this.randomToggleY != randomToggleY || this.randomYCount != randomYCount || this.randomStartYRange != randomStartYRange || this.randomEndYRange != randomEndYRange || this.randomSEToggleY != randomSEToggleY ||
                this.randomToggleZ != randomToggleZ || this.randomZCount != randomZCount || this.randomStartZRange != randomStartZRange || this.randomEndZRange != randomEndZRange || this.randomSEToggleZ != randomSEToggleZ ||
                this.gravityX != gravityX || this.gravityY != gravityY || this.gravityZ != gravityZ || this.curveRange != curveRange || this.curveToggle != curveToggle ||
                this.cycleTime != cycleTime || this.travelTime != travelTime || this.fixedTrajectory != fixedTrajectory ||
                this.randomScaleToggle != randomScaleToggle || this.randomScaleCount != randomScaleCount || this.randomStartScaleRange != randomStartScaleRange || this.randomEndScaleRange != randomEndScaleRange || this.randomSEScaleToggle != randomSEScaleToggle ||
                this.randomRotXToggle != randomRotXToggle || this.randomRotXCount != randomRotXCount || this.randomStartRotXRange != randomStartRotXRange || this.randomEndRotXRange != randomEndRotXRange || this.randomSERotXToggle != randomSERotXToggle ||
                this.randomRotYToggle != randomRotYToggle || this.randomRotYCount != randomRotYCount || this.randomStartRotYRange != randomStartRotYRange || this.randomEndRotYRange != randomEndRotYRange || this.randomSERotYToggle != randomSERotYToggle ||
                this.randomRotZToggle != randomRotZToggle || this.randomRotZCount != randomRotZCount || this.randomStartRotZRange != randomStartRotZRange || this.randomEndRotZRange != randomEndRotZRange || this.randomSERotZToggle != randomSERotZToggle ||
                this.randomOpacityToggle != randomOpacityToggle || this.randomOpacityCount != randomOpacityCount || this.randomStartOpacityRange != randomStartOpacityRange || this.randomEndOpacityRange != randomEndOpacityRange ||
                this.billboard != billboard || this.billboardXYZ != billboardXYZ || this.randomSyScaleToggle != randomSyScaleToggle ||
                this.pSEToggleX != pSEToggleX || this.pSEToggleY != pSEToggleY || this.pSEToggleZ != pSEToggleZ || this.grTerminationToggle != grTerminationToggle ||
                this.startColor != startColor || this.endColor != endColor || this.scaleStartx != scaleStartx || this.scaleStarty != scaleStarty || this.scaleStartz != scaleStartz ||
                this.autoOrient != autoOrient || this.autoOrient2D != autoOrient2D || this.randomHueRange != randomHueRange || this.randomSatRange != randomSatRange || this.randomLumRange != randomLumRange ||
                this.randomColorCount != randomColorCount || this.randomColorToggle != randomColorToggle || this.calculationType != calculationType || this.forcePitch != forcePitch || this.forceYaw != forceYaw || this.forceRoll != forceRoll || this.forceVelocity != forceVelocity ||
                this.fps != fps || this.forceRandomCount != forceRandomCount || this.forceRandomPitch != forceRandomPitch || this.forceRandomRoll != forceRandomRoll || this.forceRandomYaw != forceRandomYaw || this.forceRandomVelocity != forceRandomVelocity ||
                this.floorActionType != floorActionType || this.floorJudgementType != floorJudgementType || this.floorToggle != floorToggle || this.floorY != floorY || this.floorWaitTime != floorWaitTime || this.floorFadeTime != floorFadeTime ||
                this.zSortToggle != zSortToggle || this.airResistance != airResistance || this.bounceFactor != bounceFactor || this.bounceEnergyLoss != bounceEnergyLoss || this.bounceGravity != bounceGravity ||
                this.focusToggle != focusToggle || this.focusFadeToggle != focusFadeToggle || this.focusDepth != focusDepth || this.focusRange != focusRange || this.focusMaxBlur != focusMaxBlur || this.focusFadeMinOpacity != focusFadeMinOpacity || this.focusFallOffBlur != focusFallOffBlur ||
                this.loopToggle != loopToggle || this.opacityMapMidPoint != opacityMapMidPoint || this.opacityMapToggle != opacityMapToggle || this.opacityMapEase != opacityMapEase || this.bounceCount != bounceCount ||
                this.trailToggle != trailToggle || this.trailCount != trailCount || this.trailInterval != trailInterval || this.trailFade != trailFade || this.trailScale != trailScale ||
                this.cullingToggle != cullingToggle || this.cullingBuffer != cullingBuffer || this.oldSerializedEffects != serializedEffects)
            {
                if (sourceChanged || isFirst)
                {
                    // 同時に複数の処理が走らないようにロックをかける
                    lock (_imageLoadLock)
                    {
                        if (this.sourceType != sourceType || this.directory != directory || this.imageSources == null)
                        {
                            // 古い画像を確実に Dispose する
                            if (this.imageSources != null)
                            {
                                for (int idx = 0; idx < this.imageSources.Length; idx++)
                                {
                                    this.imageSources[idx]?.Dispose();
                                    this.imageSources[idx] = null;
                                }
                                this.imageSources = null;
                            }
                            this.imageFiles = null;

                            if (sourceType == 1 && !string.IsNullOrEmpty(directory) && System.IO.Directory.Exists(directory))
                            {
                                var files = new List<string>();
                                foreach (var file in System.IO.Directory.GetFiles(directory))
                                {
                                    var fileType = SettingsBase<FileSettings>.Default.FileExtensions.GetFileType(file);
                                    if (fileType.HasFlag(FileType.画像))
                                    {
                                        files.Add(file);
                                    }
                                }
                                this.imageFiles = files.ToArray();
                                this.imageSources = new IImageFileSource[this.imageFiles.Length];

                                for (int idx = 0; idx < this.imageFiles.Length; idx++)
                                {
                                    try
                                    {
                                        // disposer.Collect は使わず、配列に入れるだけでOKです
                                        this.imageSources[idx] = ImageFileSourceFactory.Create(this.devices, this.imageFiles[idx]);
                                    }
                                    catch { }
                                }
                            }

                            this.sourceType = sourceType;
                            this.directory = directory;
                        }
                    }
                }

                this.itemDrawX = drawDesc.Draw.X;
                this.itemDrawY = drawDesc.Draw.Y;
                this.itemDrawZ = drawDesc.Draw.Z;
                this.frame = frame;
                this.fps = fps;
                this.reverseDraw = reverseDraw;
                this.fixedDraw = fixedDraw;
                this.count = count;
                this.startx = startx;
                this.starty = starty;
                this.startz = startz;
                this.endx = endx;
                this.endy = endy;
                this.endz = endz;
                this.startRotationX = startRotationX;
                this.startRotationY = startRotationY;
                this.startRotationZ = startRotationZ;
                this.endRotationX = endRotationX;
                this.endRotationY = endRotationY;
                this.endRotationZ = endRotationZ;
                this.scalex = scalex;
                this.scaley = scaley;
                this.scalez = scalez;
                this.randomToggleX = randomToggleX;
                this.randomSEToggleX = randomSEToggleX;
                this.randomSeed = randomSeed;
                this.randomXCount = randomXCount;
                this.randomStartXRange = randomStartXRange;
                this.randomEndXRange = randomEndXRange;
                this.randomToggleY = randomToggleY;
                this.randomSEToggleY = randomSEToggleY;
                this.randomYCount = randomYCount;
                this.randomStartYRange = randomStartYRange;
                this.randomEndYRange = randomEndYRange;
                this.randomToggleZ = randomToggleZ;
                this.randomSEToggleZ = randomSEToggleZ;
                this.randomZCount = randomZCount;
                this.randomStartZRange = randomStartZRange;
                this.randomEndZRange = randomEndZRange;
                this.gravityX = gravityX;
                this.gravityY = gravityY;
                this.gravityZ = gravityZ;
                this.curveRange = curveRange;
                this.curveToggle = curveToggle;
                this.startopacity = startopacity;
                this.endopacity = endopacity;
                this.delaytime = delaytime;
                this.rotation = rotation;
                this.camera = camera;
                this.cycleTime = cycleTime;
                this.travelTime = travelTime;
                this.fixedTrajectory = fixedTrajectory;
                this.randomScaleToggle = randomScaleToggle;
                this.randomScaleCount = randomScaleCount;
                this.randomStartScaleRange = randomStartScaleRange;
                this.randomEndScaleRange = randomEndScaleRange;
                this.randomSEScaleToggle = randomSEScaleToggle;
                this.randomRotXToggle = randomRotXToggle;
                this.randomRotXCount = randomRotXCount;
                this.randomStartRotXRange = randomStartRotXRange;
                this.randomEndRotXRange = randomEndRotXRange;
                this.randomSERotXToggle = randomSERotXToggle;
                this.randomRotYToggle = randomRotYToggle;
                this.randomRotYCount = randomRotYCount;
                this.randomStartRotYRange = randomStartRotYRange;
                this.randomEndRotYRange = randomEndRotYRange;
                this.randomSERotYToggle = randomSERotYToggle;
                this.randomRotZToggle = randomRotZToggle;
                this.randomRotZCount = randomRotZCount;
                this.randomStartRotZRange = randomStartRotZRange;
                this.randomEndRotZRange = randomEndRotZRange;
                this.randomSERotZToggle = randomSERotZToggle;
                this.randomOpacityToggle = randomOpacityToggle;
                this.randomOpacityCount = randomOpacityCount;
                this.randomStartOpacityRange = randomStartOpacityRange;
                this.randomEndOpacityRange = randomEndOpacityRange;
                this.billboardXYZ = billboardXYZ;
                this.billboard = billboard;
                this.grTerminationToggle = grTerminationToggle;
                this.randomSyScaleToggle = randomSyScaleToggle;
                this.pSEToggleX = pSEToggleX;
                this.pSEToggleY = pSEToggleY;
                this.pSEToggleZ = pSEToggleZ;
                this.startColor = startColor;
                this.endColor = endColor;
                this.scaleStartx = scaleStartx;
                this.scaleStarty = scaleStarty;
                this.scaleStartz = scaleStartz;
                this.autoOrient = autoOrient;
                this.autoOrient2D = autoOrient2D;
                this.randomColorCount = randomColorCount;
                this.randomHueRange = randomHueRange;
                this.randomSatRange = randomSatRange;
                this.randomLumRange = randomLumRange;
                this.randomColorToggle = randomColorToggle;
                this.calculationType = calculationType;
                this.forcePitch = forcePitch;
                this.forceYaw = forceYaw;
                this.forceRoll = forceRoll;
                this.forceVelocity = forceVelocity;
                this.forceRandomVelocity = forceRandomVelocity;
                this.forceRandomPitch = forceRandomPitch;
                this.forceRandomYaw = forceRandomYaw;
                this.forceRandomRoll = forceRandomRoll;
                this.forceRandomCount = forceRandomCount;
                this.floorActionType = floorActionType;
                this.floorJudgementType = floorJudgementType;
                this.floorY = floorY;
                this.floorFadeTime = floorFadeTime;
                this.floorWaitTime = floorWaitTime;
                this.floorToggle = floorToggle;
                this.zSortToggle = zSortToggle;
                this.focusToggle = focusToggle;
                this.focusFadeToggle = focusFadeToggle;
                this.focusDepth = focusDepth;
                this.focusRange = focusRange;
                this.focusMaxBlur = focusMaxBlur;
                this.focusFadeMinOpacity = focusFadeMinOpacity;
                this.focusFallOffBlur = focusFallOffBlur;
                this.airResistance = airResistance;
                this.bounceFactor = bounceFactor;
                this.bounceEnergyLoss = bounceEnergyLoss;
                this.bounceGravity = bounceGravity;
                this.bounceCount = bounceCount;
                this.loopToggle = loopToggle;
                this.opacityMapToggle = opacityMapToggle;
                this.opacityMapMidPoint = opacityMapMidPoint;
                this.opacityMapEase = opacityMapEase;
                this.trailToggle = trailToggle;
                this.trailCount = trailCount;
                this.trailInterval = trailInterval;
                this.trailFade = trailFade;
                this.trailScale = trailScale;
                this.cullingToggle = cullingToggle;
                this.cullingBuffer = cullingBuffer;
                this.oldSerializedEffects = serializedEffects;

                if (pSEToggleX) this.endx = this.startx + this.endx;
                if (pSEToggleY) this.endy = this.starty + this.endy;
                if (pSEToggleZ) this.endz = this.startz + this.endz;

                int numberOfGroupsForce = (int)Math.Ceiling((double)this.count / Math.Max(1, this.forceRandomCount));

                var dc = devices.DeviceContext;

                /*
                int numberofGroupsColor = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomColorCount));
                if (this.randomColorToggle && numberofGroupsColor > this.hueEffects.Count)
                {
                    int needed = numberofGroupsColor - this.hueEffects.Count;
                    for (int i = 0; i < needed; i++)
                    {
                        this.hueEffects.Add(new Particles3DHueCustomEffect(this.devices));
                    }
                }
                */

                if (arrayNeedsUpdate)
                {
                    staticRng = new Random(this.randomSeed);

                    EnsureArraySize(ref this.curveFactorArray, this.count);

                    for (int i = 0; i < this.count; i++)
                    {
                        float factor = 1.0f + ((float)staticRng.NextDouble() * curveRange - (curveRange / 2.0f));
                        this.curveFactorArray[i] = factor;
                    }
                    if (this.fixedTrajectory)
                    {
                        EnsureArraySize(ref this.fixedPositionStartXArray, this.count);
                        EnsureArraySize(ref this.fixedPositionStartYArray, this.count);
                        EnsureArraySize(ref this.fixedPositionStartZArray, this.count);
                        EnsureArraySize(ref this.fixedPositionEndXArray, this.count);
                        EnsureArraySize(ref this.fixedPositionEndYArray, this.count);
                        EnsureArraySize(ref this.fixedPositionEndZArray, this.count);
                        EnsureArraySize(ref this.fixedGravityXArray, this.count);
                        EnsureArraySize(ref this.fixedGravityYArray, this.count);
                        EnsureArraySize(ref this.fixedGravityZArray, this.count);
                        EnsureArraySize(ref this.fixedScaleStartXArray, this.count);
                        EnsureArraySize(ref this.fixedScaleStartYArray, this.count);
                        EnsureArraySize(ref this.fixedScaleStartZArray, this.count);
                        EnsureArraySize(ref this.fixedScaleEndXArray, this.count);
                        EnsureArraySize(ref this.fixedScaleEndYArray, this.count);
                        EnsureArraySize(ref this.fixedScaleEndZArray, this.count);
                        EnsureArraySize(ref this.fixedRotationStartXArray, this.count);
                        EnsureArraySize(ref this.fixedRotationStartYArray, this.count);
                        EnsureArraySize(ref this.fixedRotationStartZArray, this.count);
                        EnsureArraySize(ref this.fixedRotationEndXArray, this.count);
                        EnsureArraySize(ref this.fixedRotationEndYArray, this.count);
                        EnsureArraySize(ref this.fixedRotationEndZArray, this.count);
                        EnsureArraySize(ref this.fixedOpacityStartArray, this.count);
                        EnsureArraySize(ref this.fixedOpacityMidArray, this.count);
                        EnsureArraySize(ref this.fixedOpacityEndArray, this.count);
                        EnsureArraySize(ref this.fixedForcePitchArray, this.count);
                        EnsureArraySize(ref this.fixedForceYawArray, this.count);
                        EnsureArraySize(ref this.fixedForceRollArray, this.count);
                        EnsureArraySize(ref this.fixedForceVelocityArray, this.count);

                        Parallel.For(0, this.count, i =>
                        {
                            float T_launch_float = i * this.cycleTime;
                            long T_launch_long = (long)T_launch_float;

                            float launch_StartX = (float)item.StartX.GetValue(T_launch_long, length, fps);
                            float launch_StartY = (float)item.StartY.GetValue(T_launch_long, length, fps);
                            float launch_StartZ = (float)item.StartZ.GetValue(T_launch_long, length, fps);

                            this.fixedPositionStartXArray[i] = launch_StartX;
                            this.fixedPositionStartYArray[i] = launch_StartY;
                            this.fixedPositionStartZArray[i] = launch_StartZ;

                            float launch_EndX = (float)item.EndX.GetValue(T_launch_long, length, fps);
                            float launch_EndY = (float)item.EndY.GetValue(T_launch_long, length, fps);
                            float launch_EndZ = (float)item.EndZ.GetValue(T_launch_long, length, fps);

                            this.fixedPositionEndXArray[i] = launch_EndX;
                            this.fixedPositionEndYArray[i] = launch_EndY;
                            this.fixedPositionEndZArray[i] = launch_EndZ;

                            if (pSEToggleX) this.fixedPositionEndXArray[i] = launch_StartX + launch_EndX;
                            if (pSEToggleY) this.fixedPositionEndYArray[i] = launch_StartY + launch_EndY;
                            if (pSEToggleZ) this.fixedPositionEndZArray[i] = launch_StartZ + launch_EndZ;

                            this.fixedGravityXArray[i] = (float)item.GravityX.GetValue(T_launch_long, length, fps);
                            this.fixedGravityYArray[i] = (float)item.GravityY.GetValue(T_launch_long, length, fps);
                            this.fixedGravityZArray[i] = (float)item.GravityZ.GetValue(T_launch_long, length, fps);

                            this.fixedScaleStartXArray[i] = (float)item.ScaleStartX.GetValue(T_launch_long, length, fps);
                            this.fixedScaleStartYArray[i] = (float)item.ScaleStartY.GetValue(T_launch_long, length, fps);
                            this.fixedScaleStartZArray[i] = (float)item.ScaleStartZ.GetValue(T_launch_long, length, fps);
                            this.fixedScaleEndXArray[i] = (float)item.ScaleX.GetValue(T_launch_long, length, fps);
                            this.fixedScaleEndYArray[i] = (float)item.ScaleY.GetValue(T_launch_long, length, fps);
                            this.fixedScaleEndZArray[i] = (float)item.ScaleZ.GetValue(T_launch_long, length, fps);

                            this.fixedRotationEndXArray[i] = (float)item.EndRotationX.GetValue(T_launch_long, length, fps);
                            this.fixedRotationEndYArray[i] = (float)item.EndRotationY.GetValue(T_launch_long, length, fps);
                            this.fixedRotationEndZArray[i] = (float)item.EndRotationZ.GetValue(T_launch_long, length, fps);
                            this.fixedRotationStartXArray[i] = (float)item.StartRotationX.GetValue(T_launch_long, length, fps);
                            this.fixedRotationStartYArray[i] = (float)item.StartRotationY.GetValue(T_launch_long, length, fps);
                            this.fixedRotationStartZArray[i] = (float)item.StartRotationZ.GetValue(T_launch_long, length, fps);

                            this.fixedOpacityStartArray[i] = (float)item.StartOpacity.GetValue(T_launch_long, length, fps);
                            this.fixedOpacityMidArray[i] = (float)item.OpacityMapMidPoint.GetValue(T_launch_long, length, fps);
                            this.fixedOpacityEndArray[i] = (float)item.EndOpacity.GetValue(T_launch_long, length, fps);

                            this.fixedForcePitchArray[i] = (float)item.ForcePitch.GetValue(T_launch_long, length, fps);
                            this.fixedForceYawArray[i] = (float)item.ForceYaw.GetValue(T_launch_long, length, fps);
                            this.fixedForceRollArray[i] = (float)item.ForceRoll.GetValue(T_launch_long, length, fps);
                            this.fixedForceVelocityArray[i] = (float)item.ForceVelocity.GetValue(T_launch_long, length, fps);
                        });
                    }

                    if (randomColorToggle)
                    {
                        int numberOfGroupsColor = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomColorCount));
                        EnsureArraySize(ref this.randomHueOffsetArray, numberOfGroupsColor);
                        EnsureArraySize(ref this.randomSatOffsetArray, numberOfGroupsColor);
                        EnsureArraySize(ref this.randomLumOffsetArray, numberOfGroupsColor);

                        float satRange = this.randomSatRange / 100.0f;
                        float lumRange = this.randomLumRange / 100.0f;

                        for (int g = 0; g < numberOfGroupsColor; g++)
                        {
                            float hueOffset = (float)staticRng.NextDouble() * this.randomHueRange - (this.randomHueRange / 2.0f);
                            this.randomHueOffsetArray[g] = hueOffset;
                            float satOffset = (float)staticRng.NextDouble() * satRange - (satRange / 2.0f);
                            this.randomSatOffsetArray[g] = satOffset;
                            float lumOffset = (float)staticRng.NextDouble() * lumRange - (lumRange / 2.0f);
                            this.randomLumOffsetArray[g] = lumOffset;

                            // ▼削除：リストがなくなったのでここは消す
                            // var hueEffect = this.hueEffects[g];
                            // hueEffect.HueShift = hueOffset;
                            // hueEffect.SaturationFactor = 1.0f + satOffset;
                            // hueEffect.LuminanceFactor = 1.0f + lumOffset;
                            // hueEffect.Factor = 1.0f;
                        }
                    }

                    EnsureArraySize(ref this.randomForcePitchArray, numberOfGroupsForce);
                    EnsureArraySize(ref this.randomForceYawArray, numberOfGroupsForce);
                    EnsureArraySize(ref this.randomForceRollArray, numberOfGroupsForce);
                    EnsureArraySize(ref this.randomForceVelocityArray, numberOfGroupsForce);

                    for (int g = 0; g < numberOfGroupsForce; g++)
                    {
                        int i = g * Math.Max(1, this.forceRandomCount);
                        float basePitch = this.forcePitch;
                        float baseYaw = this.forceYaw;
                        float baseRoll = this.forceRoll;
                        float baseVelocity = this.forceVelocity;

                        if (this.fixedTrajectory)
                        {
                            if (this.fixedForcePitchArray != null && i < this.fixedForcePitchArray.Length) basePitch = this.fixedForcePitchArray[i];
                            if (this.fixedForceYawArray != null && i < this.fixedForceYawArray.Length) baseYaw = this.fixedForceYawArray[i];
                            if (this.fixedForceRollArray != null && i < this.fixedForceRollArray.Length) baseRoll = this.fixedForceRollArray[i];
                            if (this.fixedForceVelocityArray != null && i < this.fixedForceVelocityArray.Length) baseVelocity = this.fixedForceVelocityArray[i];
                        }

                        float p_offset = (float)staticRng.NextDouble() * this.forceRandomPitch - (this.forceRandomPitch / 2.0f);
                        float y_offset = (float)staticRng.NextDouble() * this.forceRandomYaw - (this.forceRandomYaw / 2.0f);
                        float r_offset = (float)staticRng.NextDouble() * this.forceRandomRoll - (this.forceRandomRoll / 2.0f);
                        float v_offset = (float)staticRng.NextDouble() * this.forceRandomVelocity - (this.forceRandomVelocity / 2.0f);

                        this.randomForcePitchArray[g] = basePitch + p_offset;
                        this.randomForceYawArray[g] = baseYaw + y_offset;
                        this.randomForceRollArray[g] = baseRoll + r_offset;
                        this.randomForceVelocityArray[g] = baseVelocity + v_offset;
                    }

                    int numberOfGroupsX = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomXCount));
                    EnsureArraySize(ref this.startXArray, numberOfGroupsX);
                    for (int g = 0; g < numberOfGroupsX; g++)
                    {
                        int i = g * Math.Max(1, this.randomXCount);
                        float baseStartX = this.startx;
                        if (this.fixedTrajectory && this.fixedPositionStartXArray != null && this.fixedPositionStartXArray.Length > i)
                            baseStartX = this.fixedPositionStartXArray[i];

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartXRange - (this.randomStartXRange / 2.0f);
                        this.startXArray[g] = baseStartX + randomOffset;
                    }

                    EnsureArraySize(ref this.targetXArray, numberOfGroupsX);
                    for (int g = 0; g < numberOfGroupsX; g++)
                    {
                        int i = g * Math.Max(1, this.randomXCount);
                        float baseEnd = this.endx;
                        if (this.fixedTrajectory && this.fixedPositionEndXArray != null && this.fixedPositionEndXArray.Length > i)
                            baseEnd = this.fixedPositionEndXArray[i];

                        if (this.randomSEToggleX && this.startXArray != null && this.startXArray.Length > g)
                        {
                            float baseStartX = this.startx;
                            if (this.fixedTrajectory && this.fixedPositionStartXArray != null && this.fixedPositionStartXArray.Length > i)
                                baseStartX = this.fixedPositionStartXArray[i];

                            float startXOffset = this.startXArray[g] - baseStartX;
                            baseEnd += startXOffset;
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomEndXRange - (this.randomEndXRange / 2.0f);
                        this.targetXArray[g] = baseEnd + randomOffset;
                    }

                    int numberOfGroupsY = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomYCount));
                    EnsureArraySize(ref this.startYArray, numberOfGroupsY);
                    for (int g = 0; g < numberOfGroupsY; g++)
                    {
                        int i = g * Math.Max(1, this.randomYCount);
                        float baseStartY = this.starty;
                        if (this.fixedTrajectory && this.fixedPositionStartYArray != null && this.fixedPositionStartYArray.Length > i)
                            baseStartY = this.fixedPositionStartYArray[i];

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartYRange - (this.randomStartYRange / 2.0f);
                        this.startYArray[g] = baseStartY + randomOffset;
                    }

                    EnsureArraySize(ref this.targetYArray, numberOfGroupsY);
                    for (int g = 0; g < numberOfGroupsY; g++)
                    {
                        int i = g * Math.Max(1, this.randomYCount);
                        float baseEnd = this.endy;
                        if (this.fixedTrajectory && this.fixedPositionEndYArray != null && this.fixedPositionEndYArray.Length > i)
                            baseEnd = this.fixedPositionEndYArray[i];

                        if (this.randomSEToggleY && this.startYArray != null && this.startYArray.Length > g)
                        {
                            float baseStartY = this.starty;
                            if (this.fixedTrajectory && this.fixedPositionStartYArray != null && this.fixedPositionStartYArray.Length > i)
                                baseStartY = this.fixedPositionStartYArray[i];

                            float startYOffset = this.startYArray[g] - baseStartY;
                            baseEnd += startYOffset;
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomEndYRange - (this.randomEndYRange / 2.0f);
                        this.targetYArray[g] = baseEnd + randomOffset;
                    }

                    int numberOfGroupsZ = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomZCount));
                    EnsureArraySize(ref this.startZArray, numberOfGroupsZ);
                    for (int g = 0; g < numberOfGroupsZ; g++)
                    {
                        int i = g * Math.Max(1, this.randomZCount);
                        float baseStartZ = this.startz;
                        if (this.fixedTrajectory && this.fixedPositionStartZArray != null && this.fixedPositionStartZArray.Length > i)
                            baseStartZ = this.fixedPositionStartZArray[i];

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartZRange - (this.randomStartZRange / 2.0f);
                        this.startZArray[g] = baseStartZ + randomOffset;
                    }

                    EnsureArraySize(ref this.targetZArray, numberOfGroupsZ);
                    for (int g = 0; g < numberOfGroupsZ; g++)
                    {
                        int i = g * Math.Max(1, this.randomZCount);
                        float baseEnd = this.endz;
                        if (this.fixedTrajectory && this.fixedPositionEndZArray != null && this.fixedPositionEndZArray.Length > i)
                            baseEnd = this.fixedPositionEndZArray[i];

                        if (this.randomSEToggleZ && this.startZArray != null && this.startZArray.Length > g)
                        {
                            float baseStartZ = this.startz;
                            if (this.fixedTrajectory && this.fixedPositionStartZArray != null && this.fixedPositionStartZArray.Length > i)
                                baseStartZ = this.fixedPositionStartZArray[i];

                            float startZOffset = this.startZArray[g] - baseStartZ;
                            baseEnd += startZOffset;
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomEndZRange - (this.randomEndZRange / 2.0f);
                        this.targetZArray[g] = baseEnd + randomOffset;
                    }

                    int numberOfGroupsScaleX = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomScaleCount));
                    EnsureArraySize(ref this.startScaleXArray, numberOfGroupsScaleX);
                    EnsureArraySize(ref this.targetScaleXArray, numberOfGroupsScaleX);
                    for (int g = 0; g < numberOfGroupsScaleX; g++)
                    {
                        int i = g * Math.Max(1, this.randomScaleCount);
                        float baseStartX = this.scaleStartx;
                        if (this.fixedTrajectory && this.fixedScaleStartXArray != null && this.fixedScaleStartXArray.Length > i)
                            baseStartX = this.fixedScaleStartXArray[i];

                        float startXOffset = (float)staticRng!.NextDouble() * this.randomStartScaleRange - (this.randomStartScaleRange / 2.0f);
                        this.startScaleXArray[g] = baseStartX + startXOffset;
                    }
                    for (int g = 0; g < numberOfGroupsScaleX; g++)
                    {
                        int i = g * Math.Max(1, this.randomScaleCount);
                        float baseEnd = this.scalex;

                        if (this.fixedTrajectory && this.fixedScaleEndXArray != null && this.fixedScaleEndXArray.Length > i)
                            baseEnd = this.fixedScaleEndXArray[i];

                        if (this.randomSEScaleToggle && this.startScaleXArray != null && this.startScaleXArray.Length > g)
                        {
                            float baseStartX = this.scaleStartx;
                            if (this.fixedTrajectory && this.fixedScaleStartXArray != null && this.fixedScaleStartXArray.Length > i)
                                baseStartX = this.fixedScaleStartXArray[i];

                            float startOffset = this.startScaleXArray[g] - baseStartX;
                            baseEnd += startOffset;
                        }

                        float endXOffset = (float)staticRng.NextDouble() * this.randomEndScaleRange - (this.randomEndScaleRange / 2.0f);
                        this.targetScaleXArray[g] = baseEnd + endXOffset;
                    }

                    int numberOfGroupsScaleY = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomScaleCount));
                    EnsureArraySize(ref this.startScaleYArray, numberOfGroupsScaleY);
                    EnsureArraySize(ref this.targetScaleYArray, numberOfGroupsScaleY);
                    for (int g = 0; g < numberOfGroupsScaleY; g++)
                    {
                        int i = g * Math.Max(1, this.randomScaleCount);
                        float baseStartY = this.scaleStarty;
                        if (this.fixedTrajectory && this.fixedScaleStartYArray != null && this.fixedScaleStartYArray.Length > i)
                            baseStartY = this.fixedScaleStartYArray[i];

                        float startYOffset = (float)staticRng!.NextDouble() * this.randomStartScaleRange - (this.randomStartScaleRange / 2.0f);
                        this.startScaleYArray[g] = baseStartY + startYOffset;
                    }
                    for (int g = 0; g < numberOfGroupsScaleY; g++)
                    {
                        int i = g * Math.Max(1, this.randomScaleCount);
                        float baseEnd = this.scaley;

                        if (this.fixedTrajectory && this.fixedScaleEndYArray != null && this.fixedScaleEndYArray.Length > i)
                            baseEnd = this.fixedScaleEndYArray[i];

                        if (this.randomSEScaleToggle && this.startScaleYArray != null && this.startScaleYArray.Length > g)
                        {
                            float baseStartY = this.scaleStarty;
                            if (this.fixedTrajectory && this.fixedScaleStartYArray != null && this.fixedScaleStartYArray.Length > i)
                                baseStartY = this.fixedScaleStartYArray[i];

                            float startOffset = this.startScaleYArray[g] - baseStartY;
                            baseEnd += startOffset;
                        }

                        float endYOffset = (float)staticRng.NextDouble() * this.randomEndScaleRange - (this.randomEndScaleRange / 2.0f);
                        this.targetScaleYArray[g] = baseEnd + endYOffset;
                    }

                    int numberOfGroupsScaleZ = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomScaleCount));
                    EnsureArraySize(ref this.startScaleZArray, numberOfGroupsScaleZ);
                    EnsureArraySize(ref this.targetScaleZArray, numberOfGroupsScaleZ);
                    for (int g = 0; g < numberOfGroupsScaleZ; g++)
                    {
                        int i = g * Math.Max(1, this.randomScaleCount);
                        float baseStartZ = this.scaleStartz;
                        if (this.fixedTrajectory && this.fixedScaleStartZArray != null && this.fixedScaleStartZArray.Length > i)
                            baseStartZ = this.fixedScaleStartZArray[i];

                        float startZOffset = (float)staticRng!.NextDouble() * this.randomStartScaleRange - (this.randomStartScaleRange / 2.0f);
                        this.startScaleZArray[g] = baseStartZ + startZOffset;
                    }
                    for (int g = 0; g < numberOfGroupsScaleZ; g++)
                    {
                        int i = g * Math.Max(1, this.randomScaleCount);
                        float baseEnd = this.scalez;

                        if (this.fixedTrajectory && this.fixedScaleEndZArray != null && this.fixedScaleEndZArray.Length > i)
                            baseEnd = this.fixedScaleEndZArray[i];

                        if (this.randomSEScaleToggle && this.startScaleZArray != null && this.startScaleZArray.Length > g)
                        {
                            float baseStartZ = this.scaleStartz;
                            if (this.fixedTrajectory && this.fixedScaleStartZArray != null && this.fixedScaleStartZArray.Length > i)
                                baseStartZ = this.fixedScaleStartZArray[i];

                            float startOffset = this.startScaleZArray[g] - baseStartZ;
                            baseEnd += startOffset;
                        }

                        float endZOffset = (float)staticRng.NextDouble() * this.randomEndScaleRange - (this.randomEndScaleRange / 2.0f);
                        this.targetScaleZArray[g] = baseEnd + endZOffset;
                    }

                    if (this.randomSyScaleToggle && this.startScaleXArray != null && this.startScaleYArray != null && this.startScaleZArray != null)
                    {
                        int minLength = Math.Min(this.startScaleXArray.Length,
                                        Math.Min(this.startScaleYArray.Length, this.startScaleZArray.Length));

                        for (int g = 0; g < minLength; g++)
                        {
                            this.startScaleYArray[g] = this.startScaleXArray[g];
                            this.targetScaleYArray[g] = this.targetScaleXArray[g];
                            this.startScaleZArray[g] = this.startScaleXArray[g];
                            this.targetScaleZArray[g] = this.targetScaleXArray[g];
                        }
                    }

                    int numberOfGroupsRotX = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomRotXCount));
                    EnsureArraySize(ref this.startRotationXArray, numberOfGroupsRotX);
                    EnsureArraySize(ref this.targetRotationXArray, numberOfGroupsRotX);
                    for (int g = 0; g < numberOfGroupsRotX; g++)
                    {
                        int i = g * Math.Max(1, this.randomRotXCount);
                        float baseStartX = this.startRotationX;
                        if (this.fixedTrajectory && this.fixedRotationStartXArray != null && this.fixedRotationStartXArray.Length > i)
                            baseStartX = this.fixedRotationStartXArray[i];

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartRotXRange - (this.randomStartRotXRange / 2.0f);
                        this.startRotationXArray[g] = baseStartX + randomOffset;
                    }
                    for (int g = 0; g < numberOfGroupsRotX; g++)
                    {
                        int i = g * Math.Max(1, this.randomRotXCount);
                        float baseEnd = this.endRotationX;
                        if (this.fixedTrajectory && this.fixedRotationEndXArray != null && this.fixedRotationEndXArray.Length > i)
                            baseEnd = this.fixedRotationEndXArray[i];

                        if (this.randomSERotXToggle && this.startRotationXArray != null && this.startRotationXArray.Length > g)
                        {
                            float baseStartX = this.startRotationX;
                            if (this.fixedTrajectory && this.fixedRotationStartXArray != null && this.fixedRotationStartXArray.Length > i)
                                baseStartX = this.fixedRotationStartXArray[i];

                            float startXOffset = this.startRotationXArray[g] - baseStartX;
                            baseEnd += startXOffset;
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomEndRotXRange - (this.randomEndRotXRange / 2.0f);
                        this.targetRotationXArray[g] = baseEnd + randomOffset;
                    }

                    int numberOfGroupsRotY = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomRotYCount));
                    EnsureArraySize(ref this.startRotationYArray, numberOfGroupsRotY);
                    EnsureArraySize(ref this.targetRotationYArray, numberOfGroupsRotY);
                    for (int g = 0; g < numberOfGroupsRotY; g++)
                    {
                        int i = g * Math.Max(1, this.randomRotYCount);
                        float baseStartY = this.startRotationY;
                        if (this.fixedTrajectory && this.fixedRotationStartYArray != null && this.fixedRotationStartYArray.Length > i)
                            baseStartY = this.fixedRotationStartYArray[i];

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartRotYRange - (this.randomStartRotYRange / 2.0f);
                        this.startRotationYArray[g] = baseStartY + randomOffset;
                    }
                    for (int g = 0; g < numberOfGroupsRotY; g++)
                    {
                        int i = g * Math.Max(1, this.randomRotYCount);
                        float baseEnd = this.endRotationY;
                        if (this.fixedTrajectory && this.fixedRotationEndYArray != null && this.fixedRotationEndYArray.Length > i)
                            baseEnd = this.fixedRotationEndYArray[i];

                        if (this.randomSERotYToggle && this.startRotationYArray != null && this.startRotationYArray.Length > g)
                        {
                            float baseStartY = this.startRotationY;
                            if (this.fixedTrajectory && this.fixedRotationStartYArray != null && this.fixedRotationStartYArray.Length > i)
                                baseStartY = this.fixedRotationStartYArray[i];

                            float startYOffset = this.startRotationYArray[g] - baseStartY;
                            baseEnd += startYOffset;
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomEndRotYRange - (this.randomEndRotYRange / 2.0f);
                        this.targetRotationYArray[g] = baseEnd + randomOffset;
                    }

                    int numberOfGroupsRotZ = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomRotZCount));
                    EnsureArraySize(ref this.startRotationZArray, numberOfGroupsRotZ);
                    EnsureArraySize(ref this.targetRotationZArray, numberOfGroupsRotZ);
                    for (int g = 0; g < numberOfGroupsRotZ; g++)
                    {
                        int i = g * Math.Max(1, this.randomRotZCount);
                        float baseStartZ = this.startRotationZ;
                        if (this.fixedTrajectory && this.fixedRotationStartZArray != null && this.fixedRotationStartZArray.Length > i)
                            baseStartZ = this.fixedRotationStartZArray[i];

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartRotZRange - (this.randomStartRotZRange / 2.0f);
                        this.startRotationZArray[g] = baseStartZ + randomOffset;
                    }
                    for (int g = 0; g < numberOfGroupsRotZ; g++)
                    {
                        int i = g * Math.Max(1, this.randomRotZCount);
                        float baseEnd = this.endRotationZ;
                        if (this.fixedTrajectory && this.fixedRotationEndZArray != null && this.fixedRotationEndZArray.Length > i)
                            baseEnd = this.fixedRotationEndZArray[i];

                        if (this.randomSERotZToggle && this.startRotationZArray != null && this.startRotationZArray.Length > g)
                        {
                            float baseStartZ = this.startRotationZ;
                            if (this.fixedTrajectory && this.fixedRotationStartZArray != null && this.fixedRotationStartZArray.Length > i)
                                baseStartZ = this.fixedRotationStartZArray[i];

                            float startZOffset = this.startRotationZArray[g] - baseStartZ;
                            baseEnd += startZOffset;
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomEndRotZRange - (this.randomEndRotZRange / 2.0f);
                        this.targetRotationZArray[g] = baseEnd + randomOffset;
                    }

                    int numberOfGroupsOpacity = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomOpacityCount));
                    EnsureArraySize(ref this.startOpacityArray, numberOfGroupsOpacity);
                    EnsureArraySize(ref this.targetOpacityArray, numberOfGroupsOpacity);
                    for (int g = 0; g < numberOfGroupsOpacity; g++)
                    {
                        float minVal = Math.Min(this.randomStartOpacityRange, this.randomEndOpacityRange);
                        float maxVal = Math.Max(this.randomStartOpacityRange, this.randomEndOpacityRange);
                        float randomFactor = (float)staticRng!.NextDouble();
                        float randomOpacity = minVal + (maxVal - minVal) * randomFactor;
                        this.startOpacityArray[g] = randomOpacity;
                        this.targetOpacityArray[g] = randomOpacity;
                    }

                    EnsureArraySize(ref this.hitProgressArray, numberOfGroupsForce);
                    EnsureArraySize(ref this.hitVelocityArray, numberOfGroupsForce);

                    EnsureArraySize(ref this.randomImageIndexArray, this.count);
                    for (int i = 0; i < this.count; i++)
                    {
                        if (this.sourceType == 1 && this.imageFiles != null && this.imageFiles.Length > 0)
                        {
                            this.randomImageIndexArray[i] = staticRng.Next(0, this.imageFiles.Length);
                        }
                        else
                        {
                            this.randomImageIndexArray[i] = -1;
                        }
                    }

                    if (this.floorToggle)
                    {
                        const int SIMULATION_STEPS = 30;
                        float FloorY = this.floorY;
                        float stepProgress = 1.0f / SIMULATION_STEPS;

                        Parallel.For(0, numberOfGroupsForce, g =>
                        {
                            float hitProgress = float.MaxValue;
                            Vector3 hitVelocity = Vector3.Zero;

                            Vector3 prevPos = CalculatePosition_Internal(i: (g * this.forceRandomCount), progress: 0f);
                            float prevProgress = 0f;

                            for (int step = 1; step <= SIMULATION_STEPS; step++)
                            {
                                float currentProgress = (float)step * stepProgress;
                                Vector3 currentPos = CalculatePosition_Internal(i: (g * this.forceRandomCount), progress: currentProgress);

                                bool hasHit = false;
                                if (this.floorJudgementType == 0 && currentPos.Y >= FloorY && prevPos.Y < FloorY) hasHit = true;
                                if (this.floorJudgementType == 1 && currentPos.Y <= FloorY && prevPos.Y > FloorY) hasHit = true;

                                if (hasHit)
                                {
                                    float yTravelInStep = currentPos.Y - prevPos.Y;
                                    float yTravelToHit = FloorY - prevPos.Y;
                                    float interpolationFactor = 0.0f;
                                    if (Math.Abs(yTravelInStep) > 0.0001f)
                                    {
                                        interpolationFactor = yTravelToHit / yTravelInStep;
                                        interpolationFactor = Math.Clamp(interpolationFactor, 0.0f, 1.0f);
                                    }
                                    float stepDuration = currentProgress - prevProgress;
                                    hitProgress = prevProgress + (stepDuration * interpolationFactor);
                                    float t_sec = (this.fps > 0) ? (stepDuration * (this.travelTime / this.fps)) : 0.01f;
                                    if (t_sec < 0.0001f) t_sec = 0.0001f;
                                    hitVelocity = (currentPos - prevPos) / t_sec;
                                    break;
                                }
                                prevPos = currentPos;
                                prevProgress = currentProgress;
                            }
                            this.hitProgressArray[g] = hitProgress;
                            this.hitVelocityArray[g] = hitVelocity;
                        });
                    }
                }

                disposer.RemoveAndDispose(ref commandList);

                float loopDuration = this.count * this.cycleTime;
                if (loopDuration <= 0) loopDuration = 1;
                float timeToUse = this.frame;

                int usedNodeCount = 0;

                // ▼追加：描画するイメージを一時保存するリスト
                var imagesToDraw = new List<ID2D1Image>();

                // ▼関数名を draw から process に変更
                void process(int i, float baseVirtualFrame)
                {
                    float mainProgress = float.NegativeInfinity;

                    if (this.loopToggle)
                    {
                        float T_base_start = i * this.cycleTime;
                        float timeInLoop = baseVirtualFrame % loopDuration;
                        float T_end_relative = T_base_start + this.travelTime;
                        float startFrameOfItem = (float)i * delayFramesPerItem;

                        if (timeInLoop < startFrameOfItem) return;

                        if (T_end_relative <= loopDuration)
                        {
                            if (timeInLoop >= T_base_start)
                                mainProgress = (timeInLoop - T_base_start) / this.travelTime;
                        }
                        else
                        {
                            float T_end_wrapped = T_end_relative % loopDuration;
                            float T_start_relative = T_base_start;
                            if (timeInLoop >= T_start_relative)
                                mainProgress = (timeInLoop - T_start_relative) / this.travelTime;
                            else
                                mainProgress = ((loopDuration - T_start_relative) + timeInLoop) / this.travelTime;
                        }
                    }
                    else
                    {
                        float T_start = i * this.cycleTime;
                        float startFrameOfItem = (float)i * delayFramesPerItem;

                        if (baseVirtualFrame < startFrameOfItem) return;

                        if (baseVirtualFrame >= T_start)
                            mainProgress = (baseVirtualFrame - T_start) / this.travelTime;
                    }

                    if (float.IsNegativeInfinity(mainProgress)) return;

                    float maxTrailLife = this.trailToggle ? (this.trailCount * this.trailInterval) : 0f;
                    if (mainProgress < 0f || mainProgress > 1.0f + maxTrailLife) return;

                    int loopCount = this.trailToggle ? Math.Max(1, this.trailCount) : 1;

                    for (int t = loopCount - 1; t >= 0; t--)
                    {
                        float rawProgress = mainProgress - (t * this.trailInterval);

                        if (rawProgress < 0.0f || rawProgress > 1.0f) continue;

                        if (usedNodeCount >= trailEffectPool.Count)
                        {
                            trailEffectPool.Add(new ParticleEffectNodes(devices));
                        }

                        var nodes = trailEffectPool[usedNodeCount];
                        usedNodeCount++;

                        float paramProgress = rawProgress;
                        if (this.curveToggle && this.curveFactorArray != null && this.curveFactorArray.Length > i)
                        {
                            float curveFactor = this.curveFactorArray[i];
                            paramProgress = rawProgress * curveFactor;
                        }
                        float clampedProgress = Math.Min(1.0f, Math.Max(0.0f, paramProgress));

                        float RotationX_progress = clampedProgress;
                        float RotationY_progress = clampedProgress;
                        float RotationZ_progress = clampedProgress;
                        float ScaleX_progress = clampedProgress;
                        float ScaleY_progress = clampedProgress;
                        float ScaleZ_progress = clampedProgress;
                        float Opacity_progress = clampedProgress;

                        float current_startscalex = this.scaleStartx;
                        if (this.fixedTrajectory && this.fixedScaleStartXArray != null && this.fixedScaleStartXArray.Length > i) current_startscalex = this.fixedScaleStartXArray[i];
                        float current_startscaley = this.scaleStarty;
                        if (this.fixedTrajectory && this.fixedScaleStartYArray != null && this.fixedScaleStartYArray.Length > i) current_startscaley = this.fixedScaleStartYArray[i];
                        float current_startscalez = this.scaleStartz;
                        if (this.fixedTrajectory && this.fixedScaleStartZArray != null && this.fixedScaleStartZArray.Length > i) current_startscalez = this.fixedScaleStartZArray[i];

                        float current_scalex = this.scalex;
                        if (this.fixedTrajectory && this.fixedScaleEndXArray != null && this.fixedScaleEndXArray.Length > i) current_scalex = this.fixedScaleEndXArray[i];
                        float current_scaley = this.scaley;
                        if (this.fixedTrajectory && this.fixedScaleEndYArray != null && this.fixedScaleEndYArray.Length > i) current_scaley = this.fixedScaleEndYArray[i];
                        float current_scalez = this.scalez;
                        if (this.fixedTrajectory && this.fixedScaleEndZArray != null && this.fixedScaleEndZArray.Length > i) current_scalez = this.fixedScaleEndZArray[i];

                        float current_startRotationX = this.startRotationX;
                        if (this.fixedTrajectory && this.fixedRotationStartXArray != null && this.fixedRotationStartXArray.Length > i) current_startRotationX = this.fixedRotationStartXArray[i];
                        float current_startRotationY = this.startRotationY;
                        if (this.fixedTrajectory && this.fixedRotationStartYArray != null && this.fixedRotationStartYArray.Length > i) current_startRotationY = this.fixedRotationStartYArray[i];
                        float current_startRotationZ = this.startRotationZ;
                        if (this.fixedTrajectory && this.fixedRotationStartZArray != null && this.fixedRotationStartZArray.Length > i) current_startRotationZ = this.fixedRotationStartZArray[i];

                        float current_endRotationX = this.endRotationX;
                        if (this.fixedTrajectory && this.fixedRotationEndXArray != null && this.fixedRotationEndXArray.Length > i) current_endRotationX = this.fixedRotationEndXArray[i];
                        float current_endRotationY = this.endRotationY;
                        if (this.fixedTrajectory && this.fixedRotationEndYArray != null && this.fixedRotationEndYArray.Length > i) current_endRotationY = this.fixedRotationEndYArray[i];
                        float current_endRotationZ = this.endRotationZ;
                        if (this.fixedTrajectory && this.fixedRotationEndZArray != null && this.fixedRotationEndZArray.Length > i) current_endRotationZ = this.fixedRotationEndZArray[i];

                        float current_endopacity = this.endopacity;
                        if (this.fixedTrajectory && this.fixedOpacityEndArray != null && this.fixedOpacityEndArray.Length > i) current_endopacity = this.fixedOpacityEndArray[i];
                        float current_midopacity = this.opacityMapMidPoint;
                        if (this.fixedTrajectory && this.fixedOpacityMidArray != null && this.fixedOpacityMidArray.Length > i) current_midopacity = this.fixedOpacityMidArray[i];
                        float current_startopacity = this.startopacity;
                        if (this.fixedTrajectory && this.fixedOpacityStartArray != null && this.fixedOpacityStartArray.Length > i) current_startopacity = this.fixedOpacityStartArray[i];

                        int safeCount;
                        int groupIndex;

                        float finalScalex;
                        if (this.randomScaleToggle && targetScaleXArray != null && startScaleXArray != null && targetScaleXArray.Length > 0 && startScaleXArray.Length > 0)
                        {
                            safeCount = Math.Max(1, this.randomScaleCount);
                            groupIndex = i / safeCount;
                            int targetIndex = Math.Min(groupIndex, targetScaleXArray.Length - 1);
                            float startX_Random = startScaleXArray[targetIndex];
                            float targetX = targetScaleXArray[targetIndex];
                            finalScalex = startX_Random + (targetX - startX_Random) * paramProgress;
                        }
                        else
                        {
                            finalScalex = current_startscalex + (current_scalex - current_startscalex) * ScaleX_progress;
                        }

                        float finalScaley;
                        if (this.randomScaleToggle && targetScaleYArray != null && startScaleYArray != null && targetScaleYArray.Length > 0 && startScaleYArray.Length > 0)
                        {
                            safeCount = Math.Max(1, this.randomScaleCount);
                            groupIndex = i / safeCount;
                            int targetIndex = Math.Min(groupIndex, targetScaleYArray.Length - 1);
                            float startY_Random = startScaleYArray[targetIndex];
                            float targetY = targetScaleYArray[targetIndex];
                            finalScaley = startY_Random + (targetY - startY_Random) * paramProgress;
                        }
                        else
                        {
                            finalScaley = current_startscaley + (current_scaley - current_startscaley) * ScaleY_progress;
                        }

                        float finalScalez;
                        if (this.randomScaleToggle && targetScaleZArray != null && startScaleZArray != null && targetScaleZArray.Length > 0 && startScaleZArray.Length > 0)
                        {
                            safeCount = Math.Max(1, this.randomScaleCount);
                            groupIndex = i / safeCount;
                            int targetIndex = Math.Min(groupIndex, targetScaleZArray.Length - 1);
                            float startZ_Random = startScaleZArray[targetIndex];
                            float targetZ = targetScaleZArray[targetIndex];
                            finalScalez = startZ_Random + (targetZ - startZ_Random) * paramProgress;
                        }
                        else
                        {
                            finalScalez = current_startscalez + (current_scalez - current_startscalez) * ScaleZ_progress;
                        }

                        float finalRotX;
                        if (this.randomRotXToggle && targetRotationXArray != null && startRotationXArray != null && targetRotationXArray.Length > 0 && startRotationXArray.Length > 0)
                        {
                            safeCount = Math.Max(1, this.randomRotXCount);
                            groupIndex = i / safeCount;
                            int targetIndex = Math.Min(groupIndex, targetRotationXArray.Length - 1);
                            float startX_Random = startRotationXArray[targetIndex];
                            float targetX = targetRotationXArray[targetIndex];
                            finalRotX = startX_Random + (targetX - startX_Random) * paramProgress;
                        }
                        else
                        {
                            finalRotX = current_startRotationX + (current_endRotationX - current_startRotationX) * RotationX_progress;
                        }

                        float finalRotY;
                        if (this.randomRotYToggle && targetRotationYArray != null && startRotationYArray != null && targetRotationYArray.Length > 0 && startRotationYArray.Length > 0)
                        {
                            safeCount = Math.Max(1, this.randomRotYCount);
                            groupIndex = i / safeCount;
                            int targetIndex = Math.Min(groupIndex, targetRotationYArray.Length - 1);
                            float startY_Random = startRotationYArray[targetIndex];
                            float targetY = targetRotationYArray[targetIndex];
                            finalRotY = startY_Random + (targetY - startY_Random) * paramProgress;
                        }
                        else
                        {
                            finalRotY = current_startRotationY + (current_endRotationY - current_startRotationY) * RotationY_progress;
                        }

                        float finalRotZ;
                        if (this.randomRotZToggle && targetRotationZArray != null && startRotationZArray != null && targetRotationZArray.Length > 0 && startRotationZArray.Length > 0)
                        {
                            safeCount = Math.Max(1, this.randomRotZCount);
                            groupIndex = i / safeCount;
                            int targetIndex = Math.Min(groupIndex, targetRotationZArray.Length - 1);
                            float startZ_Random = startRotationZArray[targetIndex];
                            float targetZ = targetRotationZArray[targetIndex];
                            finalRotZ = startZ_Random + (targetZ - startZ_Random) * paramProgress;
                        }
                        else
                        {
                            finalRotZ = current_startRotationZ + (current_endRotationZ - current_startRotationZ) * RotationZ_progress;
                        }

                        float finalOpacity;
                        if (this.opacityMapToggle)
                        {
                            float op_start = current_startopacity;
                            float op_mid = current_midopacity;
                            float op_end = current_endopacity;
                            float power = 1.0f + this.opacityMapEase * 4.0f;

                            if (clampedProgress < 0.5f)
                            {
                                float p_half1 = clampedProgress * 2.0f;
                                float t_ease = 1.0f - MathF.Pow(1.0f - p_half1, power);
                                finalOpacity = op_start + (op_mid - op_start) * t_ease;
                            }
                            else
                            {
                                float p_half2 = (clampedProgress - 0.5f) * 2.0f;
                                float t_ease = MathF.Pow(p_half2, power);
                                finalOpacity = op_mid + (op_end - op_mid) * t_ease;
                            }
                        }
                        else
                        {
                            float baseAnimatedOpacity = current_startopacity + (current_endopacity - current_startopacity) * Opacity_progress;
                            if (this.randomOpacityToggle && targetOpacityArray != null && startOpacityArray != null && targetOpacityArray.Length > 0 && startOpacityArray.Length > 0 && this.count > i)
                            {
                                safeCount = Math.Max(1, this.randomOpacityCount);
                                groupIndex = i / safeCount;
                                int targetIndex = Math.Min(groupIndex, targetOpacityArray.Length - 1);
                                float randomBaseOpacity = startOpacityArray[targetIndex];
                                float opacityMultiplier = baseAnimatedOpacity / 100.0f;
                                finalOpacity = randomBaseOpacity * opacityMultiplier;
                            }
                            else
                            {
                                finalOpacity = baseAnimatedOpacity;
                            }
                        }

                        if (this.trailToggle && t > 0)
                        {
                            float t_rate = (float)t / this.trailCount;
                            float fadeRate = 1.0f - ((float)t / this.trailCount) * this.trailFade;
                            finalOpacity *= Math.Max(0f, fadeRate);

                            float scaleMult = 1.0f + ((this.trailScale / 100f) - 1.0f) * t_rate;
                            scaleMult = Math.Max(0f, scaleMult);
                            finalScalex *= scaleMult;
                            finalScaley *= scaleMult;
                            finalScalez *= scaleMult;
                        }

                        Vector3 currentPosition = CalculatePosition(i, rawProgress);

                        float currentx = currentPosition.X;
                        float currenty = currentPosition.Y;
                        float currentz = currentPosition.Z;

                        float currentScalex = finalScalex / 100.0f;
                        float currentScaley = finalScaley / 100.0f;
                        float currentScalez = finalScalez / 100.0f;

                        float currentRotX_deg = finalRotX;
                        float currentRotY_deg = finalRotY;
                        float currentRotZ_deg = finalRotZ;

                        float currentRotX_rad = (float)Math.PI * (currentRotX_deg - rotation.X) / 180;
                        float currentRotY_rad = (float)Math.PI * (currentRotY_deg - rotation.Y) / 180;
                        float currentRotZ_rad = (float)Math.PI * (currentRotZ_deg + rotation.Z) / 180;

                        groupIndex = i / Math.Max(1, this.forceRandomCount);
                        float hitProgress = (this.floorToggle && this.hitProgressArray != null && groupIndex < this.hitProgressArray.Length)
                                            ? this.hitProgressArray[groupIndex]
                                            : float.MaxValue;

                        if (paramProgress >= hitProgress)
                        {
                            float waitFrames = (this.floorWaitTime / 1000f) * this.fps;
                            float fadeFrames = (this.floorFadeTime / 1000f) * this.fps;
                            float framesSinceHit = (this.travelTime) * (paramProgress - hitProgress);
                            float fadeStart_SinceHit = waitFrames;

                            if (framesSinceHit >= fadeStart_SinceHit && fadeFrames > 0)
                            {
                                float fadeProgress = (framesSinceHit - fadeStart_SinceHit) / fadeFrames;
                                fadeProgress = Math.Clamp(fadeProgress, 0.0f, 1.0f);
                                finalOpacity = finalOpacity * (1.0f - fadeProgress);
                            }
                        }

                        byte currentA = (byte)(this.startColor.A + (this.endColor.A - this.startColor.A) * clampedProgress);
                        byte currentR = (byte)(this.startColor.R + (this.endColor.R - this.startColor.R) * clampedProgress);
                        byte currentG = (byte)(this.startColor.G + (this.endColor.G - this.startColor.G) * clampedProgress);
                        byte currentB = (byte)(this.startColor.B + (this.endColor.B - this.startColor.B) * clampedProgress);

                        var tint = new Vortice.Mathematics.Color4(
                            currentR / 255.0f,
                            currentG / 255.0f,
                            currentB / 255.0f,
                            currentA / 255.0f
                        );

                        ID2D1Image? currentInput = input;
                        bool isRandomImage = false;

                        if (this.sourceType == 1 && this.imageSources != null && this.randomImageIndexArray != null && this.randomImageIndexArray.Length > i)
                        {
                            int imgIndex = this.randomImageIndexArray[i];
                            if (imgIndex >= 0 && imgIndex < this.imageSources.Length && this.imageSources[imgIndex] != null)
                            {
                                currentInput = this.imageSources[imgIndex].Output;
                                isRandomImage = true;
                            }
                        }

                        if (currentInput == null) return;

                        ID2D1Image centeredInput = currentInput;
                        if (isRandomImage)
                        {
                            var bounds = this.devices.DeviceContext.GetImageLocalBounds(currentInput);
                            float w = bounds.Right - bounds.Left;
                            float h = bounds.Bottom - bounds.Top;

                            nodes.centeringEffect.SetInput(0, currentInput, true);
                            nodes.centeringEffect.TransformMatrix = Matrix3x2.CreateTranslation(-w / 2f, -h / 2f);
                            centeredInput = nodes.centeringEffect.Output;
                        }

                        nodes.colorEffect.SetInput(0, centeredInput, true);

                        var tintMatrix = new Matrix5x4
                        {
                            M11 = tint.R,
                            M12 = 0,
                            M13 = 0,
                            M14 = 0,
                            M21 = 0,
                            M22 = tint.G,
                            M23 = 0,
                            M24 = 0,
                            M31 = 0,
                            M32 = 0,
                            M33 = tint.B,
                            M34 = 0,
                            M41 = 0,
                            M42 = 0,
                            M43 = 0,
                            M44 = tint.A,
                            M51 = 0,
                            M52 = 0,
                            M53 = 0,
                            M54 = 0
                        };
                        nodes.colorEffect.Matrix = tintMatrix;

                        using var colorOutput = nodes.colorEffect.Output;
                        ID2D1Image colorStageOutput = colorOutput;

                        safeCount = Math.Max(1, this.randomColorCount);
                        groupIndex = i / safeCount;

                        ID2D1Image? hueOutputTmp = null;
                        if (randomColorToggle)
                        {
                            nodes.hueEffect.SetInput(0, colorOutput, true);

                            float hOffset = (this.randomHueOffsetArray != null && groupIndex < this.randomHueOffsetArray.Length) ? this.randomHueOffsetArray[groupIndex] : 0f;
                            float sOffset = (this.randomSatOffsetArray != null && groupIndex < this.randomSatOffsetArray.Length) ? this.randomSatOffsetArray[groupIndex] : 0f;
                            float lOffset = (this.randomLumOffsetArray != null && groupIndex < this.randomLumOffsetArray.Length) ? this.randomLumOffsetArray[groupIndex] : 0f;

                            nodes.hueEffect.HueShift = hOffset;
                            nodes.hueEffect.SaturationFactor = 1.0f + sOffset;
                            nodes.hueEffect.LuminanceFactor = 1.0f + lOffset;
                            nodes.hueEffect.Factor = 1.0f;

                            hueOutputTmp = nodes.hueEffect.Output;
                            colorStageOutput = hueOutputTmp;
                        }
                        using var hueOutput = hueOutputTmp;

                        // ==========================================
                        // 個体用エフェクトチェーン（VideoEffects）の適用
                        // ==========================================
                        nodes.effectChain.VideoEffects = this.item.VideoEffects.ToList();
                        nodes.effectChain.SetInput(colorStageOutput);

                        int elapsedFrame = (int)Math.Max(0, rawProgress * this.travelTime);
                        int durationFrame = (int)Math.Max(1, this.travelTime);

                        var particleDesc = effectDescription with
                        {
                            ItemPosition = new YukkuriMovieMaker.Player.Video.FrameTime(elapsedFrame, fps),
                            ItemDuration = new YukkuriMovieMaker.Player.Video.FrameTime(durationFrame, fps),
                            DrawDescription = new DrawDescription(
                                Vector3.Zero, Vector2.Zero, Vector2.One, Vector3.Zero, Matrix4x4.Identity,
                                effectDescription.DrawDescription.ZoomInterpolationMode, 1.0, false,
                                ImmutableList<VideoEffectController>.Empty)
                        };

                        nodes.effectChain.Update(particleDesc);

                        ID2D1Image effectStageOutput = nodes.effectChain.Output ?? colorStageOutput;
                        DrawDescription updatedDrawDesc = nodes.effectChain.UpdatedDrawDescription ?? particleDesc.DrawDescription;

                        // ==========================================
                        // ピント機能（Blur）の適用
                        // ==========================================
                        float blurAmount = 0f;
                        float focusFadeFactor = 1.0f;

                        if (this.focusToggle)
                        {
                            Vector4 clipPos = Vector4.Transform(currentPosition, m_fullProjection);
                            float particleDepth = (clipPos.W != 0) ? (clipPos.Z / clipPos.W) : 0f;
                            float outOfFocus = Math.Abs(particleDepth - this.focusDepth);
                            float blurFactor = 0.0f;

                            if (outOfFocus > this.focusRange)
                            {
                                float distance = outOfFocus - this.focusRange;
                                float normalizedDistance = Math.Clamp(distance / Math.Max(0.001f, this.focusFallOffBlur), 0.0f, 1.0f);
                                blurFactor = 1.0f - MathF.Exp(-normalizedDistance * normalizedDistance * 3.0f);
                            }
                            blurAmount = blurFactor * this.focusMaxBlur;

                            if (this.focusFadeToggle)
                            {
                                float fadeRange = 1.0f - this.focusFadeMinOpacity;
                                focusFadeFactor = 1.0f - (blurFactor * fadeRange);
                            }
                        }

                        ID2D1Image blurStageOutput;
                        if (blurAmount > 0.1f)
                        {
                            nodes.blurEffect.SetInput(0, effectStageOutput, true);
                            nodes.blurEffect.StandardDeviation = blurAmount;
                            blurStageOutput = nodes.blurEffect.Output;
                        }
                        else
                        {
                            blurStageOutput = effectStageOutput;
                        }

                        // ==========================================
                        // 透明度と最終描画設定
                        // ==========================================
                        nodes.opacityEffect.SetInput(0, blurStageOutput, true);

                        float currentOpacity = finalOpacity / 100.0f;
                        currentOpacity *= (float)updatedDrawDesc.Opacity;
                        currentOpacity *= focusFadeFactor;

                        nodes.opacityEffect.SetValue((int)OpacityProperties.Opacity, Math.Clamp(currentOpacity, 0.0f, 1.0f));
                        using var opacityOutput = nodes.opacityEffect.Output;

                        nodes.renderEffect.SetInput(0, opacityOutput, true);

                        float finalPitch = currentRotX_rad;
                        float finalYaw = currentRotY_rad;
                        const float PI_HALF = (float)Math.PI / 2.0f;

                        if (this.autoOrient)
                        {
                            float futureProgress = Math.Min(1.0f, clampedProgress + 0.01f);
                            Vector3 futurePosition = CalculatePosition(i, futureProgress);
                            Vector3 velocity = futurePosition - currentPosition;
                            if (velocity.LengthSquared() < 0.0001f)
                            {
                                float pastProgress = Math.Max(0.0f, clampedProgress - 0.01f);
                                Vector3 pastPosition = CalculatePosition(i, pastProgress);
                                velocity = currentPosition - pastPosition;
                            }

                            if (velocity.LengthSquared() > 0.0001f)
                            {
                                Vector3 direction = Vector3.Normalize(velocity);
                                if (autoOrient2D)
                                {
                                    float targetAngleZ = (float)Math.Atan2(direction.Y, direction.X);
                                    finalYaw = 0f;
                                    finalPitch = 0f;
                                    currentRotZ_rad = targetAngleZ + PI_HALF;
                                }
                                else
                                {
                                    float targetYaw = (float)Math.Atan2(direction.X, direction.Z);
                                    float targetPitch = (float)Math.Asin(-direction.Y) + PI_HALF;
                                    float autoOrientYaw = targetYaw - ((float)Math.PI * drawDesc.Rotation.Y / 180);
                                    float autoOrientPitch = targetPitch - ((float)Math.PI * drawDesc.Rotation.X / 180);
                                    finalYaw = autoOrientYaw;
                                    finalPitch = autoOrientPitch;
                                }
                            }
                        }

                        Matrix4x4 finalRotationMatrix;
                        if (this.billboardXYZ)
                        {
                            Matrix4x4.Invert(camForBillboard, out var cameraWorldMatrix);
                            cameraWorldMatrix.Translation = Vector3.Zero;
                            finalRotationMatrix = cameraWorldMatrix * Matrix4x4.CreateRotationZ(currentRotZ_rad);
                        }
                        else if (this.billboard)
                        {
                            finalYaw = -cameraYaw;
                            finalRotationMatrix = Matrix4x4.CreateRotationX(finalPitch) *
                                                       Matrix4x4.CreateRotationY(finalYaw) *
                                                       Matrix4x4.CreateRotationZ(currentRotZ_rad);
                        }
                        else
                        {
                            finalRotationMatrix = Matrix4x4.CreateRotationX(finalPitch) *
                                                       Matrix4x4.CreateRotationY(finalYaw) *
                                                       Matrix4x4.CreateRotationZ(currentRotZ_rad);
                        }

                        // ==========================================
                        // 3D空間への統合行列の適用
                        // ==========================================
                        Matrix4x4 childEffectTransform =
                            Matrix4x4.CreateTranslation(-updatedDrawDesc.CenterPoint.X, -updatedDrawDesc.CenterPoint.Y, 0) *
                            Matrix4x4.CreateScale(updatedDrawDesc.Zoom.X, updatedDrawDesc.Zoom.Y, 1.0f) *
                            Matrix4x4.CreateRotationZ((float)Math.PI * updatedDrawDesc.Rotation.Z / 180f) *
                            Matrix4x4.CreateTranslation(updatedDrawDesc.CenterPoint.X, updatedDrawDesc.CenterPoint.Y, 0) *
                            Matrix4x4.CreateTranslation(updatedDrawDesc.Draw.X, updatedDrawDesc.Draw.Y, 0);

                        Vector3 currentScale = new Vector3(currentScalex, currentScaley, currentScalez);

                        nodes.renderEffect.TransformMatrix =
                                                       childEffectTransform *
                                                       finalRotationMatrix *
                                                       Matrix4x4.CreateScale(currentScale) *
                                                       Matrix4x4.CreateTranslation(new Vector3(currentx, currenty, currentz)) *
                                                       m_internalDraw;

                        // ▼変更：すぐに描画するのではなくリストに追加して後で描画する
                        imagesToDraw.Add(nodes.renderEffect.Output);
                    }
                }

                this._drawList.Clear();

                if (this.zSortToggle)
                {
                    for (int i = 0; i < this.count; i++)
                    {
                        float progress = -1f;

                        if (this.loopToggle)
                        {
                            float T_base_start = i * this.cycleTime;
                            float T_start_relative = T_base_start;
                            float timeInLoop = timeToUse % loopDuration;
                            float T_end_relative = T_base_start + this.travelTime;

                            if (T_end_relative <= loopDuration)
                            {
                                if (timeInLoop >= T_base_start && timeInLoop < T_end_relative)
                                {
                                    progress = (timeInLoop - T_base_start) / this.travelTime;
                                }
                            }
                            else
                            {
                                float T_end_wrapped = T_end_relative % loopDuration;
                                if (timeInLoop >= T_start_relative)
                                {
                                    progress = (timeInLoop - T_start_relative) / this.travelTime;
                                }
                                else if (timeInLoop < T_end_wrapped)
                                {
                                    progress = ((loopDuration - T_start_relative) + timeInLoop) / this.travelTime;
                                }
                            }
                        }
                        else
                        {
                            float T_start = i * this.cycleTime;
                            float T_end = T_start + this.travelTime;
                            if (timeToUse >= T_start && timeToUse < T_end)
                            {
                                progress = (timeToUse - T_start) / this.travelTime;
                            }
                        }

                        if (progress < 0f || progress > 1.0f) continue;

                        Vector3 currentPos = CalculatePosition(i, progress);

                        if (this.cullingToggle)
                        {
                            float maxScale = Math.Max(this.scalex, Math.Max(this.scaley, this.scalez));
                            if (this.randomScaleToggle) maxScale += Math.Max(this.randomStartScaleRange, this.randomEndScaleRange);
                            if (!IsVisible(currentPos, m_fullProjection, this.cullingBuffer, maxScale)) continue;
                        }

                        this._drawList.Add(new ItemDrawData { Index = i, SortKey = currentPos.Z });
                    }
                }
                else
                {
                    for (int i = 0; i < this.count; i++)
                    {
                        float progress = -1f;

                        if (this.loopToggle)
                        {
                            float T_base_start = i * this.cycleTime;
                            float T_start_relative = T_base_start;
                            float timeInLoop = timeToUse % loopDuration;
                            float T_end_relative = T_base_start + this.travelTime;

                            if (T_end_relative <= loopDuration)
                            {
                                if (timeInLoop >= T_base_start && timeInLoop < T_end_relative)
                                {
                                    progress = (timeInLoop - T_base_start) / this.travelTime;
                                }
                            }
                            else
                            {
                                float T_end_wrapped = T_end_relative % loopDuration;
                                if (timeInLoop >= T_start_relative)
                                {
                                    progress = (timeInLoop - T_start_relative) / this.travelTime;
                                }
                                else if (timeInLoop < T_end_wrapped)
                                {
                                    progress = ((loopDuration - T_start_relative) + timeInLoop) / this.travelTime;
                                }
                            }
                        }
                        else
                        {
                            float T_start = i * this.cycleTime;
                            float T_end = T_start + this.travelTime;
                            if (timeToUse >= T_start && timeToUse < T_end)
                            {
                                progress = (timeToUse - T_start) / this.travelTime;
                            }
                        }

                        if (progress < 0f || progress > 1.0f) continue;

                        Vector3 currentPos = CalculatePosition(i, progress);

                        if (this.cullingToggle)
                        {
                            float maxScale = Math.Max(this.scalex, Math.Max(this.scaley, this.scalez));
                            if (this.randomScaleToggle) maxScale += Math.Max(this.randomStartScaleRange, this.randomEndScaleRange);
                            if (!IsVisible(currentPos, m_fullProjection, this.cullingBuffer, maxScale)) continue;
                        }

                        Vector4 clipPos = Vector4.Transform(currentPos, m_fullProjection);
                        float perspectiveZ = (clipPos.W != 0) ? (clipPos.Z / clipPos.W) : 0f;
                        this._drawList.Add(new ItemDrawData { Index = i, SortKey = perspectiveZ });
                    }

                    if (!this.fixedDraw)
                    {
                        _drawList.Sort((a, b) =>
                        {
                            int distanceComparison = a.SortKey.CompareTo(b.SortKey);
                            if (distanceComparison != 0) return distanceComparison;
                            else return a.Index.CompareTo(b.Index);
                        });
                    }
                }

                if (this.reverseDraw >= 0.5f) _drawList.Reverse();

                // ▼変更：BeginDrawの前に全ての計算とエフェクト更新（サイズ計算含む）を完了させる
                foreach (var data in _drawList) process(data.Index, timeToUse);

                // ▼変更：グラフ処理完了後に描画ターゲットを設定し、一気に描画する
                commandList = dc.CreateCommandList();
                disposer.Collect(commandList);
                dc.Target = commandList;
                dc.BeginDraw();
                dc.Clear(null);

                foreach (var img in imagesToDraw)
                {
                    dc.DrawImage(img);
                    img.Dispose(); // VorticeのC#ラッパーのみ解放
                }

                dc.EndDraw();
                dc.Target = null;
                commandList.Close();

                isFirst = false;
                IsInputChanged = false;
            }

            return effectDescription.DrawDescription with
            {
                Draw = drawDesc.Draw with { X = tx, Y = ty, Z = -tz },
                Rotation = Vector3.Zero,
                Camera = Matrix4x4.Identity
            };
        }

        public struct ItemDrawData
        {
            public int Index;
            public float SortKey;
        }

        private Vector3 CalculatePosition(int i, float progress)
        {
            float current_gravityX = this.gravityX;
            if (this.fixedTrajectory && this.fixedGravityXArray != null && this.fixedGravityXArray.Length > i)
                current_gravityX = this.fixedGravityXArray[i];
            float current_gravityY = this.gravityY;
            if (this.fixedTrajectory && this.fixedGravityYArray != null && this.fixedGravityYArray.Length > i)
                current_gravityY = this.fixedGravityYArray[i];
            float current_gravityZ = this.gravityZ;
            if (this.fixedTrajectory && this.fixedGravityZArray != null && this.fixedGravityZArray.Length > i)
                current_gravityZ = this.fixedGravityZArray[i];

            if (this.curveToggle && this.curveFactorArray != null && this.curveFactorArray.Length > i)
            {
                float curveFactor = this.curveFactorArray[i];
                progress = progress * curveFactor;
            }

            progress = Math.Min(1.0f, Math.Max(0.0f, progress));

            int groupIndex = i / Math.Max(1, this.forceRandomCount);
            float hitProgress = (this.floorToggle && this.hitProgressArray != null && groupIndex < this.hitProgressArray.Length)
                                ? this.hitProgressArray[groupIndex]
                                : float.MaxValue;

            if (progress < hitProgress)
            {
                return CalculatePosition_Internal(i, progress);
            }
            else
            {
                Vector3 P0 = CalculatePosition_Internal(i, hitProgress);
                P0.Y = this.floorY;
                float t_sec = (this.fps > 0) ? ((progress - hitProgress) * (this.travelTime / this.fps)) : 0f;

                switch (this.floorActionType)
                {
                    case 0: return P0;
                    case 1:
                        {
                            Vector3 V0 = (this.hitVelocityArray != null && groupIndex < this.hitVelocityArray.Length) ? this.hitVelocityArray[groupIndex] : Vector3.Zero;
                            V0.Y = 0;
                            float friction = Math.Abs(this.bounceEnergyLoss - 1.0f);
                            V0 *= friction;
                            return P0 + (V0 * t_sec);
                        }
                    case 2:
                        {
                            Vector3 P_current = P0;
                            Vector3 V_current = (this.hitVelocityArray != null && groupIndex < this.hitVelocityArray.Length) ? this.hitVelocityArray[groupIndex] : Vector3.Zero;
                            float bFactor = this.bounceFactor;
                            float eLossMultiplier = 1.0f - this.bounceEnergyLoss;
                            float bounceDir = (this.floorJudgementType == 1) ? 1.0f : -1.0f;

                            V_current.Y = bounceDir * Math.Abs(V_current.Y) * bFactor;
                            V_current *= eLossMultiplier;

                            Vector3 A_XZ = new Vector3(current_gravityX, 0, current_gravityZ);
                            float g_pseudo = this.bounceGravity;
                            float t_remaining = t_sec;

                            int safetyCounter = 0;
                            while (t_remaining > 0.0001f)
                            {
                                safetyCounter++;
                                if (safetyCounter > this.bounceCount) return P_current;

                                if ((bounceDir < 0 && V_current.Y > -1.0f) || (bounceDir > 0 && V_current.Y < 1.0f))
                                {
                                    V_current.Y = 0;
                                    float t_sq = t_remaining * t_remaining;
                                    return P_current + (V_current * t_remaining) + (0.5f * A_XZ * t_sq);
                                }

                                float t_arc_duration = -2.0f * V_current.Y / g_pseudo;

                                if (t_arc_duration <= 0.0001f)
                                {
                                    float t_sq = t_remaining * t_remaining;
                                    Vector3 A_fall = new Vector3(A_XZ.X, g_pseudo, A_XZ.Z);
                                    return P_current + (V_current * t_remaining) + (0.5f * A_fall * t_sq);
                                }

                                if (t_remaining <= t_arc_duration)
                                {
                                    float t_sq = t_remaining * t_remaining;
                                    Vector3 A_arc = new Vector3(A_XZ.X, g_pseudo, A_XZ.Z);
                                    return P_current + (V_current * t_remaining) + (0.5f * A_arc * t_sq);
                                }

                                t_remaining -= t_arc_duration;
                                float t_arc_sq = t_arc_duration * t_arc_duration;
                                Vector3 A_arc_full = new Vector3(A_XZ.X, g_pseudo, A_XZ.Z);
                                Vector3 P_end_arc = P_current + (V_current * t_arc_duration) + (0.5f * A_arc_full * t_arc_sq);
                                P_end_arc.Y = this.floorY;
                                Vector3 V_end_arc = V_current + (A_arc_full * t_arc_duration);

                                P_current = P_end_arc;
                                V_current = V_end_arc;
                                V_current.Y = bounceDir * Math.Abs(V_current.Y) * bFactor;
                                V_current *= eLossMultiplier;
                            }
                            return P_current;
                        }
                    default: return P0;
                }
            }
        }

        private Vector3 CalculatePosition_Internal(int i, float progress)
        {
            progress = ApplyAirResistance(progress, this.airResistance);

            float PositionX_progress = progress;
            float PositionY_progress = progress;
            float PositionZ_progress = progress;

            float current_startx = this.startx;
            float current_starty = this.starty;
            float current_startz = this.startz;
            if (this.fixedTrajectory && this.fixedPositionStartXArray != null && this.fixedPositionStartXArray.Length > i)
                current_startx = this.fixedPositionStartXArray[i];
            if (this.fixedTrajectory && this.fixedPositionStartYArray != null && this.fixedPositionStartYArray.Length > i)
                current_starty = this.fixedPositionStartYArray[i];
            if (this.fixedTrajectory && this.fixedPositionStartZArray != null && this.fixedPositionStartZArray.Length > i)
                current_startz = this.fixedPositionStartZArray[i];

            float current_endx = this.endx;
            if (this.fixedTrajectory && this.fixedPositionEndXArray != null && this.fixedPositionEndXArray.Length > i)
                current_endx = this.fixedPositionEndXArray[i];
            float current_endy = this.endy;
            if (this.fixedTrajectory && this.fixedPositionEndYArray != null && this.fixedPositionEndYArray.Length > i)
                current_endy = this.fixedPositionEndYArray[i];
            float current_endz = this.endz;
            if (this.fixedTrajectory && this.fixedPositionEndZArray != null && this.fixedPositionEndZArray.Length > i)
                current_endz = this.fixedPositionEndZArray[i];

            float current_gravityX = this.gravityX;
            if (this.fixedTrajectory && this.fixedGravityXArray != null && this.fixedGravityXArray.Length > i)
                current_gravityX = this.fixedGravityXArray[i];
            float current_gravityY = this.gravityY;
            if (this.fixedTrajectory && this.fixedGravityYArray != null && this.fixedGravityYArray.Length > i)
                current_gravityY = this.fixedGravityYArray[i];
            float current_gravityZ = this.gravityZ;
            if (this.fixedTrajectory && this.fixedGravityZArray != null && this.fixedGravityZArray.Length > i)
                current_gravityZ = this.fixedGravityZArray[i];

            if (this.calculationType == 1)
            {
                float t_sec = (this.fps > 0) ? (progress * (this.travelTime / this.fps)) : 0f;
                float t_squared = t_sec * t_sec;
                Vector3 P0 = new Vector3(current_startx, current_starty, current_startz);
                Vector3 A = new Vector3(current_gravityX, current_gravityY, current_gravityZ);

                int groupIndex = i / Math.Max(1, this.forceRandomCount);

                float currentPitch = this.forcePitch;
                float currentYaw = this.forceYaw;
                float currentRoll = this.forceRoll;
                float currentVelocity = this.forceVelocity;

                if (this.forceRandomCount > 0 && this.randomForcePitchArray != null && groupIndex < this.randomForcePitchArray.Length && this.randomForceYawArray != null && this.randomForceRollArray != null && this.randomForceVelocityArray != null)
                {
                    currentPitch = this.randomForcePitchArray[groupIndex];
                    currentYaw = this.randomForceYawArray[groupIndex];
                    currentRoll = this.randomForceRollArray[groupIndex];
                    currentVelocity = this.randomForceVelocityArray[groupIndex];
                }
                else if (this.fixedTrajectory && this.fixedForcePitchArray != null && i < this.fixedForcePitchArray.Length && this.fixedForceYawArray != null && this.fixedForceRollArray != null && this.fixedForceVelocityArray != null)
                {
                    currentPitch = this.fixedForcePitchArray[i];
                    currentYaw = this.fixedForceYawArray[i];
                    currentRoll = this.fixedForceRollArray[i];
                    currentVelocity = this.fixedForceVelocityArray[i];
                }

                float pitchRad = MathF.PI * currentPitch / 180f;
                float yawRad = MathF.PI * currentYaw / 180f;
                float rollRad = MathF.PI * currentRoll / 180f;

                Quaternion qYawPitch = Quaternion.CreateFromYawPitchRoll(yawRad, pitchRad, 0f);
                Quaternion qRollWorld = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rollRad);
                Quaternion finalRot = Quaternion.Normalize(Quaternion.Multiply(qRollWorld, qYawPitch));

                Vector3 direction = Vector3.Transform(Vector3.UnitZ, finalRot);
                Vector3 V0 = direction * currentVelocity;

                return P0 + (V0 * t_sec) + (0.5f * A * t_squared);
            }
            else
            {
                float currentx_base = 0f;
                float currenty_base = 0f;
                float currentz_base = 0f;

                if (this.randomToggleX && targetXArray != null && startXArray != null && targetXArray.Length > 0 && startXArray.Length > 0)
                {
                    int safeRandomXCount = Math.Max(1, this.randomXCount);
                    int groupIndexX = i / safeRandomXCount;
                    int targetIndex = Math.Min(groupIndexX, targetXArray.Length - 1);
                    float startX_Random = startXArray[targetIndex];
                    float targetX = targetXArray[targetIndex];
                    currentx_base = startX_Random + (targetX - startX_Random) * progress;
                }
                else
                {
                    currentx_base = current_startx + (current_endx - current_startx) * PositionX_progress;
                }

                if (this.randomToggleY && targetYArray != null && startYArray != null && targetYArray.Length > 0 && startYArray.Length > 0)
                {
                    int safeRandomYCount = Math.Max(1, this.randomYCount);
                    int groupIndexY = i / safeRandomYCount;
                    int targetIndex = Math.Min(groupIndexY, targetYArray.Length - 1);
                    float startY_Random = startYArray[targetIndex];
                    float targetY = targetYArray[targetIndex];
                    currenty_base = startY_Random + (targetY - startY_Random) * progress;
                }
                else
                {
                    currenty_base = current_starty + (current_endy - current_starty) * PositionY_progress;
                }

                if (this.randomToggleZ && targetZArray != null && startZArray != null && targetZArray.Length > 0 && startZArray.Length > 0)
                {
                    int safeRandomZCount = Math.Max(1, this.randomZCount);
                    int groupIndexZ = i / safeRandomZCount;
                    int targetIndex = Math.Min(groupIndexZ, targetZArray.Length - 1);
                    float startZ_Random = startZArray[targetIndex];
                    float targetZ = targetZArray[targetIndex];
                    currentz_base = startZ_Random + (targetZ - startZ_Random) * progress;
                }
                else
                {
                    currentz_base = current_startz + (current_endz - current_startz) * PositionZ_progress;
                }

                float progressSquared = progress * progress;
                if (grTerminationToggle) progressSquared = (progress * progress - progress);

                float gravityOffsetX = current_gravityX * progressSquared;
                float gravityOffsetY = current_gravityY * progressSquared;
                float gravityOffsetZ = current_gravityZ * progressSquared;

                float currentx = currentx_base + gravityOffsetX;
                float currenty = currenty_base + gravityOffsetY;
                float currentz = currentz_base + gravityOffsetZ;

                return new Vector3(currentx, currenty, currentz);
            }
        }

        private float ApplyAirResistance(float progress, float resistance)
        {
            if (resistance <= 0.001f) return progress;
            if (progress >= 0.999f) return 1.0f;

            float power = 1.0f + resistance * 4.0f;
            return 1.0f - MathF.Pow(1.0f - progress, power);
        }

        void EnsureArraySize<T>(ref T[]? array, int length)
        {
            if (array == null || array.Length != length) array = new T[length];
        }

        private bool IsVisible(Vector3 worldPos, Matrix4x4 cameraMatrix, float buffer, float particleScale)
        {
            Vector4 clipPos = Vector4.Transform(worldPos, cameraMatrix);
            float w = clipPos.W;

            float baseSize = Math.Max(this.imageWidth, this.imageHeight);
            float radius = (baseSize / 2.0f) * (particleScale / 100.0f);
            float nearClipLimit = -(radius * 1.5f);

            if (w < nearClipLimit) return false;

            float baseWidth = this.projectWidth;
            float baseHeight = this.projectHeight;
            float baseWidthHalf = baseWidth / 2f;
            float baseHeightHalf = baseHeight / 2f;

            float depth = Math.Abs(clipPos.Z);
            float allowedHalfWidth = baseWidthHalf + (depth * baseWidthHalf / 1000f);
            float allowedHalfHeight = baseHeightHalf + (depth * (baseHeightHalf / 1000f));

            float marginScale = 1.0f + buffer;
            float limitX = allowedHalfWidth * marginScale;
            float limitY = allowedHalfHeight * marginScale;

            if (Math.Abs(clipPos.X) > limitX) return false;
            if (Math.Abs(clipPos.Y) > limitY) return false;

            return true;
        }

        static Window? GetYmmMainWindow()
        {
            if (Application.Current == null) return null;
            foreach (Window w in Application.Current.Windows)
                if (w.GetType().FullName == "YukkuriMovieMaker.Views.MainView") return w;
            return null;
        }

        static dynamic? GetProp(dynamic obj, string propName)
        {
            if (obj == null) return null;
            Type type = obj.GetType();
            PropertyInfo? info = type.GetProperty(propName);
            return info?.GetValue(obj);
        }

        private void UpdateProjectInfo()
        {
            if (Application.Current == null) return;

            try
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var window = GetYmmMainWindow();
                    if (window == null) return;

                    dynamic? mainVM = window.DataContext;
                    dynamic? statusBarVM = GetProp(mainVM, "StatusBarViewModel");
                    dynamic? statusBarVal = GetProp(statusBarVM, "Value");
                    dynamic? videoInfoProp = GetProp(statusBarVal, "VideoInfo");

                    string? videoInfoString = GetProp(videoInfoProp, "Value");

                    if (!string.IsNullOrEmpty(videoInfoString))
                    {
                        var parts = videoInfoString.Split(' ');
                        if (parts.Length > 0)
                        {
                            var resParts = parts[0].Split('x');
                            if (resParts.Length >= 2)
                            {
                                if (float.TryParse(resParts[0], out float w) && float.TryParse(resParts[1], out float h))
                                {
                                    this.projectWidth = w;
                                    this.projectHeight = h;
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Project Info Fetch Error: " + ex.Message);
            }
        }

        public void ClearInput()
        {
        }

        public void Dispose()
        {
            if (this.imageSources != null)
            {
                for (int idx = 0; idx < this.imageSources.Length; idx++)
                {
                    this.imageSources[idx]?.Dispose();
                    this.imageSources[idx] = null;
                }
                this.imageSources = null;
            }
            this.imageFiles = null;

            disposer.Dispose();

            if (commandList != null && commandList is IDisposable disposableCommandList) disposableCommandList.Dispose();
            this.commandList = null;

            // ▼削除：リストからの削除を廃止
            // foreach (var effect in this.hueEffects) effect.Dispose();
            // this.hueEffects.Clear();

            foreach (var nodeSet in this.trailEffectPool) nodeSet.Dispose();
            this.trailEffectPool.Clear();

            this.input = null;
        }

        public void SetInput(ID2D1Image? input)
        {
            this.input = input;
            IsInputChanged = true;
        }
    }
}
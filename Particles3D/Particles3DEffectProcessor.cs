using System;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using Windows.Graphics.Display;
using Windows.Networking.NetworkOperators;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Settings;

//--Todo--
//初期と終端を同期するようにしたい。ただし、初期を動かすことで終端もつられて動く。しかし、終端は固定されることはない。(済)
//不透明度もうちょっと直す。(済)
//ScaleX,Y,Z同期設定を作る。(済)
//回転をX,Y,Zわける。(済)
//ScaleX,Y,Zの初期値作る(済)
//ScaleのfixedStartArrayつくる(済)
//XYZビルボードver作成(済)
//ランダム不透明度を有効化した時に、不透明度の初期値終端値が機能しなくなるものの修正(ムズイかも)(済)

//--いつかできたら--
//回転を同期(長細いものを飛ばしたとき、常に同じ角度なのは違和感なので、飛んでいる方向に角度を変える。)(済)
//floor機能(Glueだけでも)(AEの機能)(済)
//random HSL(射出されるときの色を、色相だけランダムにする、とか、彩度だけランダムにするとか切り替え可能に。)(RGBだと、色相だけでなく輝度や明度まで変わってしまって使いづらそう)(済)
//この色からこの色へ変化(color picker)(優先度高)(現：乗算処理) (済)
//Z軸だけでなく、X軸Y軸にも描画ソート追加(これにより結構な精度で疑似3Dを表現できそうだが、それをどこ基準に変えるかによる問題点はありそう？)(済)
//X,Y,Zで力・方向を扱うのではなく、力の向きと強さで扱う。(これにより、四角形型の物理法則ではなく、円形の、実際の物理法則近づく。(スクショ2025-11-07参照))(済)
//XYビルボードver作成
//描画0fから一巡した状態にするToggle追加。(済)
//カメラに近づくほど、ぼかしや不透明度が低くなる機能(ピントのような概念かな？)(済)
//空気抵抗値をつける。(済)
//中間不透明度(AEのOpacity MAPの簡易版として作りたい。)(パラメータ・中間不透明度・減加速度(0.0なら線形、0.7ぐらいならいい感じに最初はEaseOUT、最後はEaseINに、1.0なら瞬時に中間不透明度へ。))(済)
//プリセット機能の追加(済)
//残像エフェクト(済)
//残像段々小さくする機能(済)
//カメラに表示されていないものは描画しない機能(軽量化)(済)

namespace Particles3D
{

    internal class ParticleEffectNodes : IDisposable
    {
        public Vortice.Direct2D1.Effects.ColorMatrix colorEffect;
        public Vortice.Direct2D1.Effects.Opacity opacityEffect;
        public Vortice.Direct2D1.Effects.GaussianBlur blurEffect;
        public Vortice.Direct2D1.Effects.Transform3D renderEffect;

        // コンストラクタで、必要なエフェクトをすべて生成する
        public ParticleEffectNodes(ID2D1DeviceContext dc)
        {
            colorEffect = new Vortice.Direct2D1.Effects.ColorMatrix(dc);
            opacityEffect = new Opacity(dc);
            blurEffect = new Vortice.Direct2D1.Effects.GaussianBlur(dc);
            renderEffect = new Transform3D(dc);
        }

        // 破棄
        public void Dispose()
        {
            colorEffect?.Dispose();
            opacityEffect?.Dispose();
            blurEffect?.Dispose();
            renderEffect?.Dispose();
        }
    }

    internal class Particles3DEffectProcessor : IVideoEffectProcessor, IDisposable
    {
        DisposeCollector disposer = new();

        Random? staticRng;
        float[]? targetXArray; // 目標Xを格納する配列
        float[]? startXArray;  // StartXのランダム始端値
        float[]? targetYArray; // 目標Yを格納する配列
        float[]? startYArray;  // StartYのランダム始端値
        float[]? targetZArray; // 目標Zを格納する配列
        float[]? startZArray;  // StartZのランダム始端値

        float[]? startScaleXArray; // ...以下略
        float[]? targetScaleXArray;
        float[]? startScaleYArray;
        float[]? targetScaleYArray;
        float[]? startScaleZArray;
        float[]? targetScaleZArray;

        // Rotation (始点/終点) のランダム配列
        float[]? startRotationXArray;
        float[]? targetRotationXArray;
        float[]? startRotationYArray;
        float[]? targetRotationYArray;
        float[]? startRotationZArray;
        float[]? targetRotationZArray;

        // Opacity (始点/終点) のランダム配列
        float[]? startOpacityArray;
        float[]? targetOpacityArray;

        float[]? curveFactorArray;
        float[]? fixedPositionStartXArray;
        float[]? fixedPositionStartYArray;
        float[]? fixedPositionStartZArray;
        float[]? fixedPositionEndXArray;    // 固定された EndX 値
        float[]? fixedPositionEndYArray;    // 固定された EndY 値
        float[]? fixedPositionEndZArray;    // 固定された EndZ 値
        float[]? fixedScaleStartXArray;     // ...以下略
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

        // 事前計算したHLSLエフェクトを保存するリスト
        List<Particles3DHueCustomEffect> hueEffects = new();

        readonly Particles3DEffect item;

        IGraphicsDevicesAndContext devices;

        ID2D1Image? input;
        ID2D1CommandList? commandList;

        List<ParticleEffectNodes> trailEffectPool = new();

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
        bool randomScaleToggle, randomRotXToggle, randomRotYToggle, randomRotZToggle, randomOpacityToggle; // ランダムON/OFF
        int randomScaleCount, randomRotXCount, randomRotYCount, randomRotZCount, randomOpacityCount; // グループ数
        float randomStartScaleRange, randomEndScaleRange; // スケールランダム幅
        float randomStartRotXRange, randomEndRotXRange; // 回転ランダム幅
        float randomStartRotYRange, randomEndRotYRange; // 回転ランダム幅
        float randomStartRotZRange, randomEndRotZRange; // 回転ランダム幅
        float randomStartOpacityRange, randomEndOpacityRange; // 透明度ランダム幅
        bool randomSEScaleToggle, randomSERotXToggle, randomSERotYToggle, randomSERotZToggle;
        bool billboard, billboardXYZ;
        //bool billboardXY;
        bool grTerminationToggle;
        bool randomSyScaleToggle;
        bool pSEToggleX, pSEToggleY, pSEToggleZ;
        bool autoOrient, autoOrient2D;
        float randomHueRange, randomSatRange, randomLumRange;
        int randomColorCount;
        bool randomColorToggle;
        int calculationType; // 0:終端値指定 1:力指定
        float forcePitch, forceYaw, forceVelocity, forceRoll;
        int fps;
        float forceRandomPitch, forceRandomYaw, forceRandomRoll, forceRandomVelocity;
        int forceRandomCount;
        bool floorToggle;
        float floorY, floorWaitTime, floorFadeTime;
        bool zSortToggle;
        int floorJudgementType; // 0:床上 1:床下
        int floorActionType; // 0:接着 1:氷原 2;反射
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

            //いっぱい定義
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
            //var billboardXY = item.BillboardXYDraw;
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
            var bounceFactor = (float)item.BounceFactor.GetValue(frame, length, fps);
            var bounceEnergyLoss = (float)item.BounceEnergyLoss.GetValue(frame, length, fps);
            var bounceGravity = (float)item.BounceGravity.GetValue(frame, length, fps);
            var bounceCount = (int)item.BounceCount.GetValue(frame, length, fps);

            var zSortToggle = item.ZSortToggle;

            var focusToggle = item.FocusToggle;
            var focusFadeToggle = item.FocusFadeToggle;
            var focusDepth = (float)item.FocusDepth.GetValue(frame, length, fps);
            var focusRange = (float)item.FocusRange.GetValue(frame, length, fps);
            var focusMaxBlur = (float)item.FocusMaxBlur.GetValue(frame, length, fps);
            var focusFadeMinOpacity = (float)item.FocusFadeMinOpacity.GetValue(frame, length, fps);
            var focusFallOffBlur = (float)item.FocusFallOffBlur.GetValue(frame, length, fps);

            var airResistance = (float)item.AirResistance.GetValue(frame, length, fps);

            var loopToggle = item.LoopToggle;

            var trailToggle = item.TrailToggle;
            var trailCount = (int)item.TrailCount.GetValue(frame, length, fps);
            var trailInterval = (float)item.TrailInterval.GetValue(frame, length, fps);
            var trailFade = (float)item.TrailFade.GetValue(frame, length, fps);
            var trailScale = (float)item.TrailScale.GetValue(frame, length, fps);

            var cullingToggle = item.CullingToggle;
            var cullingBuffer = (float)item.CullingBuffer.GetValue(frame, length, fps);

            //---random関連---
            // randomXCountが0だと0除算や計算がおかしくなるので、1以上に強制する
            int SafeRandomXCount = Math.Max(1, randomXCount);

            // グループの数
            int numberOfGroups = (int)Math.Ceiling((double)count / SafeRandomXCount);

            // 配列に必要なサイズは、[グループ0の目標]...[グループN-1の目標] + [全体の最終目標] の N+1 個
            int arraySize = numberOfGroups + 1;

            // プロジェクト情報の更新
            if (isFirst)
            {
                UpdateProjectInfo();
            }

            if ((isFirst || IsInputChanged) && this.input != null)
            {
                // 画像のバウンディングボックス（範囲）を取得
                var imageSize = this.devices.DeviceContext.GetImageLocalBounds(this.input);

                // 幅と高さを計算して保存
                this.imageWidth = imageSize.Right - imageSize.Left;
                this.imageHeight = imageSize.Bottom - imageSize.Top;

                // 念のため0以下ならデフォルトに戻す
                if (this.imageWidth <= 0) this.imageWidth = 100f;
                if (this.imageHeight <= 0) this.imageHeight = 100f;
            }

            // arrayNeedsUpdate のチェックは、this.に代入する前に行う
            bool arrayNeedsUpdate = isFirst || this.randomSeed != randomSeed ||
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
                this.billboard != billboard || /*this.billboardXY != billboardXY ||*/ this.billboardXYZ != billboardXYZ || this.startx != startx || this.starty != starty || this.startz != startz ||
                this.scaleStartz != scaleStartz || this.scaleStartx != scaleStartx || this.scaleStarty != scaleStarty ||
                this.pSEToggleX != pSEToggleX || this.pSEToggleY != pSEToggleY || this.pSEToggleZ != pSEToggleZ || this.autoOrient != autoOrient || this.autoOrient2D != autoOrient2D ||
                this.randomHueRange != randomHueRange || this.randomSatRange != randomSatRange || this.randomLumRange != randomLumRange || this.randomColorCount != randomColorCount || this.randomColorToggle != randomColorToggle ||
                this.calculationType != calculationType || this.forcePitch != forcePitch || this.forceYaw != forceYaw || this.forceVelocity != forceVelocity || this.forceRoll != forceRoll || this.fps != fps ||
                this.forceRandomCount != forceRandomCount || this.forceRandomPitch != forceRandomPitch || this.forceRandomRoll != forceRandomRoll || this.forceRandomYaw != forceRandomYaw || this.forceRandomVelocity != forceRandomVelocity ||
                this.floorActionType != floorActionType || this.floorJudgementType != floorJudgementType || this.floorToggle != floorToggle || this.floorY != floorY || this.floorWaitTime != floorWaitTime || this.floorFadeTime != floorFadeTime ||
                this.zSortToggle != zSortToggle || this.airResistance != airResistance || this.bounceFactor != bounceFactor || this.bounceEnergyLoss != bounceEnergyLoss || this.bounceGravity != bounceGravity ||
                this.loopToggle != loopToggle || this.opacityMapMidPoint != opacityMapMidPoint || this.opacityMapToggle != opacityMapToggle || this.opacityMapEase != opacityMapEase || this.bounceCount != bounceCount;

            //もしこれに該当しなかったら描画更新しない
            if (isFirst || IsInputChanged || this.frame != frame || this.count != count || this.startx != startx || this.starty != starty || this.startz != startz ||
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
                this.billboard != billboard || /*this.billboardXY != billboardXY ||*/ this.billboardXYZ != billboardXYZ || this.randomSyScaleToggle != randomSyScaleToggle ||
                this.pSEToggleX != pSEToggleX || this.pSEToggleY != pSEToggleY || this.pSEToggleZ != pSEToggleZ || this.grTerminationToggle != grTerminationToggle ||
                this.startColor != startColor || this.endColor != endColor || this.scaleStartx != scaleStartx || this.scaleStarty != scaleStarty || this.scaleStartz != scaleStartz ||
                this.autoOrient != autoOrient || this.autoOrient2D != autoOrient2D || this.randomHueRange != randomHueRange || this.randomSatRange != randomSatRange || this.randomLumRange != randomLumRange ||
                this.randomColorCount != randomColorCount || this.randomColorToggle != randomColorToggle || this.calculationType != calculationType || this.forcePitch != forcePitch || this.forceYaw != forceYaw || this.forceRoll != forceRoll || this.forceVelocity != forceVelocity ||
                this.fps != fps || this.forceRandomCount != forceRandomCount || this.forceRandomPitch != forceRandomPitch || this.forceRandomRoll != forceRandomRoll || this.forceRandomYaw != forceRandomYaw || this.forceRandomVelocity != forceRandomVelocity ||
                this.floorActionType != floorActionType || this.floorJudgementType != floorJudgementType || this.floorToggle != floorToggle || this.floorY != floorY || this.floorWaitTime != floorWaitTime || this.floorFadeTime != floorFadeTime ||
                this.zSortToggle != zSortToggle || this.focusToggle != focusToggle || this.focusFadeToggle != focusFadeToggle || this.focusDepth != focusDepth || this.focusRange != focusRange || this.focusMaxBlur != focusMaxBlur || this.focusFadeMinOpacity != focusFadeMinOpacity ||
                this.focusFallOffBlur != focusFallOffBlur || this.airResistance != airResistance || this.bounceFactor != bounceFactor || this.bounceEnergyLoss != bounceEnergyLoss || this.bounceGravity != bounceGravity ||
                this.loopToggle != loopToggle || this.opacityMapMidPoint != opacityMapMidPoint || this.opacityMapToggle != opacityMapToggle || this.opacityMapEase != opacityMapEase || this.bounceCount != bounceCount ||
                this.trailToggle != trailToggle || this.trailCount != trailCount || this.trailInterval != trailInterval || this.trailFade != trailFade || this.trailScale != trailScale ||
                this.cullingToggle != cullingToggle || this.cullingBuffer != cullingBuffer
                /* || this.easingType != item.EasingType || this.easingMode != item.EasingMode */)
            {
                //this.に記憶
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
                //this.billboardXY = billboardXY;
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

                if (pSEToggleX)
                {
                    this.endx = this.startx + this.endx;
                }
                if (pSEToggleY)
                {
                    this.endy = this.starty + this.endy;
                }
                if (pSEToggleZ)
                {
                    this.endz = this.startz + this.endz;
                }

                //FloorとForceは常に計算する必要があるので、arrayNeedsUpdateには含めない
                int numberOfGroupsForce = (int)Math.Ceiling((double)this.count / Math.Max(1, this.forceRandomCount));

                var dc = devices.DeviceContext;

                int numberofGroupsColor = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomColorCount));
                // randomColorToggle が ON で、必要な数 (numberofGroupsColor) がプールの数 (hueEffects.Count) より多いか？
                if (this.randomColorToggle && numberofGroupsColor > this.hueEffects.Count)
                {
                    // 3. 足りない分だけ new してプールに追加
                    int needed = numberofGroupsColor - this.hueEffects.Count;
                    for (int i = 0; i < needed; i++)
                    {
                        // new は「足りなくなった時」に「1回だけ」実行される
                        this.hueEffects.Add(new Particles3DHueCustomEffect(this.devices));
                    }
                }

                //---random関連---
                if (arrayNeedsUpdate) // arrayNeedsUpdate が true ならば、this.変数への代入は実行済み
                {

                    // 配列初期化時は、**this.に代入済みの新しい** this.randomSeed を参照する
                    staticRng = new Random(this.randomSeed);

                    // グループ数は count に固定（個体ごとにランダムな値を割り当てる）
                    EnsureArraySize(ref this.curveFactorArray, this.count);

                    for (int i = 0; i < this.count; i++)
                    {
                        // 1.0 を中心に +/- (curveRange / 2) の範囲でブレさせる
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

                            // ここで float を long (フレーム数) にキャストして GetValue に渡す
                            long T_launch_long = (long)T_launch_float;
                            // 個体 i の射出フレーム時間 T_launch を計算

                            // 射出時間におけるアニメーション値（EndX の値）を評価し、配列に記憶
                            float launch_StartX = (float)item.StartX.GetValue(T_launch_long, length, fps);
                            float launch_StartY = (float)item.StartY.GetValue(T_launch_long, length, fps);
                            float launch_StartZ = (float)item.StartZ.GetValue(T_launch_long, length, fps);

                            this.fixedPositionStartXArray[i] = launch_StartX;
                            this.fixedPositionStartYArray[i] = launch_StartY;
                            this.fixedPositionStartZArray[i] = launch_StartZ;

                            // 射出時間におけるアニメーション値（EndX の値）を評価し、配列に記憶
                            float launch_EndX = (float)item.EndX.GetValue(T_launch_long, length, fps);
                            float launch_EndY = (float)item.EndY.GetValue(T_launch_long, length, fps);
                            float launch_EndZ = (float)item.EndZ.GetValue(T_launch_long, length, fps);

                            this.fixedPositionEndXArray[i] = launch_EndX;
                            this.fixedPositionEndYArray[i] = launch_EndY;
                            this.fixedPositionEndZArray[i] = launch_EndZ;

                            // pSEToggle (始点終点同期) の処理
                            if (pSEToggleX)
                                this.fixedPositionEndXArray[i] = launch_StartX + launch_EndX;
                            if (pSEToggleY)
                                this.fixedPositionEndYArray[i] = launch_StartY + launch_EndY;
                            if (pSEToggleZ)
                                this.fixedPositionEndZArray[i] = launch_StartZ + launch_EndZ;

                            this.fixedGravityXArray[i] = (float)item.GravityX.GetValue(T_launch_long, length, fps);
                            this.fixedGravityYArray[i] = (float)item.GravityY.GetValue(T_launch_long, length, fps);
                            this.fixedGravityZArray[i] = (float)item.GravityZ.GetValue(T_launch_long, length, fps);
                            // スケール固定値の計算と格納
                            this.fixedScaleStartXArray[i] = (float)item.ScaleStartX.GetValue(T_launch_long, length, fps);
                            this.fixedScaleStartYArray[i] = (float)item.ScaleStartY.GetValue(T_launch_long, length, fps);
                            this.fixedScaleStartZArray[i] = (float)item.ScaleStartZ.GetValue(T_launch_long, length, fps);
                            this.fixedScaleEndXArray[i] = (float)item.ScaleX.GetValue(T_launch_long, length, fps);
                            this.fixedScaleEndYArray[i] = (float)item.ScaleY.GetValue(T_launch_long, length, fps);
                            this.fixedScaleEndZArray[i] = (float)item.ScaleZ.GetValue(T_launch_long, length, fps);

                            // 回転固定値の計算と格納
                            this.fixedRotationEndXArray[i] = (float)item.EndRotationX.GetValue(T_launch_long, length, fps);
                            this.fixedRotationEndYArray[i] = (float)item.EndRotationY.GetValue(T_launch_long, length, fps);
                            this.fixedRotationEndZArray[i] = (float)item.EndRotationZ.GetValue(T_launch_long, length, fps);
                            this.fixedRotationStartXArray[i] = (float)item.StartRotationX.GetValue(T_launch_long, length, fps);
                            this.fixedRotationStartYArray[i] = (float)item.StartRotationY.GetValue(T_launch_long, length, fps);
                            this.fixedRotationStartZArray[i] = (float)item.StartRotationZ.GetValue(T_launch_long, length, fps);

                            // 透明度固定値の計算と格納
                            this.fixedOpacityStartArray[i] = (float)item.StartOpacity.GetValue(T_launch_long, length, fps);
                            this.fixedOpacityMidArray[i] = (float)item.OpacityMapMidPoint.GetValue(T_launch_long, length, fps);
                            this.fixedOpacityEndArray[i] = (float)item.EndOpacity.GetValue(T_launch_long, length, fps);

                            this.fixedForcePitchArray[i] = (float)item.ForcePitch.GetValue(T_launch_long, length, fps);
                            this.fixedForceYawArray[i] = (float)item.ForceYaw.GetValue(T_launch_long, length, fps);
                            this.fixedForceRollArray[i] = (float)item.ForceRoll.GetValue(T_launch_long, length, fps);
                            this.fixedForceVelocityArray[i] = (float)item.ForceVelocity.GetValue(T_launch_long, length, fps);
                        });
                    }
                    // (disposer に Collect して RemoveAndDisposeAll<HueCorrectionCustomEffect>() でもOK)

                    if (randomColorToggle)
                    {
                        // 2. HSLのランダムオフセット配列を計算 (これは元のコード)
                        int numberOfGroupsColor = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomColorCount));
                        EnsureArraySize(ref this.randomHueOffsetArray, numberOfGroupsColor);
                        EnsureArraySize(ref this.randomSatOffsetArray, numberOfGroupsColor);
                        EnsureArraySize(ref this.randomLumOffsetArray, numberOfGroupsColor);

                        float satRange = this.randomSatRange / 100.0f;
                        float lumRange = this.randomLumRange / 100.0f;

                        for (int g = 0; g < numberOfGroupsColor; g++)
                        {
                            // a. HSLオフセットを計算 (元のコード)
                            float hueOffset = (float)staticRng.NextDouble() * this.randomHueRange - (this.randomHueRange / 2.0f);
                            this.randomHueOffsetArray[g] = hueOffset;
                            float satOffset = (float)staticRng.NextDouble() * satRange - (satRange / 2.0f);
                            this.randomSatOffsetArray[g] = satOffset;
                            float lumOffset = (float)staticRng.NextDouble() * lumRange - (lumRange / 2.0f);
                            this.randomLumOffsetArray[g] = lumOffset;


                            var hueEffect = this.hueEffects[g];

                            // c.パラメータを「更新」する
                            hueEffect.HueShift = hueOffset;
                            hueEffect.SaturationFactor = 1.0f + satOffset;
                            hueEffect.LuminanceFactor = 1.0f + lumOffset;
                            hueEffect.Factor = 1.0f;

                        }
                    }
                    EnsureArraySize(ref this.randomForcePitchArray, numberOfGroupsForce);
                    EnsureArraySize(ref this.randomForceYawArray, numberOfGroupsForce);
                    EnsureArraySize(ref this.randomForceRollArray, numberOfGroupsForce);
                    EnsureArraySize(ref this.randomForceVelocityArray, numberOfGroupsForce);

                    for (int g = 0; g < numberOfGroupsForce; g++)
                    {
                        int i = g * Math.Max(1, this.forceRandomCount); // グループの代表インデックス

                        // 1. ベースとなる値を取得 (軌道固定を考慮)
                        float basePitch = this.forcePitch;
                        float baseYaw = this.forceYaw;
                        float baseRoll = this.forceRoll;
                        float baseVelocity = this.forceVelocity;

                        if (this.fixedTrajectory)
                        {
                            if (this.fixedForcePitchArray != null && i < this.fixedForcePitchArray.Length)
                                basePitch = this.fixedForcePitchArray[i];
                            if (this.fixedForceYawArray != null && i < this.fixedForceYawArray.Length)
                                baseYaw = this.fixedForceYawArray[i];
                            if (this.fixedForceRollArray != null && i < this.fixedForceRollArray.Length)
                                baseRoll = this.fixedForceRollArray[i];
                            if (this.fixedForceVelocityArray != null && i < this.fixedForceVelocityArray.Length)
                                baseVelocity = this.fixedForceVelocityArray[i];
                        }

                        // 2. ランダムなオフセットを計算
                        float p_offset = (float)staticRng.NextDouble() * this.forceRandomPitch - (this.forceRandomPitch / 2.0f);
                        float y_offset = (float)staticRng.NextDouble() * this.forceRandomYaw - (this.forceRandomYaw / 2.0f);
                        float r_offset = (float)staticRng.NextDouble() * this.forceRandomRoll - (this.forceRandomRoll / 2.0f);
                        float v_offset = (float)staticRng.NextDouble() * this.forceRandomVelocity - (this.forceRandomVelocity / 2.0f);

                        // 3. 最終的な値を配列に保存
                        this.randomForcePitchArray[g] = basePitch + p_offset;
                        this.randomForceYawArray[g] = baseYaw + y_offset;
                        this.randomForceRollArray[g] = baseRoll + r_offset;
                        this.randomForceVelocityArray[g] = baseVelocity + v_offset;
                    }

                    // --- X軸の配列計算 ---
                    int numberOfGroupsX = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomXCount));

                    // --- 始点 (StartX) の配列計算 ---
                    EnsureArraySize(ref this.startXArray, numberOfGroupsX);
                    for (int g = 0; g < numberOfGroupsX; g++)
                    {
                        // グループgの代表インデックスiを計算
                        int i = g * Math.Max(1, this.randomXCount);

                        // ベースとなる始点を決定
                        float baseStartX = this.startx; // デフォルトは現在の始点
                        if (this.fixedTrajectory && this.fixedPositionStartXArray != null && this.fixedPositionStartXArray.Length > i)
                        {
                            baseStartX = this.fixedPositionStartXArray[i]; // 軌道固定時は、射出時の始点
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartXRange - (this.randomStartXRange / 2.0f);
                        //ベース始点(baseStartX)に対してオフセットをかける
                        this.startXArray[g] = baseStartX + randomOffset;
                    }

                    // --- 終点 (EndX) の配列計算 ---
                    EnsureArraySize(ref this.targetXArray, numberOfGroupsX);
                    for (int g = 0; g < numberOfGroupsX; g++)
                    {
                        int i = g * Math.Max(1, this.randomXCount);

                        // 1. ベースとなる終点(baseEnd)を決定
                        float baseEnd = this.endx; // デフォルトは現在の終点
                        if (this.fixedTrajectory && this.fixedPositionEndXArray != null && this.fixedPositionEndXArray.Length > i)
                        {
                            baseEnd = this.fixedPositionEndXArray[i]; // 軌道固定時は、射出時の終点
                        }

                        // 2. Random SEToggleX によるオフセットの適用
                        if (this.randomSEToggleX && this.startXArray != null && this.startXArray.Length > g)
                        {
                            // ベースとなる始点も固定軌道を考慮する
                            float baseStartX = this.startx; // デフォルト
                            if (this.fixedTrajectory && this.fixedPositionStartXArray != null && this.fixedPositionStartXArray.Length > i)
                            {
                                baseStartX = this.fixedPositionStartXArray[i]; // 軌道固定時
                            }


                            float startXOffset = this.startXArray[g] - baseStartX; // ランダム始点と「ベース始点」の差
                            baseEnd += startXOffset;
                        }

                        // 3. EndX のランダム範囲の適用
                        float randomOffset = (float)staticRng.NextDouble() * this.randomEndXRange - (this.randomEndXRange / 2.0f);
                        this.targetXArray[g] = baseEnd + randomOffset;
                    }

                    // --- Y軸の配列計算 ---
                    int numberOfGroupsY = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomYCount));

                    // --- 始点 (StartY) の配列計算 ---
                    EnsureArraySize(ref this.startYArray, numberOfGroupsY);
                    for (int g = 0; g < numberOfGroupsY; g++)
                    {
                        int i = g * Math.Max(1, this.randomYCount);

                        // ベースとなる始点を決定 
                        float baseStartY = this.starty;
                        if (this.fixedTrajectory && this.fixedPositionStartYArray != null && this.fixedPositionStartYArray.Length > i)
                        {
                            baseStartY = this.fixedPositionStartYArray[i];
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartYRange - (this.randomStartYRange / 2.0f);
                        this.startYArray[g] = baseStartY + randomOffset;
                    }

                    // --- 終点 (EndY) の配列計算 ---
                    EnsureArraySize(ref this.targetYArray, numberOfGroupsY);
                    for (int g = 0; g < numberOfGroupsY; g++)
                    {
                        int i = g * Math.Max(1, this.randomYCount);

                        // 1. ベースとなる終点(baseEnd)を決定
                        float baseEnd = this.endy;
                        if (this.fixedTrajectory && this.fixedPositionEndYArray != null && this.fixedPositionEndYArray.Length > i)
                        {
                            baseEnd = this.fixedPositionEndYArray[i];
                        }

                        // 2. Random SEToggleY によるオフセットの適用
                        if (this.randomSEToggleY && this.startYArray != null && this.startYArray.Length > g)
                        {
                            // ベースとなる始点も固定軌道を考慮する
                            float baseStartY = this.starty;
                            if (this.fixedTrajectory && this.fixedPositionStartYArray != null && this.fixedPositionStartYArray.Length > i)
                            {
                                baseStartY = this.fixedPositionStartYArray[i];
                            }

                            float startYOffset = this.startYArray[g] - baseStartY;
                            baseEnd += startYOffset;
                        }

                        // 3. EndY のランダム範囲の適用
                        float randomOffset = (float)staticRng.NextDouble() * this.randomEndYRange - (this.randomEndYRange / 2.0f);
                        this.targetYArray[g] = baseEnd + randomOffset;
                    }

                    // --- Z軸の配列計算 ---
                    int numberOfGroupsZ = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomZCount));

                    // --- 始点 (StartZ) の配列計算 ---
                    EnsureArraySize(ref this.startZArray, numberOfGroupsZ);
                    for (int g = 0; g < numberOfGroupsZ; g++)
                    {
                        int i = g * Math.Max(1, this.randomZCount);

                        //ベースとなる始点を決定
                        float baseStartZ = this.startz;
                        if (this.fixedTrajectory && this.fixedPositionStartZArray != null && this.fixedPositionStartZArray.Length > i)
                        {
                            baseStartZ = this.fixedPositionStartZArray[i];
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartZRange - (this.randomStartZRange / 2.0f);
                        this.startZArray[g] = baseStartZ + randomOffset;
                    }

                    // --- 終点 (EndZ) の配列計算 ---
                    EnsureArraySize(ref this.targetZArray, numberOfGroupsZ);
                    for (int g = 0; g < numberOfGroupsZ; g++)
                    {
                        int i = g * Math.Max(1, this.randomZCount);

                        // 1. ベースとなる終点(baseEnd)を決定
                        float baseEnd = this.endz;
                        if (this.fixedTrajectory && this.fixedPositionEndZArray != null && this.fixedPositionEndZArray.Length > i)
                        {
                            baseEnd = this.fixedPositionEndZArray[i];
                        }

                        // 2. Random SEToggleZ によるオフセットの適用
                        if (this.randomSEToggleZ && this.startZArray != null && this.startZArray.Length > g)
                        {
                            //ベースとなる始点も固定軌道を考慮する
                            float baseStartZ = this.startz;
                            if (this.fixedTrajectory && this.fixedPositionStartZArray != null && this.fixedPositionStartZArray.Length > i)
                            {
                                baseStartZ = this.fixedPositionStartZArray[i];
                            }

                            float startZOffset = this.startZArray[g] - baseStartZ;
                            baseEnd += startZOffset;
                        }

                        // 3. EndZ のランダム範囲の適用
                        float randomOffset = (float)staticRng.NextDouble() * this.randomEndZRange - (this.randomEndZRange / 2.0f);
                        this.targetZArray[g] = baseEnd + randomOffset;
                    }

                    // --- Scale X軸の配列計算 ---
                    int numberOfGroupsScaleX = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomScaleCount));
                    EnsureArraySize(ref this.startScaleXArray, numberOfGroupsScaleX);
                    EnsureArraySize(ref this.targetScaleXArray, numberOfGroupsScaleX);
                    for (int g = 0; g < numberOfGroupsScaleX; g++)
                    {
                        int i = g * Math.Max(1, this.randomScaleCount);

                        float baseStartX = this.scaleStartx;
                        if (this.fixedTrajectory && this.fixedScaleStartXArray != null && this.fixedScaleStartXArray.Length > i)
                        {
                            baseStartX = this.fixedScaleStartXArray[i];
                        }

                        // 1. 始点のランダム値計算
                        float startXOffset = (float)staticRng!.NextDouble() * this.randomStartScaleRange - (this.randomStartScaleRange / 2.0f);
                        this.startScaleXArray[g] = baseStartX + startXOffset;
                    }
                    for (int g = 0; g < numberOfGroupsScaleX; g++)
                    {
                        int i = g * Math.Max(1, this.randomScaleCount);

                        // 2. 終点のベース値決定 (FixedTrajectoryのチェック)
                        float baseEnd = this.scalex; // ScaleX のアニメーション値 (終端値) を一旦ベースにする

                        if (this.fixedTrajectory && this.fixedScaleEndXArray != null && this.fixedScaleEndXArray.Length > i)
                        {
                            // 軌道固定がONなら、射出時の ScaleX 固定値をベースにする
                            baseEnd = this.fixedScaleEndXArray[i];
                        }

                        // 3. SEToggle の適用 (始点ランダムオフセットを終点に反映)
                        if (this.randomSEScaleToggle && this.startScaleXArray != null && this.startScaleXArray.Length > g)
                        {
                            //ベースとなる始点も固定軌道を考慮する
                            float baseStartX = this.scaleStartx;
                            if (this.fixedTrajectory && this.fixedScaleStartXArray != null && this.fixedScaleStartXArray.Length > i)
                            {
                                baseStartX = this.fixedScaleStartXArray[i];
                            }

                            float startOffset = this.startScaleXArray[g] - baseStartX;
                            baseEnd += startOffset;
                        }

                        // 4. 終点のランダムオフセットを適用
                        float endXOffset = (float)staticRng.NextDouble() * this.randomEndScaleRange - (this.randomEndScaleRange / 2.0f);
                        this.targetScaleXArray[g] = baseEnd + endXOffset;
                    }

                    // --- Scale Y軸の配列計算 ---
                    int numberOfGroupsScaleY = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomScaleCount));
                    EnsureArraySize(ref this.startScaleYArray, numberOfGroupsScaleY);
                    EnsureArraySize(ref this.targetScaleYArray, numberOfGroupsScaleY);
                    for (int g = 0; g < numberOfGroupsScaleY; g++)
                    {
                        int i = g * Math.Max(1, this.randomScaleCount);

                        float baseStartY = this.scaleStarty;
                        if (this.fixedTrajectory && this.fixedScaleStartYArray != null && this.fixedScaleStartYArray.Length > i)
                        {
                            baseStartY = this.fixedScaleStartYArray[i];
                        }

                        // 1. 始点のランダム値計算
                        float startYOffset = (float)staticRng!.NextDouble() * this.randomStartScaleRange - (this.randomStartScaleRange / 2.0f);
                        this.startScaleYArray[g] = baseStartY + startYOffset;
                    }
                    for (int g = 0; g < numberOfGroupsScaleY; g++)
                    {
                        int i = g * Math.Max(1, this.randomScaleCount);

                        // 2. 終点のベース値決定 (FixedTrajectoryのチェック)
                        float baseEnd = this.scaley; // ScaleY のアニメーション値 (終端値) を一旦ベースにする

                        if (this.fixedTrajectory && this.fixedScaleEndYArray != null && this.fixedScaleEndYArray.Length > i)
                        {
                            // 軌道固定がONなら、射出時の ScaleY 固定値をベースにする
                            baseEnd = this.fixedScaleEndYArray[i];
                        }

                        // 3. SEToggle の適用 (始点ランダムオフセットを終点に反映)
                        if (this.randomSEScaleToggle && this.startScaleYArray != null && this.startScaleYArray.Length > g)
                        {
                            // ベースとなる始点も固定軌道を考慮する
                            float baseStartY = this.scaleStarty;
                            if (this.fixedTrajectory && this.fixedScaleStartYArray != null && this.fixedScaleStartYArray.Length > i)
                            {
                                baseStartY = this.fixedScaleStartYArray[i];
                            }

                            float startOffset = this.startScaleYArray[g] - baseStartY;
                            baseEnd += startOffset;
                        }

                        // 4. 終点のランダムオフセットを適用
                        float endYOffset = (float)staticRng.NextDouble() * this.randomEndScaleRange - (this.randomEndScaleRange / 2.0f);
                        this.targetScaleYArray[g] = baseEnd + endYOffset;
                    }
                    // --- Scale Z軸の配列計算 ---
                    int numberOfGroupsScaleZ = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomScaleCount));
                    EnsureArraySize(ref this.startScaleZArray, numberOfGroupsScaleZ);
                    EnsureArraySize(ref this.targetScaleZArray, numberOfGroupsScaleZ);
                    for (int g = 0; g < numberOfGroupsScaleZ; g++)
                    {
                        int i = g * Math.Max(1, this.randomScaleCount);

                        float baseStartZ = this.scaleStartz;
                        if (this.fixedTrajectory && this.fixedScaleStartZArray != null && this.fixedScaleStartZArray.Length > i)
                        {
                            baseStartZ = this.fixedScaleStartZArray[i];
                        }

                        // 1. 始点のランダム値計算
                        float startZOffset = (float)staticRng!.NextDouble() * this.randomStartScaleRange - (this.randomStartScaleRange / 2.0f);
                        this.startScaleZArray[g] = baseStartZ + startZOffset;
                    }
                    for (int g = 0; g < numberOfGroupsScaleZ; g++)
                    {
                        int i = g * Math.Max(1, this.randomScaleCount);

                        // 2. 終点のベース値決定 (FixedTrajectoryのチェック)
                        float baseEnd = this.scalez; // ScaleZ のアニメーション値 (終端値) を一旦ベースにする

                        if (this.fixedTrajectory && this.fixedScaleEndZArray != null && this.fixedScaleEndZArray.Length > i)
                        {
                            // 軌道固定がONなら、射出時の ScaleZ 固定値をベースにする
                            baseEnd = this.fixedScaleEndZArray[i];
                        }

                        // 3. SEToggle の適用 (始点ランダムオフセットを終点に反映)
                        if (this.randomSEScaleToggle && this.startScaleZArray != null && this.startScaleZArray.Length > g)
                        {
                            //ベースとなる始点も固定軌道を考慮する
                            float baseStartZ = this.scaleStartz;
                            if (this.fixedTrajectory && this.fixedScaleStartZArray != null && this.fixedScaleStartZArray.Length > i)
                            {
                                baseStartZ = this.fixedScaleStartZArray[i];
                            }

                            float startOffset = this.startScaleZArray[g] - baseStartZ;
                            baseEnd += startOffset;
                        }

                        // 4. 終点のランダムオフセットを適用
                        float endZOffset = (float)staticRng.NextDouble() * this.randomEndScaleRange - (this.randomEndScaleRange / 2.0f);
                        this.targetScaleZArray[g] = baseEnd + endZOffset;
                    }

                    //同期設定
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

                    //回転X
                    int numberOfGroupsRotX = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomRotXCount));
                    EnsureArraySize(ref this.startRotationXArray, numberOfGroupsRotX);
                    EnsureArraySize(ref this.targetRotationXArray, numberOfGroupsRotX);
                    for (int g = 0; g < numberOfGroupsRotX; g++)
                    {
                        //グループgの代表インデックスiを計算
                        int i = g * Math.Max(1, this.randomRotXCount);

                        // ベースとなる始点を決定
                        float baseStartX = this.startRotationX; // デフォルトは現在の始点
                        if (this.fixedTrajectory && this.fixedRotationStartXArray != null && this.fixedRotationStartXArray.Length > i)
                        {
                            baseStartX = this.fixedRotationStartXArray[i]; // 軌道固定時は、射出時の始点
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartRotXRange - (this.randomStartRotXRange / 2.0f);
                        //ベース始点(baseStartX)に対してオフセットをかける
                        this.startRotationXArray[g] = baseStartX + randomOffset;
                    }
                    for (int g = 0; g < numberOfGroupsRotX; g++)
                    {
                        int i = g * Math.Max(1, this.randomRotXCount);

                        // 1. ベースとなる終点(baseEnd)を決定
                        float baseEnd = this.endRotationX; // デフォルトは現在の終点
                        if (this.fixedTrajectory && this.fixedRotationEndXArray != null && this.fixedRotationEndXArray.Length > i)
                        {
                            baseEnd = this.fixedRotationEndXArray[i]; // 軌道固定時は、射出時の終点
                        }

                        // 2. Random SEToggleX によるオフセットの適用
                        if (this.randomSERotXToggle && this.startRotationXArray != null && this.startRotationXArray.Length > g)
                        {
                            //ベースとなる始点も固定軌道を考慮す
                            float baseStartX = this.startRotationX; // デフォルト
                            if (this.fixedTrajectory && this.fixedRotationStartXArray != null && this.fixedRotationStartXArray.Length > i)
                            {
                                baseStartX = this.fixedRotationStartXArray[i]; // 軌道固定時
                            }

                            float startXOffset = this.startRotationXArray[g] - baseStartX; // ランダム始点と「ベース始点」の差
                            baseEnd += startXOffset;
                        }

                        // 3. EndX のランダム範囲の適用
                        float randomOffset = (float)staticRng.NextDouble() * this.randomEndRotXRange - (this.randomEndRotXRange / 2.0f);
                        this.targetRotationXArray[g] = baseEnd + randomOffset;
                    }


                    // --- Rotation Y軸の配列計算 ---
                    int numberOfGroupsRotY = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomRotYCount));
                    EnsureArraySize(ref this.startRotationYArray, numberOfGroupsRotY);
                    EnsureArraySize(ref this.targetRotationYArray, numberOfGroupsRotY);
                    for (int g = 0; g < numberOfGroupsRotY; g++)
                    {
                        // グループgの代表インデックスiを計算
                        int i = g * Math.Max(1, this.randomRotYCount);

                        //ベースとなる始点を決定
                        float baseStartY = this.startRotationY; // デフォルトは現在の始点
                        if (this.fixedTrajectory && this.fixedRotationStartYArray != null && this.fixedRotationStartYArray.Length > i)
                        {
                            baseStartY = this.fixedRotationStartYArray[i]; // 軌道固定時は、射出時の始点
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartRotYRange - (this.randomStartRotYRange / 2.0f);
                        // ベース始点(baseStartY)に対してオフセットをかける
                        this.startRotationYArray[g] = baseStartY + randomOffset;
                    }
                    for (int g = 0; g < numberOfGroupsRotY; g++)
                    {
                        int i = g * Math.Max(1, this.randomRotYCount);

                        // 1. ベースとなる終点(baseEnd)を決定
                        float baseEnd = this.endRotationY; // デフォルトは現在の終点
                        if (this.fixedTrajectory && this.fixedRotationEndYArray != null && this.fixedRotationEndYArray.Length > i)
                        {
                            baseEnd = this.fixedRotationEndYArray[i]; // 軌道固定時は、射出時の終点
                        }

                        // 2. Random SEToggleY によるオフセットの適用
                        if (this.randomSERotYToggle && this.startRotationYArray != null && this.startRotationYArray.Length > g)
                        {
                            //ベースとなる始点も固定軌道を考慮する
                            float baseStartY = this.startRotationY; // デフォルト
                            if (this.fixedTrajectory && this.fixedRotationStartYArray != null && this.fixedRotationStartYArray.Length > i)
                            {
                                baseStartY = this.fixedRotationStartYArray[i]; // 軌道固定時
                            }

                            float startYOffset = this.startRotationYArray[g] - baseStartY; // ランダム始点と「ベース始点」の差
                            baseEnd += startYOffset;
                        }

                        // 3. EndY のランダム範囲の適用
                        float randomOffset = (float)staticRng.NextDouble() * this.randomEndRotYRange - (this.randomEndRotYRange / 2.0f);
                        this.targetRotationYArray[g] = baseEnd + randomOffset;
                    }

                    // --- Rotation Z軸の配列計算 ---
                    int numberOfGroupsRotZ = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomRotZCount));
                    EnsureArraySize(ref this.startRotationZArray, numberOfGroupsRotZ);
                    EnsureArraySize(ref this.targetRotationZArray, numberOfGroupsRotZ);
                    for (int g = 0; g < numberOfGroupsRotZ; g++)
                    {
                        //グループgの代表インデックスiを計算
                        int i = g * Math.Max(1, this.randomRotZCount);

                        //ベースとなる始点を決定
                        float baseStartZ = this.startRotationZ; // デフォルトは現在の始点
                        if (this.fixedTrajectory && this.fixedRotationStartZArray != null && this.fixedRotationStartZArray.Length > i)
                        {
                            baseStartZ = this.fixedRotationStartZArray[i]; // 軌道固定時は、射出時の始点
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartRotZRange - (this.randomStartRotZRange / 2.0f);
                        //ベース始点(baseStartZ)に対してオフセットをかける 
                        this.startRotationZArray[g] = baseStartZ + randomOffset;
                    }
                    for (int g = 0; g < numberOfGroupsRotZ; g++)
                    {
                        int i = g * Math.Max(1, this.randomRotZCount);

                        // 1. ベースとなる終点(baseEnd)を決定
                        float baseEnd = this.endRotationZ; // デフォルトは現在の終点
                        if (this.fixedTrajectory && this.fixedRotationEndZArray != null && this.fixedRotationEndZArray.Length > i)
                        {
                            baseEnd = this.fixedRotationEndZArray[i]; // 軌道固定時は、射出時の終点
                        }

                        // 2. Random SEToggleZ によるオフセットの適用
                        if (this.randomSERotZToggle && this.startRotationZArray != null && this.startRotationZArray.Length > g)
                        {
                            //ベースとなる始点も固定軌道を考慮する
                            float baseStartZ = this.startRotationZ; // デフォルト
                            if (this.fixedTrajectory && this.fixedRotationStartZArray != null && this.fixedRotationStartZArray.Length > i)
                            {
                                baseStartZ = this.fixedRotationStartZArray[i]; // 軌道固定時
                            }

                            float startZOffset = this.startRotationZArray[g] - baseStartZ; // ランダム始点と「ベース始点」の差
                            baseEnd += startZOffset;
                        }

                        // 3. EndZ のランダム範囲の適用
                        float randomOffset = (float)staticRng.NextDouble() * this.randomEndRotZRange - (this.randomEndRotZRange / 2.0f);
                        this.targetRotationZArray[g] = baseEnd + randomOffset;
                    }
                    //Opacity
                    int numberOfGroupsOpacity = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomOpacityCount));
                    EnsureArraySize(ref this.startOpacityArray, numberOfGroupsOpacity);
                    EnsureArraySize(ref this.targetOpacityArray, numberOfGroupsOpacity);
                    for (int g = 0; g < numberOfGroupsOpacity; g++)
                    {
                        // 1. 最小値・最大値を取得 (0-100スケール)
                        float minVal = Math.Min(this.randomStartOpacityRange, this.randomEndOpacityRange);
                        float maxVal = Math.Max(this.randomStartOpacityRange, this.randomEndOpacityRange);

                        // 2. 乱数を 0.0-1.0 で取得
                        float randomFactor = (float)staticRng!.NextDouble();

                        // 3. 最小値と最大値の間で線形補間 (0-100スケール)
                        float randomOpacity = minVal + (maxVal - minVal) * randomFactor;

                        // 4. 始点と終点の両方に、この固定ランダム値を設定
                        //    これで progress によらず不透明度が固定されます。
                        this.startOpacityArray[g] = randomOpacity;
                        this.targetOpacityArray[g] = randomOpacity;

                    }
                    //---floor---
                    EnsureArraySize(ref this.hitProgressArray, numberOfGroupsForce);// グループ数
                    EnsureArraySize(ref this.hitVelocityArray, numberOfGroupsForce);// 速度配列
                    //floor計算
                    if (this.floorToggle)
                    {
                        const int SIMULATION_STEPS = 30; // 30ステップで衝突判定（精度）
                        float FloorY = this.floorY;
                        float stepProgress = 1.0f / SIMULATION_STEPS; // 1ステップあたりの進行度

                        Parallel.For(0, numberOfGroupsForce, g =>
                        {
                            float hitProgress = float.MaxValue; // デフォルト = 衝突しない
                            Vector3 hitVelocity = Vector3.Zero; // デフォルト速度

                            // 1ステップ前の座標と進行度を保持
                            Vector3 prevPos = CalculatePosition_Internal(i: (g * this.forceRandomCount), progress: 0f); //
                            float prevProgress = 0f; // 進行度も保持

                            for (int step = 1; step <= SIMULATION_STEPS; step++)
                            {
                                float currentProgress = (float)step * stepProgress;
                                Vector3 currentPos = CalculatePosition_Internal(i: (g * this.forceRandomCount), progress: currentProgress);

                                bool hasHit = false;

                                // 床下判定 (Type 0)
                                if (this.floorJudgementType == 0 && currentPos.Y >= FloorY && prevPos.Y < FloorY)
                                {
                                    hasHit = true;
                                }
                                // 床上判定 (Type 1)
                                if (this.floorJudgementType == 1 && currentPos.Y <= FloorY && prevPos.Y > FloorY)
                                {
                                    hasHit = true;
                                }

                                if (hasHit)
                                {
                                    //ここから補間ロジック

                                    // 1. このステップでのY軸の総移動量
                                    float yTravelInStep = currentPos.Y - prevPos.Y;

                                    // 2. 衝突地点までのY軸移動量
                                    float yTravelToHit = FloorY - prevPos.Y;

                                    // 3. 補間係数 (0.0 ～ 1.0)
                                    float interpolationFactor = 0.0f;
                                    if (Math.Abs(yTravelInStep) > 0.0001f)
                                    {
                                        interpolationFactor = yTravelToHit / yTravelInStep;
                                        interpolationFactor = Math.Clamp(interpolationFactor, 0.0f, 1.0f);
                                    }

                                    // 4. このステップの進行度の「幅」
                                    float stepDuration = currentProgress - prevProgress;

                                    // 5. 真の衝突進行度 (hitProgress) を計算
                                    hitProgress = prevProgress + (stepDuration * interpolationFactor);

                                    // 6. 衝突速度 (Velocity) を計算
                                    //    (t_sec が 0 にならないようクランプ)
                                    float t_sec = (this.fps > 0) ? (stepDuration * (this.travelTime / this.fps)) : 0.01f;
                                    if (t_sec < 0.0001f) t_sec = 0.0001f;

                                    hitVelocity = (currentPos - prevPos) / t_sec;

                                    break; // 衝突したので、このグループ(g)のシミュレーションは終了
                                }

                                prevPos = currentPos;
                                prevProgress = currentProgress;
                            }
                            this.hitProgressArray[g] = hitProgress;
                            this.hitVelocityArray[g] = hitVelocity;
                        });
                    }
                }


                //---random関連終了---

                disposer.RemoveAndDispose(ref commandList);

                float loopDuration = this.count * this.cycleTime;
                if (loopDuration <= 0) loopDuration = 1;

                // 2. 描画に使う「基準時間」を決定
                float timeToUse = this.frame;


                commandList = dc.CreateCommandList();
                disposer.Collect(commandList);
                dc.Target = commandList;
                dc.BeginDraw();
                dc.Clear(null);

                int usedNodeCount = 0;

                //実際の描画の設計
                void draw(int i, float baseVirtualFrame)
                {
                    // --- 基本の進行度(mainProgress)を計算 ---
                    float mainProgress = float.NegativeInfinity;

                    if (this.loopToggle)
                    {
                        // --- ループモード (V5) ---
                        float T_base_start = i * this.cycleTime;
                        float timeInLoop = baseVirtualFrame % loopDuration;
                        float T_end_relative = T_base_start + this.travelTime;
                        float startFrameOfItem = (float)i * delayFramesPerItem;

                        if (timeInLoop < startFrameOfItem) return;

                        if (T_end_relative <= loopDuration)
                        {
                            if (timeInLoop >= T_base_start)
                            {
                                mainProgress = (timeInLoop - T_base_start) / this.travelTime;
                            }
                        }
                        else
                        {
                            float T_end_wrapped = T_end_relative % loopDuration;
                            float T_start_relative = T_base_start;
                            if (timeInLoop >= T_start_relative)
                            {
                                mainProgress = (timeInLoop - T_start_relative) / this.travelTime;
                            }
                            else
                            {
                                mainProgress = ((loopDuration - T_start_relative) + timeInLoop) / this.travelTime;
                            }
                        }
                    }
                    else
                    {
                        // --- 単発モード (V4) ---
                        float T_start = i * this.cycleTime;
                        float startFrameOfItem = (float)i * delayFramesPerItem;

                        if (baseVirtualFrame < startFrameOfItem) return;

                        if (baseVirtualFrame >= T_start)
                        {
                            mainProgress = (baseVirtualFrame - T_start) / this.travelTime;
                        }
                    }

                    if (float.IsNegativeInfinity(mainProgress)) return;

                    // 最も古い残像が完全に消滅していれば描画しない
                    float maxTrailLife = this.trailToggle ? (this.trailCount * this.trailInterval) : 0f;
                    if (mainProgress < 0f || mainProgress > 1.0f + maxTrailLife) return;

                    // --- 残像ループの開始 ---
                    int loopCount = this.trailToggle ? Math.Max(1, this.trailCount) : 1;

                    // 古い残像 → 本体 の順に描画
                    for (int t = loopCount - 1; t >= 0; t--)
                    {
                        float rawProgress = mainProgress - (t * this.trailInterval);

                        // 0.0未満（生まれる前）または 1.0超過（死んだ後）なら描画しない
                        if (rawProgress < 0.0f || rawProgress > 1.0f) continue;

                        // プールが足りなければ、その場で生成して追加する
                        if (usedNodeCount >= trailEffectPool.Count)
                        {
                            trailEffectPool.Add(new ParticleEffectNodes(devices.DeviceContext));
                        }

                        var nodes = trailEffectPool[usedNodeCount];
                        usedNodeCount++;

                        // 手動でCurveを計算してparamProgressを作る。

                        float paramProgress = rawProgress;
                        if (this.curveToggle && this.curveFactorArray != null && this.curveFactorArray.Length > i)
                        {
                            float curveFactor = this.curveFactorArray[i];
                            paramProgress = rawProgress * curveFactor;
                        }
                        // パラメータ計算用に 0-1 にクランプ
                        float clampedProgress = Math.Min(1.0f, Math.Max(0.0f, paramProgress));

                        // --- パラメータには clampedProgress を使う ---
                        float RotationX_progress = clampedProgress;
                        float RotationY_progress = clampedProgress;
                        float RotationZ_progress = clampedProgress;
                        float ScaleX_progress = clampedProgress;
                        float ScaleY_progress = clampedProgress;
                        float ScaleZ_progress = clampedProgress;
                        float Opacity_progress = clampedProgress;

                        // --- 固定軌道パラメータの取得 ---
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

                        // Random Scale
                        float finalScalex;
                        if (this.randomScaleToggle && targetScaleXArray != null && startScaleXArray != null && targetScaleXArray.Length > 0 && startScaleXArray.Length > 0)
                        {
                            safeCount = Math.Max(1, this.randomScaleCount);
                            groupIndex = i / safeCount;
                            int targetIndex = Math.Min(groupIndex, targetScaleXArray.Length - 1);
                            float startX_Random = startScaleXArray[targetIndex];
                            float targetX = targetScaleXArray[targetIndex];
                            // パラメータ用には paramProgress (Curve計算済み) を使う
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

                        // Random Rotation
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

                        // Opacity
                        float finalOpacity;
                        if (this.opacityMapToggle)
                        {
                            float op_start = current_startopacity;
                            float op_mid = current_midopacity;
                            float op_end = current_endopacity;
                            float power = 1.0f + this.opacityMapEase * 4.0f;

                            // Opacityマップ計算はclampedProgressを使用
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

                        // 残像のフェード処理
                        if (this.trailToggle && t > 0)
                        {
                            // 0.0(本体) ～ 1.0(最も古い残像) の進行度
                            float t_rate = (float)t / this.trailCount;

                            float fadeRate = 1.0f - ((float)t / this.trailCount) * this.trailFade;
                            finalOpacity *= Math.Max(0f, fadeRate);

                            // 残像の拡大縮小処理
                            float scaleMult = 1.0f + ((this.trailScale / 100f) - 1.0f) * t_rate;

                            // 負の値防止
                            scaleMult = Math.Max(0f, scaleMult);

                            finalScalex *= scaleMult;
                            finalScaley *= scaleMult;
                            finalScalez *= scaleMult;
                        }


                        // --- 座標計算には rawProgress (未加工) を渡す ---
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

                        // Floor判定 (paramProgressを使用)
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

                        // 色 (clampedProgressを使用)
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

                        nodes.colorEffect.SetInput(0, input, true);

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

                        using var hueOutput = (randomColorToggle && groupIndex < this.hueEffects.Count)
                                              ? this.hueEffects[groupIndex].Output
                                              : null;

                        if (hueOutput != null)
                        {
                            var hueEffect = this.hueEffects[groupIndex];
                            hueEffect.SetInput(0, colorOutput, true);
                            colorStageOutput = hueOutput;
                        }

                        // ブラー / ピント
                        float blurAmount = 0f;
                        float focusFadeFactor = 1.0f;

                        if (this.focusToggle)
                        {
                            Vector4 clipPos = Vector4.Transform(currentPosition, this.camera);
                            float particleDepth = (clipPos.W != 0) ? (clipPos.Z / clipPos.W) : 0f;
                            float outOfFocus = Math.Abs(particleDepth - this.focusDepth);
                            float blurFactor = 0.0f;
                            if (outOfFocus > this.focusRange)
                            {
                                float distance = outOfFocus - this.focusRange;
                                float normalizedDistance = Math.Clamp(distance / this.focusFallOffBlur, 0.0f, 1.0f);
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
                            nodes.blurEffect.SetInput(0, colorStageOutput, true);
                            nodes.blurEffect.StandardDeviation = blurAmount;
                            blurStageOutput = nodes.blurEffect.Output;
                        }
                        else
                        {
                            blurStageOutput = colorStageOutput;
                        }

                        nodes.opacityEffect.SetInput(0, blurStageOutput, true);

                        if (blurAmount > 0.1f && blurStageOutput is IDisposable d)
                        {
                            d.Dispose();
                        }
                        // 最終処理
                        float currentOpacity = finalOpacity / 100.0f;
                        currentOpacity *= focusFadeFactor;

                        nodes.opacityEffect.SetValue((int)OpacityProperties.Opacity, Math.Clamp(currentOpacity, 0.0f, 1.0f));
                        using var opacityOutput = nodes.opacityEffect.Output;

                        nodes.renderEffect.SetInput(0, opacityOutput, true);

                        // Matrix
                        Matrix4x4 cam = effectDescription.DrawDescription.Camera;
                        Vector3 camForward = new Vector3(cam.M31, cam.M32, cam.M33);
                        if (camForward == Vector3.Zero) camForward = new Vector3(0, 0, 1);
                        camForward = Vector3.Normalize(camForward);
                        float cameraYaw = (float)Math.Atan2(camForward.X, camForward.Z);

                        float finalPitch = currentRotX_rad;
                        float finalYaw = currentRotY_rad;
                        const float PI_HALF = (float)Math.PI / 2.0f;

                        // 向きを同期機能
                        if (this.autoOrient)
                        {
                            float futureProgress = Math.Min(1.0f, clampedProgress + 0.01f);
                            Vector3 futurePosition = CalculatePosition(i, futureProgress); // ここはparamProgressを使うべきだが、微差なのでparamでもrawでもOK
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
                                    float autoOrientYaw = targetYaw - ((float)Math.PI * rotation.Y / 180);
                                    float autoOrientPitch = targetPitch - ((float)Math.PI * rotation.X / 180);
                                    finalYaw = autoOrientYaw;
                                    finalPitch = autoOrientPitch;
                                }
                            }
                        }
                        //　ビルボード
                        Matrix4x4 finalRotationMatrix;
                        if (this.billboardXYZ)
                        {
                            Matrix4x4.Invert(cam, out var cameraWorldMatrix);
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

                        //最終的な描画
                        Vector3 currentScale = new Vector3(currentScalex, currentScaley, currentScalez);

                        nodes.renderEffect.TransformMatrix = finalRotationMatrix *
                                                       Matrix4x4.CreateScale(currentScale) *
                                                       Matrix4x4.CreateTranslation(new Vector3(currentx, currenty, currentz)) *
                                                       effectDescription.DrawDescription.Camera *
                                                       new Matrix4x4(1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, -0.001f, 0f, 0f, 0f, 1f);

                        using var renderOutput = nodes.renderEffect.Output;
                        dc.DrawImage(renderOutput);
                    }
                }

                // 描画順序を決定する変数

                this._drawList.Clear();

                Matrix4x4 cam = effectDescription.DrawDescription.Camera;

                if (this.zSortToggle)
                {
                    // --- 1. 旧Zソート (ワールドZ軸) ---
                    for (int i = 0; i < this.count; i++)
                    {
                        float progress = -1f; // -1 = 非描画

                        if (this.loopToggle)
                        {
                            // --- ケース1: ループ (V5) の progress 計算 ---
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
                            // --- ケース2: 一回だけ (V4) の progress 計算 ---
                            float T_start = i * this.cycleTime;
                            float T_end = T_start + this.travelTime;
                            if (timeToUse >= T_start && timeToUse < T_end)
                            {
                                progress = (timeToUse - T_start) / this.travelTime;
                            }
                        }

                        if (progress < 0f || progress > 1.0f)
                        {
                            continue; // アクティブでない
                        }

                        // 2. 座標を計算してZソートキーに追加
                        Vector3 currentPos = CalculatePosition(i, progress);

                        if (this.cullingToggle)
                        {
                            // 画面外ならリストに追加せずスキップ
                            float maxScale = Math.Max(this.scalex, Math.Max(this.scaley, this.scalez));

                            // ランダムスケール有効なら、ランダム範囲の最大値も考慮
                            if (this.randomScaleToggle)
                            {
                                maxScale += Math.Max(this.randomStartScaleRange, this.randomEndScaleRange);
                            }

                            // 修正したIsVisibleを呼び出し
                            if (!IsVisible(currentPos, cam, this.cullingBuffer, maxScale))
                            {
                                continue;
                            }
                        }

                        this._drawList.Add(new ItemDrawData { Index = i, SortKey = currentPos.Z }); // SortKey = Z
                    }
                }

                else
                {
                    for (int i = 0; i < this.count; i++)
                    {
                        float progress = -1f; // -1 = 非描画

                        if (this.loopToggle)
                        {
                            // --- ケース1: ループ (V5) の progress 計算 ---
                            float T_base_start = i * this.cycleTime;
                            float T_start_relative = T_base_start;
                            float timeInLoop = timeToUse % loopDuration; // timeToUseは(5)で計算済み
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
                            // --- ケース2: 一回だけ (V4) の progress 計算 ---
                            float T_start = i * this.cycleTime;
                            float T_end = T_start + this.travelTime;
                            if (timeToUse >= T_start && timeToUse < T_end)
                            {
                                progress = (timeToUse - T_start) / this.travelTime;
                            }
                        }

                        if (progress < 0f || progress > 1.0f)
                        {
                            continue; // アクティブでない
                        }

                        // 2. 座標を計算
                        Vector3 currentPos = CalculatePosition(i, progress);

                        if (this.cullingToggle)
                        {
                            // 画面外ならリストに追加せずスキップ
                            float maxScale = Math.Max(this.scalex, Math.Max(this.scaley, this.scalez));

                            // ランダムスケール有効なら、ランダム範囲の最大値も考慮
                            if (this.randomScaleToggle)
                            {
                                maxScale += Math.Max(this.randomStartScaleRange, this.randomEndScaleRange);
                            }

                            // 修正したIsVisibleを呼び出し
                            if (!IsVisible(currentPos, cam, this.cullingBuffer, maxScale))
                            {
                                continue;
                            }
                        }

                        Vector4 clipPos = Vector4.Transform(currentPos, cam);
                        float perspectiveZ = (clipPos.W != 0) ? (clipPos.Z / clipPos.W) : 0f;
                        this._drawList.Add(new ItemDrawData { Index = i, SortKey = perspectiveZ });
                    }

                    if (!this.fixedDraw)
                    {
                        _drawList.Sort((a, b) =>
                        {
                            // 'SortKey' は 0.0 (手前) ～ 1.0 (奥) の値
                            int distanceComparison = a.SortKey.CompareTo(b.SortKey);

                            if (distanceComparison != 0)
                            {
                                return distanceComparison;
                            }
                            else
                            {
                                return a.Index.CompareTo(b.Index);
                            }
                        });
                    }
                }

                if (this.reverseDraw >= 0.5f) // または this.reverseDraw == true など
                {
                    _drawList.Reverse();
                }
                foreach (var data in _drawList)
                {
                    // data.Index は、元のアイテムのインデックス i に相当します
                    draw(data.Index, timeToUse);
                }

                // ---邪魔なメモリ清掃---
                int threshold = (int)(usedNodeCount * 1.5) + 100;

                if (trailEffectPool.Count > threshold)
                {
                    // 余裕を持たせたラインより後ろを削除対象にする
                    int keepCount = usedNodeCount + 50;
                    if (keepCount < trailEffectPool.Count)
                    {
                        int removeCount = trailEffectPool.Count - keepCount;

                        // 1. 削除する分のDirect2Dリソースを解放
                        for (int k = 0; k < removeCount; k++)
                        {
                            // リストの後ろから順にアクセス
                            trailEffectPool[keepCount + k].Dispose();
                        }

                        // 2. リストから削除
                        trailEffectPool.RemoveRange(keepCount, removeCount);
                    }
                }

                dc.EndDraw();
                dc.Target = null;
                commandList.Close();

                isFirst = false;
                IsInputChanged = false;
            }



            return effectDescription.DrawDescription with
            {
                Rotation = default,
                Camera = Matrix4x4.Identity
            };
        }

        public struct ItemDrawData
        {
            public int Index;
            public float SortKey; // ソートキー
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

            // progress を 0.0-1.0 にクランプ
            progress = Math.Min(1.0f, Math.Max(0.0f, progress));

            // 「床にくっつく(Glue)」処理
            int groupIndex = i / Math.Max(1, this.forceRandomCount);
            float hitProgress = (this.floorToggle && this.hitProgressArray != null && groupIndex < this.hitProgressArray.Length)
                                ? this.hitProgressArray[groupIndex]
                                : float.MaxValue;

            if (progress < hitProgress)
            {
                // --- 1. 衝突していない（飛行中）---
                return CalculatePosition_Internal(i, progress);
            }
            else
            {
                // --- 2. 衝突時刻 (hitProgress) 以降 ---

                // 衝突した「瞬間」の座標と、その後の「経過時間(秒)」を計算
                Vector3 P0 = CalculatePosition_Internal(i, hitProgress); // 衝突地点
                P0.Y = this.floorY; // 床にスナップ
                float t_sec = (this.fps > 0) ? ((progress - hitProgress) * (this.travelTime / this.fps)) : 0f;

                switch (this.floorActionType)
                {
                    case 0: // --- Glue (接着) ---
                        {
                            // 衝突地点から動かない
                            return P0;
                        }

                    case 1: // --- Ice (滑走) ---
                        {
                            // 衝突時の速度(V0)を取得
                            Vector3 V0 = (this.hitVelocityArray != null && groupIndex < this.hitVelocityArray.Length)
                                        ? this.hitVelocityArray[groupIndex]
                                        : Vector3.Zero;

                            V0.Y = 0; // Y(垂直)の速度をゼロにする

                            //摩擦のロジックを追加
                            float friction = Math.Abs(this.bounceEnergyLoss - 1.0f);
                            V0 *= friction;
                            // 加速度 A はゼロ (重力なし)
                            return P0 + (V0 * t_sec); // (P = P0 + V0*t)
                        }

                    case 2: // --- Bounce (反射) ---
                        {
                            // 1. 最初の衝突状態を取得
                            Vector3 P_current = P0; // P0 = 衝突地点
                            Vector3 V_current = (this.hitVelocityArray != null && groupIndex < this.hitVelocityArray.Length)
                                                ? this.hitVelocityArray[groupIndex]
                                                : Vector3.Zero;

                            // 2. UIからパラメータ取得
                            float bFactor = this.bounceFactor;
                            float eLossMultiplier = 1.0f - this.bounceEnergyLoss;

                            // 床判定タイプによって反射方向を決める
                            // 0 (床上): 上に跳ねる (-1.0)
                            // 1 (床下): 下に跳ねる (+1.0)
                            float bounceDir = (this.floorJudgementType == 1) ? 1.0f : -1.0f;

                            // 3. 「最初の」バウンドと損失を適用
                            // 強制的に指定した方向(bounceDir)へ速度を向けます
                            V_current.Y = bounceDir * Math.Abs(V_current.Y) * bFactor;
                            V_current *= eLossMultiplier; // 全体に損失

                            // 4. 水平方向の重力 (X, Z) を取得
                            Vector3 A_XZ = new Vector3(current_gravityX, 0, current_gravityZ);

                            // 5. バウンドループのための擬似的な重力 (g) を定義
                            float g_pseudo = this.bounceGravity;

                            float t_remaining = t_sec; // 衝突からの総経過時間

                            // 6. 解析的バウンドループ
                            int safetyCounter = 0;
                            while (t_remaining > 0.0001f)
                            {
                                safetyCounter++;
                                if (safetyCounter > this.bounceCount)
                                {
                                    // 無限ループ防止
                                    return P_current;
                                }

                                // 7. 停止条件のチェック
                                // 床上(Dir=-1)なら V > -1.0 で停止、床下(Dir=1)なら V < 1.0 で停止とみなす
                                if ((bounceDir < 0 && V_current.Y > -1.0f) || (bounceDir > 0 && V_current.Y < 1.0f))
                                {
                                    V_current.Y = 0;
                                    float t_sq = t_remaining * t_remaining;
                                    return P_current + (V_current * t_remaining) + (0.5f * A_XZ * t_sq);
                                }

                                // 8. この1回のバウンド（放物線）にかかる時間を計算
                                float t_arc_duration = -2.0f * V_current.Y / g_pseudo;

                                // 戻ってこない場合（天井反射で重力が下向き等）の対策
                                // 時間が負、または極小の場合は「もう戻ってこない」としてそのまま移動させて終了
                                if (t_arc_duration <= 0.0001f)
                                {
                                    float t_sq = t_remaining * t_remaining;
                                    // 垂直重力も含めて移動
                                    Vector3 A_fall = new Vector3(A_XZ.X, g_pseudo, A_XZ.Z);
                                    return P_current + (V_current * t_remaining) + (0.5f * A_fall * t_sq);
                                }

                                // 9. 「残りの時間」は、この「バウンド時間」の中か？
                                if (t_remaining <= t_arc_duration)
                                {
                                    // YES: これが最後の軌道。
                                    float t_sq = t_remaining * t_remaining;
                                    Vector3 A_arc = new Vector3(A_XZ.X, g_pseudo, A_XZ.Z);
                                    return P_current + (V_current * t_remaining) + (0.5f * A_arc * t_sq);
                                }

                                // NO: このバウンドを「完了」し、まだ時間が余っている。

                                // 10. 「完了した」バウンド時間分、t_remaining を減らす
                                t_remaining -= t_arc_duration;

                                // 11. このバウンドが完了した時点の「座標」を計算 (次のP0)
                                float t_arc_sq = t_arc_duration * t_arc_duration;
                                Vector3 A_arc_full = new Vector3(A_XZ.X, g_pseudo, A_XZ.Z);
                                Vector3 P_end_arc = P_current + (V_current * t_arc_duration) + (0.5f * A_arc_full * t_arc_sq);
                                P_end_arc.Y = this.floorY; // 床にスナップ

                                // 12. このバウンドが完了した時点の「速度」を計算 (次のV0)
                                Vector3 V_end_arc = V_current + (A_arc_full * t_arc_duration);

                                // 13. 次のループのための状態を更新
                                P_current = P_end_arc;
                                V_current = V_end_arc;

                                // 14. 次のバウンドの「反発」と「エネルギー損失」を適用
                                V_current.Y = bounceDir * Math.Abs(V_current.Y) * bFactor;
                                V_current *= eLossMultiplier;
                            }

                            return P_current;
                        }

                    default: // 不明なタイプならGlueと同じ
                        return P0;
                }
            }
        }

        // 指定した個体(i)の、指定した進行度(progress)における3D座標を計算する
        private Vector3 CalculatePosition_Internal(int i, float progress)
        {
            progress = ApplyAirResistance(progress, this.airResistance);

            float PositionX_progress = progress;
            float PositionY_progress = progress;
            float PositionZ_progress = progress;

            //---軌道固定系統--- (draw(i) からコピペ)
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

            //---random関連--- (draw(i) からコピペ)
            if (this.calculationType == 1)
            {
                // 1. 経過時間
                float t_sec = (this.fps > 0) ? (progress * (this.travelTime / this.fps)) : 0f;
                float t_squared = t_sec * t_sec;

                // 2. 初期位置
                Vector3 P0 = new Vector3(current_startx, current_starty, current_startz);

                // 3. 加速度ベクトル
                Vector3 A = new Vector3(current_gravityX, current_gravityY, current_gravityZ);

                // 4. 現在の物理パラメータを取得 (ランダム/固定を考慮)
                int groupIndex = i / Math.Max(1, this.forceRandomCount);

                float currentPitch = this.forcePitch;
                float currentYaw = this.forceYaw;
                float currentRoll = this.forceRoll;
                float currentVelocity = this.forceVelocity;

                // (RandomForceToggle が ON で、配列が存在し、インデックスが有効なら)
                if (this.forceRandomCount > 0 && this.randomForcePitchArray != null && groupIndex < this.randomForcePitchArray.Length && this.randomForceYawArray != null && this.randomForceRollArray != null && this.randomForceVelocityArray != null)
                {
                    // 事前計算したランダム値（固定軌道も反映済み）を使用
                    currentPitch = this.randomForcePitchArray[groupIndex];
                    currentYaw = this.randomForceYawArray[groupIndex];
                    currentRoll = this.randomForceRollArray[groupIndex];
                    currentVelocity = this.randomForceVelocityArray[groupIndex];
                }
                // (ランダムがOFFで、固定軌道がONなら)
                else if (this.fixedTrajectory && this.fixedForcePitchArray != null && i < this.fixedForcePitchArray.Length && this.fixedForceYawArray != null && this.fixedForceRollArray != null && this.fixedForceVelocityArray != null)
                {
                    // 固定軌道の値を使用
                    currentPitch = this.fixedForcePitchArray[i];
                    currentYaw = this.fixedForceYawArray[i];
                    currentRoll = this.fixedForceRollArray[i];
                    currentVelocity = this.fixedForceVelocityArray[i];
                }

                float pitchRad = MathF.PI * currentPitch / 180f;
                float yawRad = MathF.PI * currentYaw / 180f;
                float rollRad = MathF.PI * currentRoll / 180f;

                // 4. 角度をクォータニオンに変換
                Quaternion qYawPitch = Quaternion.CreateFromYawPitchRoll(yawRad, pitchRad, 0f);
                Quaternion qRollWorld = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rollRad);
                Quaternion finalRot = Quaternion.Normalize(Quaternion.Multiply(qRollWorld, qYawPitch));

                // 最終的な方向
                Vector3 direction = Vector3.Transform(Vector3.UnitZ, finalRot);
                Vector3 V0 = direction * currentVelocity;

                // 7. 最終座標
                Vector3 finalPosition = P0 + (V0 * t_sec) + (0.5f * A * t_squared);

                return finalPosition;
            }
            else
            {
                float currentx_base = 0f;
                float currenty_base = 0f;
                float currentz_base = 0f;
                // X座標の計算
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

                // Y座標の計算
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

                // Z座標の計算
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

                //---Gravity--- (draw(i) からコピペ)
                float progressSquared = progress * progress;
                if (grTerminationToggle)
                {
                    progressSquared = (progress * progress - progress);
                }

                float gravityOffsetX = current_gravityX * progressSquared;
                float gravityOffsetY = current_gravityY * progressSquared;
                float gravityOffsetZ = current_gravityZ * progressSquared;
                //---Gravity end---

                float currentx = currentx_base + gravityOffsetX;
                float currenty = currenty_base + gravityOffsetY;
                float currentz = currentz_base + gravityOffsetZ;
                //---Gravity and current---

                return new Vector3(currentx, currenty, currentz);
            }
            // 測定終了

        }

        private float ApplyAirResistance(float progress, float resistance)
        {
            if (resistance <= 0.001f) return progress; // 抵抗なし(線形)
            if (progress >= 0.999f) return 1.0f;     // ほぼゴール

            // MathF.Pow の指数 1.0 (線形) -> 5.0 (強いカーブ) にマッピング
            float power = 1.0f + resistance * 4.0f;

            // EaseOut (Power)
            return 1.0f - MathF.Pow(1.0f - progress, power);
        }

        public struct HslColor
        {
            public double H, S, L;
        }
        void EnsureArraySize<T>(ref T[]? array, int length)
        {
            if (array == null || array.Length != length)
            {
                array = new T[length];
            }
        }
        private bool IsVisible(Vector3 worldPos, Matrix4x4 cameraMatrix, float buffer, float particleScale)
        {
            // 1. カメラ行列を使って「カメラから見た相対座標」に変換
            Vector4 clipPos = Vector4.Transform(worldPos, cameraMatrix);

            float w = clipPos.W;

            // 2. パーティクルの大きさを計算
            float baseSize = Math.Max(this.imageWidth, this.imageHeight);
            float radius = (baseSize / 2.0f) * (particleScale / 100.0f);

            // 3. 【手前/奥の判定】 
            // 中心(w)がマイナスでも、半径(radius)分だけ粘らせる
            // バッファ(buffer)も少し加味する
            float nearClipLimit = -(radius * 1.5f); // 少し余裕を持たせる(*1.5)

            if (w < nearClipLimit)
            {
                return false; // 完全にカメラの後ろに行った
            }

            float baseWidth = this.projectWidth;
            float baseHeight = this.projectHeight;
            float baseWidthHalf = baseWidth / 2f;
            float baseHeightHalf = baseHeight / 2f;

            // カメラからの距離（奥行き）を取得
            float depth = Math.Abs(clipPos.Z);

            // 3. その深度における「画面の端」の座標を計算
            float allowedHalfWidth = baseWidthHalf + (depth * baseWidthHalf / 1000f);

            // Y軸（高さ）はアスペクト比(16:9)に合わせて計算
            float allowedHalfHeight = baseHeightHalf + (depth * (baseHeightHalf / 1000f));

            // 4. バッファ（余裕）を適用
            // bufferが0.5なら、さらに1.5倍外側まで許容
            float marginScale = 1.0f + buffer;
            float limitX = allowedHalfWidth * marginScale;
            float limitY = allowedHalfHeight * marginScale;

            // 5. 判定
            // 変換後の座標(viewPos)が、計算した許容範囲(limit)に収まっているか
            if (Math.Abs(clipPos.X) > limitX) return false;
            if (Math.Abs(clipPos.Y) > limitY) return false;

            return true;
        }

        static Window? GetYmmMainWindow()
        {
            // Application.Current が null の場合やウィンドウがない場合を考慮
            if (Application.Current == null) return null;

            foreach (Window w in Application.Current.Windows)
            {
                if (w.GetType().FullName == "YukkuriMovieMaker.Views.MainView")
                {
                    return w;
                }
            }
            return null;
        }

        // リフレクションでプロパティの値を取得する
        static dynamic? GetProp(dynamic obj, string propName)
        {
            if (obj == null) return null;
            Type type = obj.GetType();
            PropertyInfo? info = type.GetProperty(propName);
            return info?.GetValue(obj);
        }

        // 解像度情報の更新処理
        private void UpdateProjectInfo()
        {
            // Application.Current が null なら何もしない
            if (Application.Current == null) return;

            try
            {
                // UIスレッド上で実行するように依頼する
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var window = GetYmmMainWindow();
                    if (window == null) return;

                    // ViewModel階層を掘っていく
                    dynamic? mainVM = window.DataContext;
                    dynamic? statusBarVM = GetProp(mainVM, "StatusBarViewModel");
                    dynamic? statusBarVal = GetProp(statusBarVM, "Value");
                    dynamic? videoInfoProp = GetProp(statusBarVal, "VideoInfo");

                    // VideoInfo.Value を取得
                    string? videoInfoString = GetProp(videoInfoProp, "Value");

                    // 文字列解析: "1920x1080 60fps 48000Hz"
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
                // エラーが出ても止まらないようにする
                Debug.WriteLine("Project Info Fetch Error: " + ex.Message);
            }
        }

        public void ClearInput()
        {
        }

        public void Dispose()
        {

            // 1. disposerによる解放処理（内部リソースをクリーンアップ）
            disposer.Dispose();

            // 2. 追加: CommandListを明示的に解放 (disposerで解放されていなければここで解放)
            if (commandList != null && commandList is IDisposable disposableCommandList)
            {
                disposableCommandList.Dispose();
            }
            this.commandList = null;

            foreach (var effect in this.hueEffects)
            {
                effect.Dispose();
            }
            this.hueEffects.Clear();

            foreach (var nodeSet in this.trailEffectPool)
            {
                nodeSet.Dispose();
            }
            this.trailEffectPool.Clear();

            this.input = null; // 参照を切る

        }

        public void SetInput(ID2D1Image? input)
        {
            this.input = input;
            IsInputChanged = true;
        }
    }
}
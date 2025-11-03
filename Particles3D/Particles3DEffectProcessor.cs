using Particles3D;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Media;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Windows.Networking.NetworkOperators;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

//--Todo--
//UIのメモリもうちょっと何とかしたいところはあるけど、しゃーない。
//初期と終端を同期するようにしたい。ただし、初期を動かすことで終端もつられて動く。しかし、終端は固定されることはない。(済)
//不透明度もうちょっと直す。(済)
//ScaleX,Y,Z同期設定を作る。(済)
//回転をX,Y,Zわける。(済)
//ScaleX,Y,Zの初期値作る
//ScaleとOpacityのfixedStartArrayつくる
//上からみてもビルボードver作成

//--いつかできたら--
//floor機能(Glueだけでも)
//random HSL
namespace Particles3D
{
    internal class Particles3DEffectProcessor : IVideoEffectProcessor, IDisposable
    {
        DisposeCollector disposer = new();

        Random? staticRng;
        float[]? targetXArray; // ★目標Xを格納する配列
        float[]? startXArray;  // ★StartXのランダム始端値
        float[]? targetYArray; // ★目標Yを格納する配列
        float[]? startYArray;  // ★StartYのランダム始端値
        float[]? targetZArray; // ★目標Zを格納する配列
        float[]? startZArray;  // ★StartZのランダム始端値

        float[]? startScaleXArray;
        float[]? targetScaleXArray;
        float[]? startScaleYArray;
        float[]? targetScaleYArray;
        float[]? startScaleZArray;
        float[]? targetScaleZArray;

        // ★ Rotation (始点/終点) のランダム配列
        float[]? startRotationXArray;
        float[]? targetRotationXArray;
        float[]? startRotationYArray;
        float[]? targetRotationYArray;
        float[]? startRotationZArray;
        float[]? targetRotationZArray;

        // ★ Opacity (始点/終点) のランダム配列
        float[]? startOpacityArray;
        float[]? targetOpacityArray;

        float[]? curveFactorArray;
        float[]? fixedPositionStartXArray;
        float[]? fixedPositionStartYArray;
        float[]? fixedPositionStartZArray;
        float[]? fixedPositionEndXArray;    // 固定された EndX 値
        float[]? fixedPositionEndYArray;    // 固定された EndY 値
        float[]? fixedPositionEndZArray;    // 固定された EndZ 値
        float[]? fixedScaleEndXArray;
        float[]? fixedScaleEndYArray;
        float[]? fixedScaleEndZArray;
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

        readonly Particles3DEffect item;

        IGraphicsDevicesAndContext devices;

        ID2D1Image? input;
        ID2D1CommandList? commandList;

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
        bool randomSEScaleToggle, randomSERotXToggle, randomSERotYToggle, randomSERotZToggle, randomSEOpacityToggle; // Start/End連動トグル
        bool billboard;
        bool grTerminationToggle;
        bool randomSyScaleToggle;
        bool pSEToggleX, pSEToggleY, pSEToggleZ;

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

            var scalex = (float)item.ScaleX.GetValue(frame, length, fps);
            var scaley = (float)item.ScaleY.GetValue(frame, length, fps);
            var scalez = (float)item.ScaleZ.GetValue(frame, length, fps);

            var startopacity = (float)item.StartOpacity.GetValue(frame, length, fps);
            var endopacity = (float)item.EndOpacity.GetValue(frame, length, fps);

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

            var randomSeed = (int)item.RandomSeed.GetValue(frame, length, fps);

            var rotation = effectDescription.DrawDescription.Rotation;
            var camera = effectDescription.DrawDescription.Camera;

            var cycleTime = (float)item.CycleTime.GetValue(frame, length, fps);
            var travelTime = (float)item.TravelTime.GetValue(frame, length, fps);

            float delayFramesPerItem = delaytime * fps / 1000.0f;

            var gravityX = (float)item.GravityX.GetValue(frame, length, fps);
            var gravityY = (float)item.GravityY.GetValue(frame, length, fps);
            var gravityZ = (float)item.GravityZ.GetValue(frame, length, fps);

            var curveRange = (float)item.CurveRange.GetValue(frame, length, fps);
            var curveToggle = item.CurveToggle;

            var fixedTrajectory = item.FixedTrajectory;

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
            var randomSEOpacityToggle = item.RandomSEOpacityToggle;

            var billboard = item.BillboardDraw;

            var grTerminationToggle = item.GrTerminationToggle;

            var randomSyScaleToggle = item.RandomSyScaleToggle;

            var pSEToggleX = item.PSEToggleX;
            var pSEToggleY = item.PSEToggleY;
            var pSEToggleZ = item.PSEToggleZ;

            //---random関連---
            // randomXCountが0だと0除算や計算がおかしくなるので、1以上に強制する
            int SafeRandomXCount = Math.Max(1, randomXCount);

            // グループの数
            int numberOfGroups = (int)Math.Ceiling((double)count / SafeRandomXCount);

            // 配列に必要なサイズは、[グループ0の目標]...[グループN-1の目標] + [全体の最終目標] の N+1 個
            int arraySize = numberOfGroups + 1;

            // arrayNeedsUpdate のチェックは、this.に代入する前に行う
            bool arrayNeedsUpdate = isFirst || this.randomSeed != randomSeed ||
                this.randomXCount != randomXCount || this.randomStartXRange != randomStartXRange || this.randomEndXRange != randomEndXRange || this.endx != endx || this.randomSEToggleX != randomSEToggleX ||
                this.randomYCount != randomYCount || this.randomStartYRange != randomStartYRange || this.randomEndYRange != randomEndYRange || this.endy != endy || this.randomSEToggleY != randomSEToggleY ||
                this.randomZCount != randomZCount || this.randomStartZRange != randomStartZRange || this.randomEndZRange != randomEndZRange || this.endz != endz || this.randomSEToggleZ != randomSEToggleZ ||
                this.curveRange != curveRange || this.curveToggle != curveToggle || this.fixedTrajectory != fixedTrajectory ||
                this.gravityX != gravityX || this.gravityY != gravityY || this.gravityZ != gravityZ || this.grTerminationToggle != grTerminationToggle ||
                this.endopacity != endopacity ||
                this.endRotationX != endRotationX || this.endRotationY != endRotationY || this.endRotationZ != endRotationZ ||
                this.scalex != scalex || this.scaley != scaley || this.scalez != scalez || this.randomSyScaleToggle != randomSyScaleToggle ||
                this.randomScaleCount != randomScaleCount || this.randomStartScaleRange != randomStartScaleRange || this.randomEndScaleRange != randomEndScaleRange || this.randomSEScaleToggle != randomSEScaleToggle ||
                this.randomRotXCount != randomRotXCount || this.randomStartRotXRange != randomStartRotXRange || this.randomEndRotXRange != randomEndRotXRange || this.randomSERotXToggle != randomSERotXToggle ||
                this.randomRotYCount != randomRotYCount || this.randomStartRotYRange != randomStartRotYRange || this.randomEndRotYRange != randomEndRotYRange || this.randomSERotYToggle != randomSERotYToggle ||
                this.randomRotZCount != randomRotZCount || this.randomStartRotZRange != randomStartRotZRange || this.randomEndRotZRange != randomEndRotZRange || this.randomSERotZToggle != randomSERotZToggle ||
                this.randomOpacityCount != randomOpacityCount || this.randomStartOpacityRange != randomStartOpacityRange || this.randomEndOpacityRange != randomEndOpacityRange || this.randomSEOpacityToggle != randomSEOpacityToggle ||
                this.billboard != billboard || this.startx != startx || this.starty != starty || this.startz != startz ||
                this.pSEToggleX != pSEToggleX || this.pSEToggleY != pSEToggleY || this.pSEToggleZ != pSEToggleZ;

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
                this.randomOpacityToggle != randomOpacityToggle || this.randomOpacityCount != randomOpacityCount || this.randomStartOpacityRange != randomStartOpacityRange || this.randomEndOpacityRange != randomEndOpacityRange || this.randomSEOpacityToggle != randomSEOpacityToggle ||
                this.billboard != billboard || this.randomSyScaleToggle != randomSyScaleToggle ||
                this.pSEToggleX != pSEToggleX || this.pSEToggleY != pSEToggleY || this.pSEToggleZ != pSEToggleZ || this.grTerminationToggle != grTerminationToggle
                /* || this.easingType != item.EasingType || this.easingMode != item.EasingMode */)
            {

                //this.に記憶
                this.frame = frame;
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
                this.randomSEOpacityToggle = randomSEOpacityToggle;
                this.billboard = billboard;
                this.grTerminationToggle = grTerminationToggle;
                this.randomSyScaleToggle = randomSyScaleToggle;
                this.pSEToggleX = pSEToggleX;
                this.pSEToggleY = pSEToggleY;
                this.pSEToggleZ = pSEToggleZ;


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

                //---random関連---
                if (arrayNeedsUpdate) // arrayNeedsUpdate が true ならば、this.変数への代入は実行済み
                {
                    // 配列初期化時は、**this.に代入済みの新しい** this.randomSeed を参照する
                    staticRng = new Random(this.randomSeed);

                    // グループ数は count に固定（個体ごとにランダムな値を割り当てる）
                    this.curveFactorArray = new float[this.count];

                    for (int i = 0; i < this.count; i++)
                    {
                        // 1.0 を中心に +/- (curveRange / 2) の範囲でブレさせるのが一般的
                        float factor = 1.0f + ((float)staticRng.NextDouble() * curveRange - (curveRange / 2.0f));
                        this.curveFactorArray[i] = factor;
                    }
                    if (this.fixedTrajectory)
                    {
                        this.fixedPositionStartXArray = new float[this.count];
                        this.fixedPositionStartYArray = new float[this.count];
                        this.fixedPositionStartZArray = new float[this.count];
                        this.fixedPositionEndXArray = new float[this.count];
                        this.fixedPositionEndYArray = new float[this.count];
                        this.fixedPositionEndZArray = new float[this.count];
                        this.fixedGravityXArray = new float[this.count];
                        this.fixedGravityYArray = new float[this.count];
                        this.fixedGravityZArray = new float[this.count];
                        this.fixedScaleEndXArray = new float[this.count];
                        this.fixedScaleEndYArray = new float[this.count];
                        this.fixedScaleEndZArray = new float[this.count];

                        // ★ 回転固定配列の初期化
                        this.fixedRotationStartXArray = new float[this.count];
                        this.fixedRotationStartYArray = new float[this.count];
                        this.fixedRotationStartZArray = new float[this.count];
                        this.fixedRotationEndXArray = new float[this.count];
                        this.fixedRotationEndYArray = new float[this.count];
                        this.fixedRotationEndZArray = new float[this.count];

                        // ★ 透明度固定配列の初期化
                        this.fixedOpacityEndArray = new float[this.count];
                        // ※ スケール、回転、不透明度も固定したいなら、配列と計算を追加

                        for (int i = 0; i < this.count; i++)
                        {
                            float T_launch_float = i * this.cycleTime;

                            // ★ ここで float を long (フレーム数) にキャストして GetValue に渡す
                            long T_launch_long = (long)T_launch_float;
                            // 個体 i の射出フレーム時間 T_launch を計算

                            // 射出時間におけるアニメーション値（EndX の値）を評価し、配列に記憶
                            float launch_StartX = (float)item.StartX.GetValue(T_launch_long, length, fps);
                            float launch_StartY = (float)item.StartY.GetValue(T_launch_long, length, fps);
                            float launch_StartZ = (float)item.StartZ.GetValue(T_launch_long, length, fps);

                            this.fixedPositionStartXArray[i] = launch_StartX;
                            this.fixedPositionStartYArray[i] = launch_StartY;
                            this.fixedPositionStartZArray[i] = launch_StartZ;
                            // ★★★ 始点記憶ここまで ★★★

                            // 射出時間におけるアニメーション値（EndX の値）を評価し、配列に記憶
                            float launch_EndX = (float)item.EndX.GetValue(T_launch_long, length, fps);
                            float launch_EndY = (float)item.EndY.GetValue(T_launch_long, length, fps);
                            float launch_EndZ = (float)item.EndZ.GetValue(T_launch_long, length, fps);

                            this.fixedPositionEndXArray[i] = launch_EndX;
                            this.fixedPositionEndYArray[i] = launch_EndY;
                            this.fixedPositionEndZArray[i] = launch_EndZ;

                            // pSEToggle (始点終点同期) の処理
                            // ※pSEToggleがONの時は、始点の値 (launch_StartX) を使って計算
                            if (pSEToggleX)
                                this.fixedPositionEndXArray[i] = launch_StartX + launch_EndX; // あなたの元のロジック
                            if (pSEToggleY)
                                this.fixedPositionEndYArray[i] = launch_StartY + launch_EndY; // あなたの元のロジック
                            if (pSEToggleZ)
                                this.fixedPositionEndZArray[i] = launch_StartZ + launch_EndZ; // あなたの元のロジック

                            this.fixedGravityXArray[i] = (float)item.GravityX.GetValue(T_launch_long, length, fps);
                            this.fixedGravityYArray[i] = (float)item.GravityY.GetValue(T_launch_long, length, fps);
                            this.fixedGravityZArray[i] = (float)item.GravityZ.GetValue(T_launch_long, length, fps);
                            // ... 他の End 系も同様 ...
                            this.fixedScaleEndXArray[i] = (float)item.ScaleX.GetValue(T_launch_long, length, fps);
                            this.fixedScaleEndYArray[i] = (float)item.ScaleY.GetValue(T_launch_long, length, fps);
                            this.fixedScaleEndZArray[i] = (float)item.ScaleZ.GetValue(T_launch_long, length, fps);

                            // ★ 回転固定値の計算と格納
                            this.fixedRotationEndXArray[i] = (float)item.EndRotationX.GetValue(T_launch_long, length, fps);
                            this.fixedRotationEndYArray[i] = (float)item.EndRotationY.GetValue(T_launch_long, length, fps);
                            this.fixedRotationEndZArray[i] = (float)item.EndRotationZ.GetValue(T_launch_long, length, fps);
                            this.fixedRotationStartXArray[i] = (float)item.StartRotationX.GetValue(T_launch_long, length, fps);
                            this.fixedRotationStartYArray[i] = (float)item.StartRotationY.GetValue(T_launch_long, length, fps);
                            this.fixedRotationStartZArray[i] = (float)item.StartRotationZ.GetValue(T_launch_long, length, fps);

                            // ★ 透明度固定値の計算と格納
                            this.fixedOpacityEndArray[i] = (float)item.EndOpacity.GetValue(T_launch_long, length, fps);
                        }
                    }


                    // --- X軸の配列計算 ---
                    int numberOfGroupsX = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomXCount));

                    // --- 始点 (StartX) の配列計算 ---
                    this.startXArray = new float[numberOfGroupsX];
                    for (int g = 0; g < numberOfGroupsX; g++)
                    {
                        // ★★★ グループgの代表インデックスiを計算 ★★★
                        int i = g * Math.Max(1, this.randomXCount);

                        // ★★★ ベースとなる始点を決定 ★★★
                        float baseStartX = this.startx; // デフォルトは現在の始点
                        if (this.fixedTrajectory && this.fixedPositionStartXArray != null && this.fixedPositionStartXArray.Length > i)
                        {
                            baseStartX = this.fixedPositionStartXArray[i]; // 軌道固定時は、射出時の始点
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartXRange - (this.randomStartXRange / 2.0f);
                        // ★★★ ベース始点(baseStartX)に対してオフセットをかける ★★★
                        this.startXArray[g] = baseStartX + randomOffset;
                    }

                    // --- 終点 (EndX) の配列計算 ---
                    this.targetXArray = new float[numberOfGroupsX];
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
                            // ★★★ ベースとなる始点も固定軌道を考慮する ★★★
                            float baseStartX = this.startx; // デフォルト
                            if (this.fixedTrajectory && this.fixedPositionStartXArray != null && this.fixedPositionStartXArray.Length > i)
                            {
                                baseStartX = this.fixedPositionStartXArray[i]; // 軌道固定時
                            }
                            // ★★★ 修正ここまで ★★★

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
                    this.startYArray = new float[numberOfGroupsY];
                    for (int g = 0; g < numberOfGroupsY; g++)
                    {
                        int i = g * Math.Max(1, this.randomYCount);

                        // ★★★ ベースとなる始点を決定 ★★★
                        float baseStartY = this.starty;
                        if (this.fixedTrajectory && this.fixedPositionStartYArray != null && this.fixedPositionStartYArray.Length > i)
                        {
                            baseStartY = this.fixedPositionStartYArray[i];
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartYRange - (this.randomStartYRange / 2.0f);
                        this.startYArray[g] = baseStartY + randomOffset;
                    }

                    // --- 終点 (EndY) の配列計算 ---
                    this.targetYArray = new float[numberOfGroupsY];
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
                            // ★★★ ベースとなる始点も固定軌道を考慮する ★★★
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
                    this.startZArray = new float[numberOfGroupsZ];
                    for (int g = 0; g < numberOfGroupsZ; g++)
                    {
                        int i = g * Math.Max(1, this.randomZCount);

                        // ★★★ ベースとなる始点を決定 ★★★
                        float baseStartZ = this.startz;
                        if (this.fixedTrajectory && this.fixedPositionStartZArray != null && this.fixedPositionStartZArray.Length > i)
                        {
                            baseStartZ = this.fixedPositionStartZArray[i];
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartZRange - (this.randomStartZRange / 2.0f);
                        this.startZArray[g] = baseStartZ + randomOffset;
                    }

                    // --- 終点 (EndZ) の配列計算 ---
                    this.targetZArray = new float[numberOfGroupsZ];
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
                            // ★★★ ベースとなる始点も固定軌道を考慮する ★★★
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
                    this.startScaleXArray = new float[numberOfGroupsScaleX];
                    this.targetScaleXArray = new float[numberOfGroupsScaleX];

                    for (int g = 0; g < numberOfGroupsScaleX; g++)
                    {
                        int i = g * Math.Max(1, this.randomScaleCount);

                        // 1. 始点のランダム値計算
                        float startXOffset = (float)staticRng!.NextDouble() * this.randomStartScaleRange - (this.randomStartScaleRange / 2.0f);
                        this.startScaleXArray[g] = this.scalex + startXOffset; // this.scalex は Start/End 共通の ScaleX のアニメーション値

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
                            // 始点ランダム値とベース始点(this.scalex)の差分を計算
                            float startOffset = this.startScaleXArray[g] - this.scalex;
                            baseEnd += startOffset;
                        }

                        // 4. 終点のランダムオフセットを適用
                        float endXOffset = (float)staticRng.NextDouble() * this.randomEndScaleRange - (this.randomEndScaleRange / 2.0f);
                        this.targetScaleXArray[g] = baseEnd + endXOffset;
                    }

                    // --- Scale Y軸の配列計算 ---
                    int numberOfGroupsScaleY = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomScaleCount));
                    this.startScaleYArray = new float[numberOfGroupsScaleY];
                    this.targetScaleYArray = new float[numberOfGroupsScaleY];

                    for (int g = 0; g < numberOfGroupsScaleY; g++)
                    {
                        int i = g * Math.Max(1, this.randomScaleCount);

                        // 1. 始点のランダム値計算
                        float startYOffset = (float)staticRng!.NextDouble() * this.randomStartScaleRange - (this.randomStartScaleRange / 2.0f);
                        this.startScaleYArray[g] = this.scaley + startYOffset; // this.scalex は Start/End 共通の ScaleX のアニメーション値

                        // 2. 終点のベース値決定 (FixedTrajectoryのチェック)
                        float baseEnd = this.scaley; // ScaleX のアニメーション値 (終端値) を一旦ベースにする

                        if (this.fixedTrajectory && this.fixedScaleEndYArray != null && this.fixedScaleEndYArray.Length > i)
                        {
                            // 軌道固定がONなら、射出時の ScaleX 固定値をベースにする
                            baseEnd = this.fixedScaleEndYArray[i];
                        }

                        // 3. SEToggle の適用 (始点ランダムオフセットを終点に反映)
                        if (this.randomSEScaleToggle && this.startScaleYArray != null && this.startScaleYArray.Length > g)
                        {
                            // 始点ランダム値とベース始点(this.scalex)の差分を計算
                            float startOffset = this.startScaleYArray[g] - this.scaley;
                            baseEnd += startOffset;
                        }

                        // 4. 終点のランダムオフセットを適用
                        float endYOffset = (float)staticRng.NextDouble() * this.randomEndScaleRange - (this.randomEndScaleRange / 2.0f);
                        this.targetScaleYArray[g] = baseEnd + endYOffset;
                    }
                    // --- Scale Z軸の配列計算 ---
                    int numberOfGroupsScaleZ = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomScaleCount));
                    this.startScaleZArray = new float[numberOfGroupsScaleZ];
                    this.targetScaleZArray = new float[numberOfGroupsScaleZ];

                    for (int g = 0; g < numberOfGroupsScaleZ; g++)
                    {
                        int i = g * Math.Max(1, this.randomScaleCount);

                        // 1. 始点のランダム値計算
                        float startZOffset = (float)staticRng!.NextDouble() * this.randomStartScaleRange - (this.randomStartScaleRange / 2.0f);
                        this.startScaleZArray[g] = this.scalez + startZOffset; // this.scalex は Start/End 共通の ScaleX のアニメーション値

                        // 2. 終点のベース値決定 (FixedTrajectoryのチェック)
                        float baseEnd = this.scalez; // ScaleX のアニメーション値 (終端値) を一旦ベースにする

                        if (this.fixedTrajectory && this.fixedScaleEndZArray != null && this.fixedScaleEndZArray.Length > i)
                        {
                            // 軌道固定がONなら、射出時の ScaleX 固定値をベースにする
                            baseEnd = this.fixedScaleEndZArray[i];
                        }

                        // 3. SEToggle の適用 (始点ランダムオフセットを終点に反映)
                        if (this.randomSEScaleToggle && this.startScaleZArray != null && this.startScaleZArray.Length > g)
                        {
                            // 始点ランダム値とベース始点(this.scalex)の差分を計算
                            float startOffset = this.startScaleZArray[g] - this.scalez;
                            baseEnd += startOffset;
                        }

                        // 4. 終点のランダムオフセットを適用
                        float endZOffset = (float)staticRng.NextDouble() * this.randomEndScaleRange - (this.randomEndScaleRange / 2.0f);
                        this.targetScaleZArray[g] = baseEnd + endZOffset;
                    }

                    if (this.randomSyScaleToggle)
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
                    this.startRotationXArray = new float[numberOfGroupsRotX];
                    this.targetRotationXArray = new float[numberOfGroupsRotX];
                    for (int g = 0; g < numberOfGroupsRotX; g++)
                    {
                        // ★★★ グループgの代表インデックスiを計算 ★★★
                        int i = g * Math.Max(1, this.randomRotXCount);

                        // ★★★ ベースとなる始点を決定 ★★★
                        float baseStartX = this.startRotationX; // デフォルトは現在の始点
                        if (this.fixedTrajectory && this.fixedRotationStartXArray != null && this.fixedRotationStartXArray.Length > i)
                        {
                            baseStartX = this.fixedRotationStartXArray[i]; // 軌道固定時は、射出時の始点
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartRotXRange - (this.randomStartRotXRange / 2.0f);
                        // ★★★ ベース始点(baseStartX)に対してオフセットをかける ★★★
                        this.startRotationXArray[g] = baseStartX + randomOffset;
                    }
                    for (int g = 0; g < numberOfGroupsX; g++)
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
                            // ★★★ ベースとなる始点も固定軌道を考慮する ★★★
                            float baseStartX = this.startRotationX; // デフォルト
                            if (this.fixedTrajectory && this.fixedRotationStartXArray != null && this.fixedRotationStartXArray.Length > i)
                            {
                                baseStartX = this.fixedRotationStartXArray[i]; // 軌道固定時
                            }
                            // ★★★ 修正ここまで ★★★

                            float startXOffset = this.startRotationXArray[g] - baseStartX; // ランダム始点と「ベース始点」の差
                            baseEnd += startXOffset;
                        }

                        // 3. EndX のランダム範囲の適用
                        float randomOffset = (float)staticRng.NextDouble() * this.randomEndRotXRange - (this.randomEndRotXRange / 2.0f);
                        this.targetRotationXArray[g] = baseEnd + randomOffset;
                    }


                    // --- Rotation Y軸の配列計算 ---
                    int numberOfGroupsRotY = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomRotYCount));
                    this.startRotationYArray = new float[numberOfGroupsRotY];
                    this.targetRotationYArray = new float[numberOfGroupsRotY];

                    for (int g = 0; g < numberOfGroupsRotY; g++)
                    {
                        // ★★★ グループgの代表インデックスiを計算 ★★★
                        int i = g * Math.Max(1, this.randomRotYCount);

                        // ★★★ ベースとなる始点を決定 ★★★
                        float baseStartY = this.startRotationY; // デフォルトは現在の始点
                        if (this.fixedTrajectory && this.fixedRotationStartYArray != null && this.fixedRotationStartYArray.Length > i)
                        {
                            baseStartY = this.fixedRotationStartYArray[i]; // 軌道固定時は、射出時の始点
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartRotYRange - (this.randomStartRotYRange / 2.0f);
                        // ★★★ ベース始点(baseStartY)に対してオフセットをかける ★★★
                        this.startRotationYArray[g] = baseStartY + randomOffset;
                    }
                    for (int g = 0; g < numberOfGroupsY; g++)
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
                            // ★★★ ベースとなる始点も固定軌道を考慮する ★★★
                            float baseStartY = this.startRotationY; // デフォルト
                            if (this.fixedTrajectory && this.fixedRotationStartYArray != null && this.fixedRotationStartYArray.Length > i)
                            {
                                baseStartY = this.fixedRotationStartYArray[i]; // 軌道固定時
                            }
                            // ★★★ 修正ここまで ★★★

                            float startYOffset = this.startRotationYArray[g] - baseStartY; // ランダム始点と「ベース始点」の差
                            baseEnd += startYOffset;
                        }

                        // 3. EndY のランダム範囲の適用
                        float randomOffset = (float)staticRng.NextDouble() * this.randomEndRotYRange - (this.randomEndRotYRange / 2.0f);
                        this.targetRotationYArray[g] = baseEnd + randomOffset;
                    }

                    // --- Rotation Z軸の配列計算 ---
                    int numberOfGroupsRotZ = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomRotZCount));
                    this.startRotationZArray = new float[numberOfGroupsRotZ];
                    this.targetRotationZArray = new float[numberOfGroupsRotZ];

                    for (int g = 0; g < numberOfGroupsRotZ; g++)
                    {
                        // ★★★ グループgの代表インデックスiを計算 ★★★
                        int i = g * Math.Max(1, this.randomRotZCount);

                        // ★★★ ベースとなる始点を決定 ★★★
                        float baseStartZ = this.startRotationZ; // デフォルトは現在の始点
                        if (this.fixedTrajectory && this.fixedRotationStartZArray != null && this.fixedRotationStartZArray.Length > i)
                        {
                            baseStartZ = this.fixedRotationStartZArray[i]; // 軌道固定時は、射出時の始点
                        }

                        float randomOffset = (float)staticRng.NextDouble() * this.randomStartRotZRange - (this.randomStartRotZRange / 2.0f);
                        // ★★★ ベース始点(baseStartZ)に対してオフセットをかける ★★★
                        this.startRotationZArray[g] = baseStartZ + randomOffset;
                    }
                    for (int g = 0; g < numberOfGroupsZ; g++)
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
                            // ★★★ ベースとなる始点も固定軌道を考慮する ★★★
                            float baseStartZ = this.startRotationZ; // デフォルト
                            if (this.fixedTrajectory && this.fixedRotationStartZArray != null && this.fixedRotationStartZArray.Length > i)
                            {
                                baseStartZ = this.fixedRotationStartZArray[i]; // 軌道固定時
                            }
                            // ★★★ 修正ここまで ★★★

                            float startZOffset = this.startRotationZArray[g] - baseStartZ; // ランダム始点と「ベース始点」の差
                            baseEnd += startZOffset;
                        }

                        // 3. EndZ のランダム範囲の適用
                        float randomOffset = (float)staticRng.NextDouble() * this.randomEndRotZRange - (this.randomEndRotZRange / 2.0f);
                        this.targetRotationZArray[g] = baseEnd + randomOffset;
                    }
                    //Opacity
                    /*
                    int numberOfGroupsOpacity = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomOpacityCount));
                    this.startOpacityArray = new float[numberOfGroupsOpacity];
                    this.targetOpacityArray = new float[numberOfGroupsOpacity];

                    for (int g = 0; g < numberOfGroupsOpacity; g++)
                    {
                        ///*
                        int i = g * Math.Max(1, this.randomOpacityCount);

                        // 1. 始点のランダム値計算
                        float startOffset = (float)staticRng!.NextDouble() * this.randomStartOpacityRange - (this.randomStartOpacityRange / 2.0f);
                        // this.startopacity は OpacityStart のアニメーション値
                        this.startOpacityArray[g] = this.startopacity + startOffset;
                        *//*
                        
                        int i = g * Math.Max(1, this.randomOpacityCount);

                        // 1. 始点のランダム値計算（ご要望の Min/Max Range 間でのランダム）
                        float rangeA = this.randomStartOpacityRange / 100.0f;
                        float rangeB = this.randomEndOpacityRange / 100.0f;

                        // 最小値と最大値を決定
                        float minVal = Math.Min(rangeA, rangeB);
                        float maxVal = Math.Max(rangeA, rangeB);

                        // 0.0f から 1.0f の乱数
                        float randomFactor = (float)staticRng!.NextDouble();

                        // 最小値と最大値の間で線形補間（Lerp）
                        float randomStartOpacity = minVal + (maxVal - minVal) * randomFactor;

                        this.startOpacityArray[g] = randomStartOpacity; // ★ 始点不透明度をランダム値で設定

                        // 2. 終点のベース値決定 (FixedTrajectoryのチェック)
                        float baseEnd = this.endopacity; // OpacityEnd のアニメーション値
                        if (this.fixedTrajectory && this.fixedOpacityEndArray != null && this.fixedOpacityEndArray.Length > i)
                        {
                            // 軌道固定がONなら、射出時の Opacity 固定値をベースにする
                            baseEnd = this.fixedOpacityEndArray[i];
                        }

                        // 3. SEToggle の適用 (始点ランダムオフセットを終点に反映)
                        if (this.randomSEOpacityToggle && this.startOpacityArray != null && this.startOpacityArray.Length > g)
                        {
                            // 始点ランダム値とベース始点(this.startopacity)の差分を計算
                            float startOffsetDiff = this.startOpacityArray[g] - this.startopacity;
                            baseEnd += startOffsetDiff;
                        }

                        // 4. 終点のランダムオフセットを適用
                        float endOffset = (float)staticRng.NextDouble() * this.randomEndOpacityRange - (this.randomEndOpacityRange / 2.0f);
                        this.targetOpacityArray[g] = baseEnd + endOffset;
                    }
                    */
                    //Opacity
                    int numberOfGroupsOpacity = (int)Math.Ceiling((double)this.count / Math.Max(1, this.randomOpacityCount));
                    this.startOpacityArray = new float[numberOfGroupsOpacity];
                    this.targetOpacityArray = new float[numberOfGroupsOpacity];

                    for (int g = 0; g < numberOfGroupsOpacity; g++)
                    {
                        // ★★★ ここから修正 ★★★

                        // 1. 最小値・最大値を取得 (0-100スケール)
                        //    ※入力が 1-100 とのことですが、0-100 の間違いとして進めます
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

                        // ★★★ 修正ここまで ★★★
                        // (元の baseEnd や SEToggle や endOffset の計算は不要なので削除)
                    }
                }



                //---random関連終了---


                disposer.RemoveAndDispose(ref commandList);



                var dc = devices.DeviceContext;
                commandList = dc.CreateCommandList();
                disposer.Collect(commandList);
                dc.Target = commandList;
                dc.BeginDraw();
                dc.Clear(null);



                //実際の描画の設計
                void draw(int i)
                {

                    float baseprogress = (count <= 1) ? 0f : (float)i / (count - 1);

                    float startFrameOfItem = (float)i * delayFramesPerItem;

                    if (frame < startFrameOfItem)
                    {
                        return;
                    }

                    //---射出系統---

                    float T_start = i * this.cycleTime;
                    float T_end = T_start + this.travelTime;

                    if (this.frame < T_start || this.frame > T_end)
                    {
                        return;
                    }
                    float progress = (this.frame - T_start) / this.travelTime;

                    if (this.curveToggle && this.curveFactorArray != null && this.curveFactorArray.Length > i)
                    {
                        float curveFactor = this.curveFactorArray[i];

                        progress = progress * curveFactor;

                    }

                    progress = Math.Min(1.0f, Math.Max(0.0f, progress));

                    float PositionX_progress = progress;
                    float PositionY_progress = progress;
                    float PositionZ_progress = progress;

                    float RotationX_progress = progress;
                    float RotationY_progress = progress;
                    float RotationZ_progress = progress;

                    float ScaleX_progress = progress;
                    float ScaleY_progress = progress;
                    float ScaleZ_progress = progress;

                    float Opacity_progress = progress;

                    //---射出系統終了---



                    //---軌道固定系統---
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
                    {
                        current_endx = this.fixedPositionEndXArray[i];
                    }
                    // Y, Z 軸も同様に current_endy, current_endz を決定
                    float current_endy = this.endy;
                    if (this.fixedTrajectory && this.fixedPositionEndYArray != null && this.fixedPositionEndYArray.Length > i)
                    {
                        current_endy = this.fixedPositionEndYArray[i];
                    }
                    float current_endz = this.endz;
                    if (this.fixedTrajectory && this.fixedPositionEndZArray != null && this.fixedPositionEndZArray.Length > i)
                    {
                        current_endz = this.fixedPositionEndZArray[i];
                    }
                    float current_gravityX = this.gravityX;
                    if (this.fixedTrajectory && this.fixedGravityXArray != null && this.fixedGravityXArray.Length > i)
                    {
                        current_gravityX = this.fixedGravityXArray[i];
                    }
                    float current_gravityY = this.gravityY;
                    if (this.fixedTrajectory && this.fixedGravityYArray != null && this.fixedGravityYArray.Length > i)
                    {
                        current_gravityY = this.fixedGravityYArray[i];
                    }
                    float current_gravityZ = this.gravityZ;
                    if (this.fixedTrajectory && this.fixedGravityZArray != null && this.fixedGravityZArray.Length > i)
                    {
                        current_gravityZ = this.fixedGravityZArray[i];
                    }

                    float current_scalex = this.scalex;
                    if (this.fixedTrajectory && this.fixedScaleEndXArray != null && this.fixedScaleEndXArray.Length > i)
                    {
                        current_scalex = this.fixedScaleEndXArray[i];
                    }
                    float current_scaley = this.scaley;
                    if (this.fixedTrajectory && this.fixedScaleEndYArray != null && this.fixedScaleEndYArray.Length > i)
                    {
                        current_scaley = this.fixedScaleEndYArray[i];
                    }
                    float current_scalez = this.scalez;
                    if (this.fixedTrajectory && this.fixedScaleEndZArray != null && this.fixedScaleEndZArray.Length > i)
                    {
                        current_scalez = this.fixedScaleEndZArray[i];
                    }

                    // ★ 回転固定値の決定
                    float current_startRotationX = this.startRotationX;
                    if (this.fixedTrajectory && this.fixedRotationStartXArray != null && this.fixedRotationStartXArray.Length > i)
                    {
                        current_startRotationX = this.fixedRotationStartXArray[i];
                    }
                    float current_startRotationY = this.startRotationY;
                    if (this.fixedTrajectory && this.fixedRotationStartYArray != null && this.fixedRotationStartYArray.Length > i)
                    {
                        current_startRotationY = this.fixedRotationStartYArray[i];
                    }
                    float current_startRotationZ = this.startRotationZ;
                    if (this.fixedTrajectory && this.fixedRotationStartZArray != null && this.fixedRotationStartZArray.Length > i)
                    {
                        current_startRotationZ = this.fixedRotationStartZArray[i];
                    }
                    float current_endRotationX = this.endRotationX;
                    if (this.fixedTrajectory && this.fixedRotationEndXArray != null && this.fixedRotationEndXArray.Length > i)
                    {
                        current_endRotationX = this.fixedRotationEndXArray[i];
                    }
                    float current_endRotationY = this.endRotationY;
                    if (this.fixedTrajectory && this.fixedRotationEndYArray != null && this.fixedRotationEndYArray.Length > i)
                    {
                        current_endRotationY = this.fixedRotationEndYArray[i];
                    }
                    float current_endRotationZ = this.endRotationZ;
                    if (this.fixedTrajectory && this.fixedRotationEndZArray != null && this.fixedRotationEndZArray.Length > i)
                    {
                        current_endRotationZ = this.fixedRotationEndZArray[i];
                    }

                    // ★ 透明度固定値の決定
                    float current_endopacity = this.endopacity;
                    if (this.fixedTrajectory && this.fixedOpacityEndArray != null && this.fixedOpacityEndArray.Length > i)
                    {
                        current_endopacity = this.fixedOpacityEndArray[i];
                    }
                    //---軌道固定系統---

                    //---random関連---
                    //初期値
                    float currentx_base = 0f;
                    float currenty_base = 0f;
                    float currentz_base = 0f;
                    // X座標の計算
                    if (this.randomToggleX && targetXArray != null && startXArray != null && targetXArray.Length > 0 && startXArray.Length > 0)
                    {
                        int safeRandomXCount = Math.Max(1, this.randomXCount);
                        int groupIndexX = i / safeRandomXCount;

                        // 配列の安全なインデックスを取得
                        int targetIndex = Math.Min(groupIndexX, targetXArray.Length - 1);

                        // ランダムな始点と終点を取得
                        float startX_Random = startXArray[targetIndex];
                        float targetX = targetXArray[targetIndex];

                        // 進行度 progress を使用して、ランダムな始点から終点へ線形補間
                        // (イージングを適用したい場合は、ここで progress にイージング関数を適用)
                        currentx_base = startX_Random + (targetX - startX_Random) * progress;
                    }
                    else
                    {
                        // ランダム無効時（以前のロジック）
                        currentx_base = current_startx + (current_endx - current_startx) * PositionX_progress;
                    }

                    // ★Y座標の計算を追加
                    if (this.randomToggleY && targetYArray != null && startYArray != null && targetYArray.Length > 0 && startYArray.Length > 0)
                    {
                        int safeRandomYCount = Math.Max(1, this.randomYCount);
                        int groupIndexY = i / safeRandomYCount;

                        // 配列の安全なインデックスを取得
                        int targetIndex = Math.Min(groupIndexY, targetYArray.Length - 1);

                        // ランダムな始点と終点を取得
                        float startY_Random = startYArray[targetIndex];
                        float targetY = targetYArray[targetIndex];

                        // 進行度 progress を使用して、ランダムな始点から終点へ線形補間
                        // (イージングを適用したい場合は、ここで progress にイージング関数を適用)
                        currenty_base = startY_Random + (targetY - startY_Random) * progress;
                    }
                    else
                    {
                        // ランダム無効時（以前のロジック）
                        currenty_base = current_starty + (current_endy - current_starty) * PositionY_progress;
                    }


                    // ★Z座標の計算を追加
                    if (this.randomToggleZ && targetZArray != null && startZArray != null && targetZArray.Length > 0 && startZArray.Length > 0)
                    {
                        int safeRandomZCount = Math.Max(1, this.randomZCount);
                        int groupIndexZ = i / safeRandomZCount;

                        // 配列の安全なインデックスを取得
                        int targetIndex = Math.Min(groupIndexZ, targetZArray.Length - 1);

                        // ランダムな始点と終点を取得
                        float startZ_Random = startZArray[targetIndex];
                        float targetZ = targetZArray[targetIndex];

                        // 進行度 progress を使用して、ランダムな始点から終点へ線形補間
                        // (イージングを適用したい場合は、ここで progress にイージング関数を適用)
                        currentz_base = startZ_Random + (targetZ - startZ_Random) * progress;
                    }
                    else
                    {
                        // ランダム無効時（以前のロジック）
                        currentz_base = current_startz + (current_endz - current_startz) * PositionZ_progress;
                    }

                    // ★ スケール X軸の計算
                    float finalScalex;
                    if (this.randomScaleToggle && targetScaleXArray != null && startScaleXArray != null && targetScaleXArray.Length > 0 && startScaleXArray.Length > 0)
                    {
                        int safeCount = Math.Max(1, this.randomScaleCount);
                        int groupIndex = i / safeCount;
                        int targetIndex = Math.Min(groupIndex, targetScaleXArray.Length - 1);

                        float startX_Random = startScaleXArray[targetIndex];
                        float targetX = targetScaleXArray[targetIndex];

                        // ランダムな始点から終点へ補間
                        finalScalex = startX_Random + (targetX - startX_Random) * progress;
                    }
                    else
                    {
                        // ランダム無効時 or 配列エラー時
                        float current_scalex_end = this.scalex; // (FixedTrajectoryを適用済み)
                        if (this.fixedTrajectory && this.fixedScaleEndXArray != null && this.fixedScaleEndXArray.Length > i)
                        {
                            current_scalex_end = this.fixedScaleEndXArray[i];
                        }
                        finalScalex = this.scalex + (current_scalex_end - this.scalex) * ScaleX_progress;
                    }
                    //スケールY
                    float finalScaley;
                    if (this.randomScaleToggle && targetScaleYArray != null && startScaleYArray != null && targetScaleYArray.Length > 0 && startScaleYArray.Length > 0)
                    {
                        int safeCount = Math.Max(1, this.randomScaleCount);
                        int groupIndex = i / safeCount;
                        int targetIndex = Math.Min(groupIndex, targetScaleYArray.Length - 1);

                        float startY_Random = startScaleYArray[targetIndex];
                        float targetY = targetScaleYArray[targetIndex];

                        // ランダムな始点から終点へ補間
                        finalScaley = startY_Random + (targetY - startY_Random) * progress;
                    }
                    else
                    {
                        // ランダム無効時 or 配列エラー時
                        float current_scaley_end = this.scaley; // (FixedTrajectoryを適用済み)
                        if (this.fixedTrajectory && this.fixedScaleEndYArray != null && this.fixedScaleEndYArray.Length > i)
                        {
                            current_scaley_end = this.fixedScaleEndYArray[i];
                        }
                        finalScaley = this.scaley + (current_scaley_end - this.scaley) * ScaleY_progress;
                    }
                    //スケールZ
                    float finalScalez;
                    if (this.randomScaleToggle && targetScaleZArray != null && startScaleZArray != null && targetScaleZArray.Length > 0 && startScaleZArray.Length > 0)
                    {
                        int safeCount = Math.Max(1, this.randomScaleCount);
                        int groupIndex = i / safeCount;
                        int targetIndex = Math.Min(groupIndex, targetScaleZArray.Length - 1);

                        float startZ_Random = startScaleZArray[targetIndex];
                        float targetZ = targetScaleZArray[targetIndex];

                        // ランダムな始点から終点へ補間
                        finalScalez = startZ_Random + (targetZ - startZ_Random) * progress;
                    }
                    else
                    {
                        // ランダム無効時 or 配列エラー時
                        float current_scalez_end = this.scalez; // (FixedTrajectoryを適用済み)
                        if (this.fixedTrajectory && this.fixedScaleEndZArray != null && this.fixedScaleEndZArray.Length > i)
                        {
                            current_scalez_end = this.fixedScaleEndZArray[i];
                        }
                        finalScalez = this.scalez + (current_scalez_end - this.scalez) * ScaleZ_progress;
                    }

                    // --- Rotation X軸の計算 ---
                    float finalRotX;
                    if (this.randomRotXToggle && targetRotationXArray != null && startRotationXArray != null && targetRotationXArray.Length > 0 && startRotationXArray.Length > 0)
                    {
                        int safeCount = Math.Max(1, this.randomRotXCount);
                        int groupIndex = i / safeCount;
                        int targetIndex = Math.Min(groupIndex, targetRotationXArray.Length - 1);

                        float startX_Random = startRotationXArray[targetIndex];
                        float targetX = targetRotationXArray[targetIndex];

                        finalRotX = startX_Random + (targetX - startX_Random) * progress;
                    }
                    else
                    {
                        // ランダム無効時 or 配列エラー時
                        current_endRotationX = this.endRotationX; // FixedTrajectory処理後の値を使用
                        if (this.fixedTrajectory && this.fixedRotationEndXArray != null && this.fixedRotationEndXArray.Length > i)
                        {
                            current_endRotationX = this.fixedRotationEndXArray[i];
                        }
                        finalRotX = current_startRotationX + (current_endRotationX - current_startRotationX) * RotationX_progress;
                    }

                    // --- Rotation Y軸の計算 ---
                    float finalRotY;
                    // Y軸のランダム計算ロジック...（X軸とほぼ同じ）
                    if (this.randomRotYToggle && targetRotationYArray != null && startRotationYArray != null && targetRotationYArray.Length > 0 && startRotationYArray.Length > 0)
                    {
                        int safeCount = Math.Max(1, this.randomRotYCount);
                        int groupIndex = i / safeCount;
                        int targetIndex = Math.Min(groupIndex, targetRotationYArray.Length - 1);

                        float startY_Random = startRotationYArray[targetIndex];
                        float targetY = targetRotationYArray[targetIndex];

                        finalRotY = startY_Random + (targetY - startY_Random) * progress;
                    }
                    else
                    {
                        current_endRotationY = this.endRotationY;
                        if (this.fixedTrajectory && this.fixedRotationEndYArray != null && this.fixedRotationEndYArray.Length > i)
                        {
                            current_endRotationY = this.fixedRotationEndYArray[i];
                        }
                        finalRotY = current_startRotationY + (current_endRotationY - current_startRotationY) * RotationY_progress;
                    }

                    // --- Rotation Z軸の計算 ---
                    float finalRotZ;
                    // Z軸のランダム計算ロジック...（X軸とほぼ同じ）
                    if (this.randomRotZToggle && targetRotationZArray != null && startRotationZArray != null && targetRotationZArray.Length > 0 && startRotationZArray.Length > 0)
                    {
                        int safeCount = Math.Max(1, this.randomRotZCount);
                        int groupIndex = i / safeCount;
                        int targetIndex = Math.Min(groupIndex, targetRotationZArray.Length - 1);

                        float startZ_Random = startRotationZArray[targetIndex];
                        float targetZ = targetRotationZArray[targetIndex];

                        finalRotZ = startZ_Random + (targetZ - startZ_Random) * progress;
                    }
                    else
                    {
                        current_endRotationZ = this.endRotationZ;
                        if (this.fixedTrajectory && this.fixedRotationEndZArray != null && this.fixedRotationEndZArray.Length > i)
                        {
                            current_endRotationZ = this.fixedRotationEndZArray[i];
                        }
                        finalRotZ = current_startRotationZ + (current_endRotationZ - current_startRotationZ) * RotationZ_progress;
                    }
                    float finalOpacity;
                    // 判定に this.count > i を含め、配列の長さを確認
                    if (this.randomOpacityToggle && targetOpacityArray != null && startOpacityArray != null && targetOpacityArray.Length > 0 && startOpacityArray.Length > 0 && this.count > i)
                    {
                        // グループ化を考慮したインデックス計算
                        int safeCount = Math.Max(1, this.randomOpacityCount);
                        int groupIndex = i / safeCount;
                        int targetIndex = Math.Min(groupIndex, targetOpacityArray.Length - 1);

                        float start_Random = startOpacityArray[targetIndex];
                        float target = targetOpacityArray[targetIndex];

                        // ランダムな始点から終点へ補間
                        finalOpacity = start_Random + (target - start_Random) * progress;
                    }
                    else
                    {
                        // ランダム無効時 or 配列エラー時
                        current_endopacity = this.endopacity; // FixedTrajectory処理後の値を使用
                        if (this.fixedTrajectory && this.fixedOpacityEndArray != null && this.fixedOpacityEndArray.Length > i)
                        {
                            current_endopacity = this.fixedOpacityEndArray[i];
                        }
                        // 通常のアニメーション補間
                        finalOpacity = this.startopacity + (current_endopacity - this.startopacity) * Opacity_progress;
                    }
                    //---random関連終了---

                    //float currentOpacity = (startopacity / 100.0f) + ((finalOpacity / 100.0f) - (startopacity / 100.0f)) * Opacity_progress;
                    float currentOpacity = finalOpacity / 100.0f;

                    using var opacityEffect = new Opacity(dc);
                    opacityEffect.SetInput(0, input, true);
                    opacityEffect.SetValue((int)OpacityProperties.Opacity, Math.Clamp(currentOpacity, 0.0f, 1.0f));
                    using var opacityOutput = opacityEffect.Output;

                    using var renderEffect = new Transform3D(dc);

                    renderEffect.SetInput(0, opacityOutput, true);

                    //---Gravity---
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

                    float currentScalex = 1.0f + ((finalScalex / 100.0f) - 1.0f) * ScaleX_progress;
                    float currentScaley = 1.0f + ((finalScaley / 100.0f) - 1.0f) * ScaleY_progress;
                    float currentScalez = 1.0f + ((finalScalez / 100.0f) - 1.0f) * ScaleZ_progress;

                    float currentRotX_deg = finalRotX;
                    float currentRotY_deg = finalRotY;
                    float currentRotZ_deg = finalRotZ;

                    float currentRotX_rad = (float)Math.PI * (currentRotX_deg - rotation.X) / 180;
                    float currentRotY_rad = (float)Math.PI * (currentRotY_deg - rotation.Y) / 180;
                    float currentRotZ_rad = (float)Math.PI * (currentRotZ_deg + rotation.Z) / 180;

                    Matrix4x4 cam = effectDescription.DrawDescription.Camera;

                    // カメラの前方ベクトル（ワールド空間での Z 軸方向）
                    Vector3 camForward = new Vector3(cam.M31, cam.M32, cam.M33);
                    if (camForward == Vector3.Zero) camForward = new Vector3(0, 0, 1);
                    camForward = Vector3.Normalize(camForward);

                    // カメラの Y 軸回転（Yaw）を取得（atan2(X, Z)）
                    float cameraYaw = (float)Math.Atan2(camForward.X, camForward.Z); // ラジアン

                    // --- ビルボード処理 ---
                    // billboard が true のとき、オブジェクトの Y 回転をカメラに合わせる（または打ち消す）
                    float appliedRotY = currentRotY_rad; // 現在のオブジェクト回転（ラジアン）
                    if (this.billboard)
                    {
                        // 方法1: オブジェクトを「カメラの正面に向ける」ならオブジェクトの Y 回転を cameraYaw に合わせる
                        // appliedRotY = cameraYaw;

                        // 方法2: オブジェクトのローカル回転を保ちながら「常にカメラへ向ける」なら、カメラYaw で置き換える（多くはこれでOK）
                        appliedRotY = -cameraYaw; // 符号は見た目に合わせて調整（- にすると正面がカメラ方向を向くことが多い）
                    }

                    Vector3 currentScale = new Vector3(currentScalex, currentScaley, currentScalez);




                    renderEffect.TransformMatrix = Matrix4x4.CreateRotationZ(currentRotZ_rad) *
                                                   Matrix4x4.CreateRotationY(appliedRotY) *
                                                   Matrix4x4.CreateRotationX(currentRotX_rad) *
                                                   Matrix4x4.CreateScale(currentScale) *
                                                   Matrix4x4.CreateTranslation(new Vector3(currentx, currenty, currentz)) *
                                                   effectDescription.DrawDescription.Camera *
                                                   new Matrix4x4(1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, -0.001f, 0f, 0f, 0f, 1f);

                    using var renderOutput = renderEffect.Output;

                    dc.DrawImage(renderOutput);

                }

                // 描画順序を決定する変数

                /*
                int drawStart;
                int drawEnd;
                int drawStep; // 1 or -1

                bool shouldReverse = (reverseDraw < 0.5);

                if (fixedDraw)
                {
                    // fixedDraw ON
                    if (shouldReverse)
                    {
                        drawStart = count - 1;
                        drawEnd = -1;
                        drawStep = -1;
                    }
                    else // ReverseDraw OFF
                    {
                        drawStart = 0;
                        drawEnd = count;
                        drawStep = 1;
                    }
                }
                else
                {
                    bool zDefaultOrder = (startz < endz);

                    bool isActualReverse = zDefaultOrder != shouldReverse;

                    if (isActualReverse)
                    {
                        drawStart = count - 1;
                        drawEnd = -1;
                        drawStep = -1;
                    }
                    else
                    {
                        drawStart = 0;
                        drawEnd = count;
                        drawStep = 1;
                    }
                }

                for (int i = drawStart; i != drawEnd; i += drawStep)
                {
                    draw(i);
                }
                */

                //List<ItemDrawData> drawList = new List<ItemDrawData>();

                this._drawList.Clear();
                /*
                // 全アイテムの Z 座標を計算し、リストに追加
                for (int i = 0; i < this.count; i++)
                {
                    bool isActive;
                    // CalculateCurrentZ を呼び出し、現在の Z 座標とアクティブ状態を取得
                    float currentz = CalculateCurrentZ(i, out isActive);

                    if (isActive)
                    {
                        // 描画期間内のアイテムのみリストに追加
                        drawList.Add(new ItemDrawData { Index = i, ZPosition = currentz });
                    }
                }
                */
                for (int i = 0; i < this.count; i++)
                {
                    bool isActive;
                    // CalculateCurrentZ を呼び出し、現在の Z 座標とアクティブ状態を取得
                    float currentz = CalculateCurrentZ(i, out isActive);

                    if (isActive)
                    {
                        // 描画期間内のアイテムのみリストに追加
                        // drawList の代わりに _drawList を使用
                        this._drawList.Add(new ItemDrawData { Index = i, ZPosition = currentz });
                    }
                }
                /*
                drawList.Sort((a, b) => {
                    // 1. ZPosition の比較 (主キー)
                    int zComparison = a.ZPosition.CompareTo(b.ZPosition);

                    if (zComparison != 0)
                    {
                        // Z座標が異なる場合、そのまま Z 座標で順序を決定
                        return zComparison;
                    }
                    else
                    {
                        // Z座標が完全に同じ場合、元のインデックス (Index) を使用して順序を決定 (副キー)
                        // インデックスが小さいものから描画することで、安定した順序を保証
                        return a.Index.CompareTo(b.Index);
                    }
                });
                */
                _drawList.Sort((a, b) => {
                    // 1. ZPosition の比較 (主キー)
                    int zComparison = a.ZPosition.CompareTo(b.ZPosition);

                    if (zComparison != 0)
                    {
                        // Z座標が異なる場合、そのまま Z 座標で順序を決定
                        return zComparison;
                    }
                    else
                    {
                        // Z座標が完全に同じ場合、元のインデックス (Index) を使用して順序を決定 (副キー)
                        // インデックスが小さいものから描画することで、安定した順序を保証
                        return a.Index.CompareTo(b.Index);
                    }
                });

                // カメラ行列 (例: effectDescription.DrawDescription.Camera)
                Matrix4x4 cam = effectDescription.DrawDescription.Camera;

                // C#の多くのライブラリでは、これが第4行の (X, Y, Z) 成分に対応します
                Vector3 camPosition = new Vector3(cam.M41, cam.M42, cam.M43);

                Vector3 itemPosition = new Vector3(this.startx, this.starty, this.startz);
                // アイテムからカメラへ向かうベクトル
                Vector3 viewVector = camPosition - itemPosition;
                viewVector = Vector3.Normalize(viewVector); // 正規化（長さ 1 にする）


                Vector3 camForward = new Vector3(cam.M31, cam.M32, cam.M33);
                camForward = Vector3.Normalize(camForward);

                // カメラの向きが正面(+Z)に対してどれくらい回転しているか
                float angleY = (float)Math.Atan2(camForward.X, camForward.Z);

                /*
                if (!this.fixedDraw)
                {
                    // 180度以上回転していたら描画順を反転
                    if (Math.Abs(angleY) > Math.PI / 2)
                    {
                        drawList.Reverse();
                    }

                    // アイテムの「正面」は通常、ワールド座標の +Z 軸方向と仮定します。
                    // ----------------------------------------------------------------------
                    // dotProduct が正 (+): 視線ベクトルとカメラの Z 軸が同じ方向を向いている
                    // dotProduct が負 (-): 視線ベクトルとカメラの Z 軸が反対方向を向いている (裏側)
                    // ----------------------------------------------------------------------
                }
                
                
                if (this.reverseDraw >= 0.5f) // または this.reverseDraw == true など
                {
                    drawList.Reverse();
                }
                foreach (var data in drawList)
                {
                    // data.Index は、元のアイテムのインデックス i に相当します
                    draw(data.Index);
                }
                */
                if (!this.fixedDraw)
                {
                    // 180度以上回転していたら描画順を反転
                    if (Math.Abs(angleY) > Math.PI / 2)
                    {
                        _drawList.Reverse();
                    }

                    // アイテムの「正面」は通常、ワールド座標の +Z 軸方向と仮定します。
                    // ----------------------------------------------------------------------
                    // dotProduct が正 (+): 視線ベクトルとカメラの Z 軸が同じ方向を向いている
                    // dotProduct が負 (-): 視線ベクトルとカメラの Z 軸が反対方向を向いている (裏側)
                    // ----------------------------------------------------------------------
                }


                if (this.reverseDraw >= 0.5f) // または this.reverseDraw == true など
                {
                    _drawList.Reverse();
                }
                foreach (var data in _drawList)
                {
                    // data.Index は、元のアイテムのインデックス i に相当します
                    draw(data.Index);
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
            public float ZPosition; // ソートキー
        }

        // 特定のアイテムの現在のZ座標を計算する関数
        private float CalculateCurrentZ(int i, out bool isActive)
        {
            isActive = false;

            // 1. 進行度 (progress) の計算（★ T_start, T_end, 期間外チェックを移植 ★）
            float T_start = i * this.cycleTime;
            float T_end = T_start + this.travelTime;

            if (this.frame < T_start || this.frame > T_end)
            {
                return 0f; // 描画範囲外なので 0f (または float.MaxValue) を返す
            }
            isActive = true;

            float progress = (this.frame - T_start) / this.travelTime;

            if (this.curveToggle && this.curveFactorArray != null && this.curveFactorArray.Length > i)
            {
                float curveFactor = this.curveFactorArray[i];

                // ★ progress をランダムな係数で調整
                progress = progress * curveFactor;

            }

            float current_startz = this.startz;
            if (this.fixedTrajectory && this.fixedPositionStartZArray != null && this.fixedPositionStartZArray.Length > i)
                current_startz = this.fixedPositionStartZArray[i];

            float current_endz = this.endz;
            if (this.fixedTrajectory && this.fixedPositionEndZArray != null && this.fixedPositionEndZArray.Length > i)
            {
                current_endz = this.fixedPositionEndZArray[i];
            }

            progress = Math.Min(1.0f, Math.Max(0.0f, progress)); // 念のため 0.0～1.0 にクランプ

            float PositionZ_progress = progress; // 例: X座標の進行度に適用

            // 2. Z座標関連の計算（★ currentz_base と current_gravityZ の決定ロジックを移植 ★）
            // ★Z座標の計算を追加

            float currentz_base = 0f;
            if (this.randomToggleZ && targetZArray != null && startZArray != null && targetZArray.Length > 0 && startZArray.Length > 0)
            {
                int safeRandomZCount = Math.Max(1, this.randomZCount);
                int groupIndexZ = i / safeRandomZCount;

                // 配列の安全なインデックスを取得
                int targetIndex = Math.Min(groupIndexZ, targetZArray.Length - 1);

                // ランダムな始点と終点を取得
                float startZ_Random = startZArray[targetIndex];
                float targetZ = targetZArray[targetIndex];

                // 進行度 progress を使用して、ランダムな始点から終点へ線形補間
                // (イージングを適用したい場合は、ここで progress にイージング関数を適用)
                currentz_base = startZ_Random + (targetZ - startZ_Random) * progress;
            }
            else
            {
                // ランダム無効時（以前のロジック）
                currentz_base = current_startz + (current_endz - current_startz) * PositionZ_progress;
            }

            float current_gravityZ = this.gravityZ;
            if (this.fixedTrajectory && this.fixedGravityZArray != null && this.fixedGravityZArray.Length > i)
            {
                // fixedTrajectory の場合、配列の値で上書きする
                current_gravityZ = this.fixedGravityZArray[i];
            }
            // ★★★★ ここで if を閉じる ★★★★

            // 3. 重力オフセットの計算 (if の外で、必ず実行される)
            float progressSquared = progress * progress;
            if (grTerminationToggle)
            {
                progressSquared = (progress * progress - progress);
            }

            float gravityOffsetZ = current_gravityZ * progressSquared;

            // 4. 最終Z座標の決定 (if の外で、必ず実行される)
            return currentz_base + gravityOffsetZ;
        } // ★ 関数が閉じている

        public void ClearInput()
        {
        }

        public void Dispose()
        {
            // 1. disposerによる解放処理（内部リソースをクリーンアップ）
            disposer.Dispose();

            // 2. ★追加: CommandListを明示的に解放 (disposerで解放されていなければここで解放)
            if (commandList != null && commandList is IDisposable disposableCommandList)
            {
                disposableCommandList.Dispose();
            }
            this.commandList = null;

            this.input = null; // 参照を切る

        }

        public void SetInput(ID2D1Image? input)
        {
            this.input = input;
            IsInputChanged = true;
        }

    }
}
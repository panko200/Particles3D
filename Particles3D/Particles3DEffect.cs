using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Particles3D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace Particles3D
{
    [VideoEffect("3Dパーティクル", ["アニメーション"], ["3Dparticles", "particles", "worldparticles", "パーティカル"])]
    internal class Particles3DEffect : VideoEffectBase
    {

        public override string Label => "3Dパーティクル";

        [Display(GroupName = "パーティクル", Name = "最大個数", Description = "アイテム全体で最大いくつのパーティクルを描画するか")]
        [AnimationSlider("F0", "", 1, 100)]
        public Animation Count { get; } = new Animation(1, 1, 10000);

        [Display(GroupName = "描画", Name = "描画順反転", Description = "描画順反転")]
        [AnimationSlider("F0", "", 0, 1)]
        public Animation ReverseDraw { get; } = new Animation(0, 0, 1);

        [Display(GroupName = "描画", Name = "描画順固定", Description = "描画順固定")]
        [ToggleSlider]
        public bool FixedDraw { get => fixedDraw; set => Set(ref fixedDraw, value); }
        bool fixedDraw = false;

        [Display(GroupName = "描画", Name = "ビルボード化", Description = "ビルボード化")]
        [ToggleSlider]
        public bool BillboardDraw { get => billboardDraw; set => Set(ref billboardDraw, value); }
        bool billboardDraw = false;

        [Display(GroupName = "遅延", Name = "遅延", Description = "遅延")]
        [AnimationSlider("F1", "ﾐﾘ秒", 0, 1000)]
        public Animation DelayTime { get; } = new Animation(0, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "座標", Name = "初期X", Description = "初期X")]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation StartX { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "座標", Name = "初期Y", Description = "初期Y")]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation StartY { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "座標", Name = "初期Z", Description = "初期Z")]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation StartZ { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "座標", Name = "終端X", Description = "終端X")]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation EndX { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "座標", Name = "終端Y", Description = "終端Y")]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation EndY { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "座標", Name = "終端Z", Description = "終端Z")]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation EndZ { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "拡大率", Name = "終端X拡大率", Description = "終端X拡大率")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation ScaleX { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "拡大率", Name = "終端Y拡大率", Description = "終端Y拡大率")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation ScaleY { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "拡大率", Name = "終端Z拡大率", Description = "終端Z拡大率")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation ScaleZ { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "透明度", Name = "初期不透明度", Description = "初期不透明度")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation StartOpacity { get; } = new Animation(100, 0, 100);

        [Display(GroupName = "透明度", Name = "終端不透明度", Description = "終端不透明度")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation EndOpacity { get; } = new Animation(100, 0, 100);

        [Display(GroupName = "角度", Name = "初期X回転", Description = "初期X回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation StartRotationX { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "角度", Name = "初期Y回転", Description = "初期Y回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation StartRotationY { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "角度", Name = "初期Z回転", Description = "初期Z回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation StartRotationZ { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "角度", Name = "終端X回転", Description = "終端X回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation EndRotationX { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "角度", Name = "終端Y回転", Description = "終端Y回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation EndRotationY { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "角度", Name = "終端Z回転", Description = "終端Z回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation EndRotationZ { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "ランダム", Name = "X座標ランダム有効化", Description = "X座標をランダムを有効化します。")]
        [ToggleSlider]
        public bool RandomToggleX { get => randomToggleX; set => Set(ref randomToggleX, value); }
        bool randomToggleX = false;

        [Display(GroupName = "ランダム", Name = "X初期とX終端を同期", Description = "Xの初期値とXの終端値を同期します。初期範囲を動かすことによって終端値もそれにつられます。")]
        [ToggleSlider]
        public bool RandomSEToggleX { get => randomSEToggleX; set => Set(ref randomSEToggleX, value); }
        bool randomSEToggleX = false;

        [Display(GroupName = "ランダム", Name = "周期X座標", Description = "周期X座標")]
        [AnimationSlider("F0", "周期", 1, 10)]
        public Animation RandomXCount { get; } = new Animation(10, 1, 100);

        [Display(GroupName = "ランダム", Name = "初期X座標範囲", Description = "初期X座標範囲")]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation RandomStartXRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "ランダム", Name = "終端X座標範囲", Description = "終端X座標範囲")]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation RandomEndXRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "ランダム", Name = "Y座標ランダム有効化", Description = "Y座標ランダムを有効化します。")]
        [ToggleSlider]
        public bool RandomToggleY { get => randomToggleY; set => Set(ref randomToggleY, value); }
        bool randomToggleY = false;

        [Display(GroupName = "ランダム", Name = "Y初期とY終端を同期", Description = "Yの初期値とYの終端値を同期します。初期範囲を動かすことによって終端値もそれにつられます。")]
        [ToggleSlider]
        public bool RandomSEToggleY { get => randomSEToggleY; set => Set(ref randomSEToggleY, value); }
        bool randomSEToggleY = false;

        [Display(GroupName = "ランダム", Name = "周期Y座標", Description = "周期Y座標")]
        [AnimationSlider("F0", "周期", 1, 10)]
        public Animation RandomYCount { get; } = new Animation(10, 1, 100);

        [Display(GroupName = "ランダム", Name = "初期Y座標範囲", Description = "初期Y座標範囲")]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation RandomStartYRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "ランダム", Name = "終端Y座標範囲", Description = "終端Y座標範囲")]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation RandomEndYRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "ランダム", Name = "Z座標ランダム有効化", Description = "Z座標ランダムを有効化します。")]
        [ToggleSlider]
        public bool RandomToggleZ { get => randomToggleZ; set => Set(ref randomToggleZ, value); }
        bool randomToggleZ = false;

        [Display(GroupName = "ランダム", Name = "Z初期とZ終端を同期", Description = "Zの初期値とZの終端値を同期します。初期範囲を動かすことによって終端値もそれにつられます。")]
        [ToggleSlider]
        public bool RandomSEToggleZ { get => randomSEToggleZ; set => Set(ref randomSEToggleZ, value); }
        bool randomSEToggleZ = false;

        [Display(GroupName = "ランダム", Name = "周期座標Z", Description = "周期Z座標")]
        [AnimationSlider("F0", "周期", 1, 10)]
        public Animation RandomZCount { get; } = new Animation(10, 1, 100);

        [Display(GroupName = "ランダム", Name = "初期Z座標範囲", Description = "初期Z座標範囲")]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation RandomStartZRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "ランダム", Name = "終端Z座標範囲", Description = "終端Z座標範囲")]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation RandomEndZRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "ランダム", Name = "拡大率ランダム有効化", Description = "拡大率ランダムを有効化します。")]
        [ToggleSlider]
        public bool RandomScaleToggle { get => randomScaleToggle; set => Set(ref randomScaleToggle, value); }
        bool randomScaleToggle = false;
        [Display(GroupName = "ランダム", Name = "拡大率初期と終端同期", Description = "拡大率の初期値と拡大率の終端値を同期します。初期範囲を動かすことによって終端値もそれにつられます。")]
        [ToggleSlider]
        public bool RandomSEScaleToggle { get => randomSEScaleToggle; set => Set(ref randomSEScaleToggle, value); }
        bool randomSEScaleToggle = false;
        [Display(GroupName = "ランダム", Name = "周期拡大率", Description = "周期拡大率")]
        [AnimationSlider("F0", "周期", 1, 10)]
        public Animation RandomScaleCount { get; } = new Animation(10, 1, 100);
        [Display(GroupName = "ランダム", Name = "初期拡大率", Description = "初期拡大率")]
        [AnimationSlider("F1", "％", -100, 100)]
        public Animation RandomStartScaleRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "ランダム", Name = "終端拡大率", Description = "終端拡大率")]
        [AnimationSlider("F1", "％", -100, 100)]
        public Animation RandomEndScaleRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "ランダム", Name = "回転ランダム有効化", Description = "回転ランダムを有効化します。")]
        [ToggleSlider]
        public bool RandomRotToggle { get => randomRotToggle; set => Set(ref randomRotToggle, value); }
        bool randomRotToggle = false;
        [Display(GroupName = "ランダム", Name = "回転初期と終端同期", Description = "回転の初期値と回転の終端値を同期します。初期範囲を動かすことによって終端値もそれにつられます。")]
        [ToggleSlider]
        public bool RandomSERotToggle { get => randomSERotToggle; set => Set(ref randomSERotToggle, value); }
        bool randomSERotToggle = false;
        [Display(GroupName = "ランダム", Name = "周期回転", Description = "周期回転")]
        [AnimationSlider("F0", "周期", 1, 10)]
        public Animation RandomRotCount { get; } = new Animation(10, 1, 100);
        [Display(GroupName = "ランダム", Name = "初期回転", Description = "初期回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RandomStartRotRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "ランダム", Name = "終端回転", Description = "終端回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RandomEndRotRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "ランダム", Name = "不透明度ランダム有効化", Description = "不透明度ランダムを有効化します。")]
        [ToggleSlider]
        public bool RandomOpacityToggle { get => randomOpacityToggle; set => Set(ref randomOpacityToggle, value); }
        bool randomOpacityToggle = false;
        [Display(GroupName = "ランダム", Name = "不透明度初期と終端同期", Description = "不透明度の初期値と不透明度の終端値を同期します。初期範囲を動かすことによって終端値もそれにつられます。")]
        [ToggleSlider]
        public bool RandomSEOpacityToggle { get => randomSEOpacityToggle; set => Set(ref randomSEOpacityToggle, value); }
        bool randomSEOpacityToggle = false;
        [Display(GroupName = "ランダム", Name = "周期不透明度", Description = "周期不透明度")]
        [AnimationSlider("F0", "周期", 1, 10)]
        public Animation RandomOpacityCount { get; } = new Animation(10, 1, 100);
        [Display(GroupName = "ランダム", Name = "初期不透明度", Description = "初期不透明度")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation RandomStartOpacityRange { get; } = new Animation(0, 0, 100);
        [Display(GroupName = "ランダム", Name = "終端不透明度", Description = "終端不透明度")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation RandomEndOpacityRange { get; } = new Animation(0, 0, 100);




        [Display(GroupName = "ランダム", Name = "シード", Description = "シード")]
        [AnimationSlider("F0", "", 1, 100)]
        public Animation RandomSeed { get; } = new Animation(1, 1, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "パーティクル", Name = "生成フレーム周期", Description = "次のパーティクルを出すフレーム間隔")]
        [AnimationSlider("F1", "", 0.1, 100)]
        public Animation CycleTime { get; } = new Animation(1, 0.1, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "パーティクル", Name = "個体移動フレーム", Description = "パーティクルが初期値から終端値まで移動するフレーム時間")]
        [AnimationSlider("F1", "", 0.1, 100)]
        public Animation TravelTime { get; } = new Animation(1, 0.1, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "パーティクル", Name = "軌道固定", Description = "終端値のアニメーションを生成時の値に固定します。")]
        [ToggleSlider]
        public bool FixedTrajectory { get => fixedTrajectory; set => Set(ref fixedTrajectory, value); }
        bool fixedTrajectory = false;

        [Display(GroupName = "ランダム", Name = "ブレ幅", Description = "軌道のブレの最大幅")]
        [AnimationSlider("F1", "", 0, 1)]
        public Animation CurveRange { get; } = new Animation(0, 0, 1);
        [Display(GroupName = "ランダム", Name = "ブレ有効化", Description = "ブレ有効化")]
        [ToggleSlider]
        public bool CurveToggle { get => curveToggle; set => Set(ref curveToggle, value); }
        bool curveToggle = false;


        [Display(GroupName = "重力", Name = "重力X", Description = "X 軸方向の重力（加速度）の強さ。負の値は左、正の値は右へ引っ張る")]
        [AnimationSlider("F1", "", -100, 100)]
        public Animation GravityX { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "重力", Name = "重力Y", Description = "Y 軸方向の重力（加速度）の強さ。負の値は上、正の値は下へ引っ張る")]
        [AnimationSlider("F1", "", -100, 100)]
        public Animation GravityY { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "重力", Name = "重力Z", Description = "Z 軸方向の重力（加速度）の強さ。負の値は奥、正の値は手前へ引っ張る")]
        [AnimationSlider("F1", "", -100, 100)]
        public Animation GravityZ { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);



        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new Particles3DEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Count, ReverseDraw, StartX, StartY, StartZ, EndX, EndY, EndZ,
            ScaleX, ScaleY, ScaleZ, EndOpacity, StartOpacity,
            StartRotationX, StartRotationY, StartRotationZ,
            EndRotationX, EndRotationY, EndRotationZ, DelayTime,RandomXCount,RandomStartXRange,RandomEndXRange,RandomSeed,RandomYCount,RandomStartYRange,RandomEndYRange,RandomZCount,RandomStartZRange,RandomEndZRange,
            CycleTime, TravelTime, GravityX, GravityY, GravityZ, CurveRange,RandomScaleCount,RandomStartScaleRange,RandomEndScaleRange,RandomRotCount,RandomStartRotRange,RandomEndRotRange,RandomOpacityCount,RandomStartOpacityRange,RandomEndOpacityRange
            ];
    }
}

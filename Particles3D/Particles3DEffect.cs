using Particles3D;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace Particles3D
{
    [VideoEffect("パーティクル3D", ["アニメーション"], ["3Dparticles", "particles", "worldparticles", "パーティカル"])]
    internal class Particles3DEffect : VideoEffectBase
    {

        public override string Label => "パーティクル3D";

        [Display(GroupName = "プリセット", Name = "プリセット", Description = "カスタムはユーザーが操作できます。カスタム以外はプラグイン製作者が作ったものです。1920x1080前提です。")]
        [EnumComboBox]
        public PresetType PresetGetType
        {
            get => presetGetType;
            set
            {
                if (Set(ref presetGetType, value))
                {
                    ApplyPreset(value);
                }
            }
        }
        public PresetType presetGetType = PresetType.Custom;

        [Display(GroupName = "パーティクル", Name = "最大個数", Description = "アイテム全体で最大いくつのパーティクルを描画するか")]
        [AnimationSlider("F0", "", 1, 100)]
        public Animation Count { get; } = new Animation(1, 1, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "描画", Name = "描画順反転", Description = "描画順反転")]
        [AnimationSlider("F0", "", 0, 1)]
        public Animation ReverseDraw { get; } = new Animation(0, 0, 1);

        [Display(GroupName = "描画", Name = "描画順固定", Description = "描画順固定")]
        [ToggleSlider]
        public bool FixedDraw { get => fixedDraw; set => Set(ref fixedDraw, value); }
        bool fixedDraw = false;
        [Display(GroupName = "描画", Name = "旧Zソートを使用", Description = "ONにすると、従来のワールドZ軸でソートします。")]
        [ToggleSlider]
        public bool ZSortToggle { get => zSortToggle; set => Set(ref zSortToggle, value); }
        private bool zSortToggle = false;

        [Display(GroupName = "描画", Name = "Y軸ビルボード化", Description = "Y軸のみビルボード化します。横から見ても一様に見えるものです。上から見ると様子が変わります。")]
        [ToggleSlider]
        public bool BillboardDraw { get => billboardDraw; set => Set(ref billboardDraw, value); }
        bool billboardDraw = false;
        /*
        [Display(GroupName = "描画", Name = "XY軸ビルボード化", Description = "X、Y軸のみビルボード化します。横、上から見ても一様に見えるものです。")]
        [ToggleSlider]
        public bool BillboardXYDraw { get => billboardXYDraw; set => Set(ref billboardXYDraw, value); }
        bool billboardXYDraw = false;
        */
        [Display(GroupName = "描画", Name = "XYZ軸ビルボード化", Description = "すべての軸でビルボード化します。横、上、傾きから見ても一様に見えるものです。")]
        [ToggleSlider]
        public bool BillboardXYZDraw { get => billboardXYZDraw; set => Set(ref billboardXYZDraw, value); }
        bool billboardXYZDraw = false;
        [Display(GroupName = "描画", Name = "進行方向を向く", Description = "オブジェクトが移動する方向を自動的に向くようになります。")]
        [ToggleSlider]
        public bool AutoOrient { get => autoOrient; set => Set(ref autoOrient, value); }
        private bool autoOrient = false;
        [Display(GroupName = "描画", Name = "進行方向2D化", Description = "2D用になります。「進行方向を向く」が有効になっていないと動きません。")]
        [ToggleSlider]
        public bool AutoOrient2D { get => autoOrient2D; set => Set(ref autoOrient2D, value); }
        private bool autoOrient2D = false;

        [Display(GroupName = "描画", Name = "処理方法", Description = "処理方法を指定します。終端値を指定するか、力を指定するかを選ぶことができます。")]
        [EnumComboBox]
        public MovementCalculationType CalculationType { get => calculationType; set => Set(ref calculationType, value); }
        private MovementCalculationType calculationType = MovementCalculationType.EndPosition;


        [Display(GroupName = "床", Name = "床有効化", Description = "床機能を有効化します。")]
        [ToggleSlider]
        public bool FloorToggle { get => floorToggle; set => Set(ref floorToggle, value); }
        private bool floorToggle = false;
        [Display(GroupName = "床", Name = "床判定", Description = "パーティクルが生成されるのは床の上か、床の下か。")]
        [EnumComboBox]
        public FloorVisibilityType FloorJudgementType { get => floorJudgementType; set => Set(ref floorJudgementType, value); }
        private FloorVisibilityType floorJudgementType = FloorVisibilityType.abovefloor;
        [Display(GroupName = "床", Name = "床動作", Description = "パーティクルは床にたどり着いた際、どのように動くか。")]
        [EnumComboBox]
        public FloorType FloorActionType { get => floorActionType; set => Set(ref floorActionType, value); }
        private FloorType floorActionType = FloorType.Glue;
        [Display(GroupName = "床", Name = "床座標Y", Description = "床座標Y")]
        [AnimationSlider("F1", "", -100, 100)]
        public Animation FloorY { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "床", Name = "消失開始間時間", Description = "床を触ってから、消え始めるまでの時間")]
        [AnimationSlider("F1", "ms", 0, 1000)]
        public Animation FloorWaitTime { get; } = new Animation(0, 0, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "床", Name = "消失開始終了間時間", Description = "消え始める～完全に消えるまでの時間")]
        [AnimationSlider("F1", "ms", 0, 1000)]
        public Animation FloorFadeTime { get; } = new Animation(0, 0, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "床", Name = "エネルギー損失", Description = "反射時に失うエネルギー（速度）。0.0で失わない、1.0ですべて失い停止します。反射か氷原を選択している時のみ使用可能です。")]
        [AnimationSlider("F2", "", 0, 1)]
        public Animation BounceEnergyLoss { get; } = new Animation(0.1f, 0, 1);
        [Display(GroupName = "床", Name = "反発係数", Description = "床に反射する強さ。1.0で衝突前と同じ速度、0.5で半分の速度で跳ね返ります。反射を選択している時のみ使用可能です。")]
        [AnimationSlider("F2", "", 0, 1)]
        public Animation BounceFactor { get; } = new Animation(0.5f, 0, 1);
        [Display(GroupName = "床", Name = "反射時重力", Description = "反射する時の重力のかかり方です。反射を選択している時のみ使用可能です。")]
        [AnimationSlider("F1", "", -500, 500)]
        public Animation BounceGravity { get; } = new Animation(100, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "床", Name = "最大反射回数", Description = "反射の計算の最大数を選択できます。反射を選択している時のみ使用可能です。")]
        [AnimationSlider("F0", "", 1, 10)]
        public Animation BounceCount { get; } = new Animation(10, 1, YMM4Constants.VeryLargeValue);



        [Display(GroupName = "遅延", Name = "遅延", Description = "このプラグインの前身に使われていたパラメータ。なんか削除するの忘れてたから残しとく。なんか挙動おもろいし。")]
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

        [Display(GroupName = "座標", Name = "初期Xと終端Xを同期", Description = "Xの初期値とXの終端値を同期します。初期範囲を動かすことによって終端値もそれにつられます。言わばランダムじゃないバージョン")]
        [ToggleSlider]
        public bool PSEToggleX { get => pSEToggleX; set => Set(ref pSEToggleX, value); }
        bool pSEToggleX = false;
        [Display(GroupName = "座標", Name = "初期Yと終端Yを同期", Description = "Yの初期値とYの終端値を同期します。初期範囲を動かすことによって終端値もそれにつられます。言わばランダムじゃないバージョン")]
        [ToggleSlider]
        public bool PSEToggleY { get => pSEToggleY; set => Set(ref pSEToggleY, value); }
        bool pSEToggleY = false;
        [Display(GroupName = "座標", Name = "初期Zと終端Zを同期", Description = "Zの初期値とZの終端値を同期します。初期範囲を動かすことによって終端値もそれにつられます。言わばランダムじゃないバージョン")]
        [ToggleSlider]
        public bool PSEToggleZ { get => pSEToggleZ; set => Set(ref pSEToggleZ, value); }
        bool pSEToggleZ = false;

        [Display(GroupName = "力", Name = "発射方向X", Description = "発射方向X")]
        [AnimationSlider("F1", "°", -180, 180)]
        public Animation ForcePitch { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "力", Name = "発射方向Y", Description = "発射方向Y")]
        [AnimationSlider("F1", "°", -180, 180)]
        public Animation ForceYaw { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "力", Name = "発射方向Z", Description = "発射方向Z")]
        [AnimationSlider("F1", "°", -180, 180)]
        public Animation ForceRoll { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "力", Name = "発射速度", Description = "発射速度")]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation ForceVelocity { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "力", Name = "ランダム力周期", Description = "ランダム力周期")]
        [AnimationSlider("F0", "周期", 1, 10)]
        public Animation ForceRandomCount { get; } = new Animation(1, 1, 10);
        [Display(GroupName = "力", Name = "ランダム発射方向X", Description = "ランダム発射方向X")]
        [AnimationSlider("F1", "°", -180, 180)]
        public Animation ForceRandomPitch { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "力", Name = "ランダム発射方向Y", Description = "ランダム発射方向Y")]
        [AnimationSlider("F1", "°", -180, 180)]
        public Animation ForceRandomYaw { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "力", Name = "ランダム発射方向Z", Description = "ランダム発射方向Z")]
        [AnimationSlider("F1", "°", -180, 180)]
        public Animation ForceRandomRoll { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "力", Name = "ランダム発射速度", Description = "ランダム発射速度")]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation ForceRandomVelocity { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);



        [Display(GroupName = "拡大率", Name = "初期X拡大率", Description = "初期X拡大率")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation ScaleStartX { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "拡大率", Name = "初期Y拡大率", Description = "初期Y拡大率")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation ScaleStartY { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "拡大率", Name = "初期Z拡大率", Description = "初期拡大率")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation ScaleStartZ { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "拡大率", Name = "終端X拡大率", Description = "終端X拡大率")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation ScaleX { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "拡大率", Name = "終端Y拡大率", Description = "終端Y拡大率")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation ScaleY { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "拡大率", Name = "終端Z拡大率", Description = "終端Z拡大率")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation ScaleZ { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "透明度", Name = "初期不透明度", Description = "初期不透明度、通常時は0 ～ 100％、ランダム有効化した際にのみ、100000％まで設定可能です。")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation StartOpacity { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "透明度", Name = "終端不透明度", Description = "終端不透明度、通常時は0 ～ 100％、ランダム有効化した際にのみ、100000％まで設定可能です。")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation EndOpacity { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "透明度", Name = "不透明度マップ有効化", Description = "ONにすると、不透明度の計算方法が「初期値→中間値→終端値」のカーブ（Opacity Map）に切り替わります。")]
        [ToggleSlider]
        public bool OpacityMapToggle { get => opacityMapToggle; set => Set(ref opacityMapToggle, value); } //
        bool opacityMapToggle = false;

        [Display(GroupName = "透明度", Name = "中間不透明度", Description = "不透明度マップがONの時、パーティクルの生涯の中間(50%)地点での不透明度。")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation OpacityMapMidPoint { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "透明度", Name = "不透明度マップイーズ", Description = "不透明度マップのカーブの強さ。0.0で線形、1.0に近づくほど急激に変化します。")]
        [AnimationSlider("F2", "", 0, 1)]
        public Animation OpacityMapEase { get; } = new Animation(0.7f, 0, 1);

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
        public Animation RandomXCount { get; } = new Animation(1, 1, 100);

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
        public Animation RandomYCount { get; } = new Animation(1, 1, 100);

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
        public Animation RandomZCount { get; } = new Animation(1, 1, 100);

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
        [Display(GroupName = "ランダム", Name = "拡大率X,Y,Z同期", Description = "拡大率のX,Y,Zが一緒に動くようになります。縦長になったりすることがなくなります。")]
        [ToggleSlider]
        public bool RandomSyScaleToggle { get => randomSyScaleToggle; set => Set(ref randomSyScaleToggle, value); }
        bool randomSyScaleToggle = false;
        [Display(GroupName = "ランダム", Name = "周期拡大率", Description = "周期拡大率")]
        [AnimationSlider("F0", "周期", 1, 10)]
        public Animation RandomScaleCount { get; } = new Animation(1, 1, 100);
        [Display(GroupName = "ランダム", Name = "初期拡大率", Description = "初期拡大率")]
        [AnimationSlider("F1", "％", -100, 100)]
        public Animation RandomStartScaleRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "ランダム", Name = "終端拡大率", Description = "終端拡大率")]
        [AnimationSlider("F1", "％", -100, 100)]
        public Animation RandomEndScaleRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "ランダム", Name = "X回転ランダム有効化", Description = "X回転ランダムを有効化します。")]
        [ToggleSlider]
        public bool RandomRotXToggle { get => randomRotXToggle; set => Set(ref randomRotXToggle, value); }
        bool randomRotXToggle = false;
        [Display(GroupName = "ランダム", Name = "X回転初期と終端同期", Description = "X回転の初期値と回転の終端値を同期します。初期範囲を動かすことによって終端値もそれにつられます。")]
        [ToggleSlider]
        public bool RandomSERotXToggle { get => randomSERotXToggle; set => Set(ref randomSERotXToggle, value); }
        bool randomSERotXToggle = false;
        [Display(GroupName = "ランダム", Name = "周期X回転", Description = "周期X回転")]
        [AnimationSlider("F0", "周期", 1, 10)]
        public Animation RandomRotXCount { get; } = new Animation(1, 1, 100);
        [Display(GroupName = "ランダム", Name = "初期X回転", Description = "初期X回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RandomStartRotXRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "ランダム", Name = "終端X回転", Description = "終端X回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RandomEndRotXRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "ランダム", Name = "Y回転ランダム有効化", Description = "Y回転ランダムを有効化します。")]
        [ToggleSlider]
        public bool RandomRotYToggle { get => randomRotYToggle; set => Set(ref randomRotYToggle, value); }
        bool randomRotYToggle = false;
        [Display(GroupName = "ランダム", Name = "Y回転初期と終端同期", Description = "Y回転の初期値と回転の終端値を同期します。初期範囲を動かすことによって終端値もそれにつられます。")]
        [ToggleSlider]
        public bool RandomSERotYToggle { get => randomSERotYToggle; set => Set(ref randomSERotYToggle, value); }
        bool randomSERotYToggle = false;
        [Display(GroupName = "ランダム", Name = "周期Y回転", Description = "周期Y回転")]
        [AnimationSlider("F0", "周期", 1, 10)]
        public Animation RandomRotYCount { get; } = new Animation(1, 1, 100);
        [Display(GroupName = "ランダム", Name = "初期Y回転", Description = "初期Y回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RandomStartRotYRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "ランダム", Name = "終端Y回転", Description = "終端Y回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RandomEndRotYRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "ランダム", Name = "Z回転ランダム有効化", Description = "Z回転ランダムを有効化します。")]
        [ToggleSlider]
        public bool RandomRotZToggle { get => randomRotZToggle; set => Set(ref randomRotZToggle, value); }
        bool randomRotZToggle = false;
        [Display(GroupName = "ランダム", Name = "Z回転初期と終端同期", Description = "Z回転の初期値と回転の終端値を同期します。初期範囲を動かすことによって終端値もそれにつられます。")]
        [ToggleSlider]
        public bool RandomSERotZToggle { get => randomSERotZToggle; set => Set(ref randomSERotZToggle, value); }
        bool randomSERotZToggle = false;
        [Display(GroupName = "ランダム", Name = "周期Z回転", Description = "周期Z回転")]
        [AnimationSlider("F0", "周期", 1, 10)]
        public Animation RandomRotZCount { get; } = new Animation(1, 1, 100);
        [Display(GroupName = "ランダム", Name = "初期Z回転", Description = "初期Z回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RandomStartRotZRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "ランダム", Name = "終端Z回転", Description = "終端Z回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RandomEndRotZRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

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
        public Animation RandomOpacityCount { get; } = new Animation(1, 1, 100);
        [Display(GroupName = "ランダム", Name = "初期不透明度", Description = "初期不透明度")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation RandomStartOpacityRange { get; } = new Animation(0, 0, 100);
        [Display(GroupName = "ランダム", Name = "終端不透明度", Description = "終端不透明度")]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation RandomEndOpacityRange { get; } = new Animation(100, 0, 100);


        [Display(GroupName = "ランダム", Name = "シード", Description = "シード")]
        [AnimationSlider("F0", "", 1, 100)]
        public Animation RandomSeed { get; } = new Animation(1, 1, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "パーティクル", Name = "生成フレーム周期", Description = "次のパーティクルを出すフレーム間隔")]
        [AnimationSlider("F2", "f", 0.01, 100)]
        public Animation CycleTime { get; } = new Animation(1, 0.01, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "パーティクル", Name = "個体移動フレーム", Description = "パーティクルが初期値から終端値まで移動するフレーム時間")]
        [AnimationSlider("F1", "f", 0.1, 100)]
        public Animation TravelTime { get; } = new Animation(1, 0.1, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "パーティクル", Name = "軌道固定", Description = "終端値のアニメーションを生成時の値に固定します。")]
        [ToggleSlider]
        public bool FixedTrajectory { get => fixedTrajectory; set => Set(ref fixedTrajectory, value); }
        bool fixedTrajectory = false;

        [Display(GroupName = "パーティクル", Name = "パーティクルをループ", Description = "ONにすると、その画像アイテムの最初から最後までずっとループします。")]
        [ToggleSlider]
        public bool LoopToggle { get => loopToggle; set => Set(ref loopToggle, value); }
        bool loopToggle = false;

        [Display(GroupName = "ランダム", Name = "ブレ幅", Description = "軌道のブレの最大幅")]
        [AnimationSlider("F1", "", 0, 1)]
        public Animation CurveRange { get; } = new Animation(0, 0, 1);
        [Display(GroupName = "ランダム", Name = "ブレ有効化", Description = "ブレ有効化")]
        [ToggleSlider]
        public bool CurveToggle { get => curveToggle; set => Set(ref curveToggle, value); }
        bool curveToggle = false;

        [Display(GroupName = "抵抗", Name = "抵抗", Description = "パーティクルの速度を経過時間で減衰させます。0.0で抵抗なし、1.0に近づくほど強い抵抗がかかります。")]
        [AnimationSlider("F2", "", 0, 1)]
        public Animation AirResistance { get; } = new Animation(0, 0, 1);


        [Display(GroupName = "重力", Name = "重力X", Description = "X 軸方向の重力（加速度）の強さ。負の値は左、正の値は右へ引っ張る")]
        [AnimationSlider("F1", "", -100, 100)]
        public Animation GravityX { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "重力", Name = "重力Y", Description = "Y 軸方向の重力（加速度）の強さ。負の値は下、正の値は上へ引っ張る")]
        [AnimationSlider("F1", "", -100, 100)]
        public Animation GravityY { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "重力", Name = "重力Z", Description = "Z 軸方向の重力（加速度）の強さ。負の値は奥、正の値は手前へ引っ張る")]
        [AnimationSlider("F1", "", -100, 100)]
        public Animation GravityZ { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "重力", Name = "終端値に到着", Description = "重力の影響下でも絶対に終端値に到着するようになります。")]
        [ToggleSlider]
        public bool GrTerminationToggle { get => grTerminationToggle; set => Set(ref grTerminationToggle, value); }
        bool grTerminationToggle = true;


        [Display(GroupName = "色", Name = "初期色", Description = "生成時の色を指定します。")]
        [ColorPicker]
        public Color StartColor { get => startColor; set => Set(ref startColor, value); }
        private Color startColor = Colors.White;
        [Display(GroupName = "色", Name = "終端色", Description = "消滅時の色を指定します。")]
        [ColorPicker]
        public Color EndColor { get => endColor; set => Set(ref endColor, value); }
        private Color endColor = Colors.White;

        [Display(GroupName = "ランダム色", Name = "色ランダム有効化", Description = "色ランダムを有効化します。非常に高負荷です。")]
        [ToggleSlider]
        public bool RandomColorToggle { get => randomColorToggle; set => Set(ref randomColorToggle, value); }
        bool randomColorToggle = false;
        [Display(GroupName = "ランダム色", Name = "周期色", Description = "周期色")]
        [AnimationSlider("F0", "周期", 1, 10)]
        public Animation RandomColorCount { get; } = new Animation(1, 1, 100);
        [Display(GroupName = "ランダム色", Name = "ランダム色相(H)", Description = "色相(H)をランダムにずらす範囲。0-360。")]
        [AnimationSlider("F1", "", 0, 360)]
        public Animation RandomHueRange { get; } = new Animation(0, 0, 360);

        [Display(GroupName = "ランダム色", Name = "ランダム彩度(S)", Description = "彩度(S)をランダムにずらす範囲。-1000～1000%。")]
        [AnimationSlider("F1", "％", -100, 100)]
        public Animation RandomSatRange { get; } = new Animation(0, -1000, 1000);

        [Display(GroupName = "ランダム色", Name = "ランダム輝度(L)", Description = "輝度(L)をランダムにずらす範囲。-1000～1000%。")]
        [AnimationSlider("F1", "％", -100, 100)]
        public Animation RandomLumRange { get; } = new Animation(0, -1000, 1000);

        [Display(GroupName = "ピント", Name = "ピント機能有効化", Description = "ピント機能を有効にします。")]
        [ToggleSlider]
        public bool FocusToggle { get => focusToggle; set => Set(ref focusToggle, value); }
        bool focusToggle = false;
        [Display(GroupName = "ピント", Name = "ピントの不透明度", Description = "ピントが外れたら透明にするか。")]
        [ToggleSlider]
        public bool FocusFadeToggle { get => focusFadeToggle; set => Set(ref focusFadeToggle, value); }
        bool focusFadeToggle = false;
        [Display(GroupName = "ピント", Name = "深度", Description = "ピントがあうZ距離")]
        [AnimationSlider("F1", "", -1000, 1000)]
        public Animation FocusDepth { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "ピント", Name = "範囲", Description = "ピントが合っているとみなす範囲")]
        [AnimationSlider("F1", "", -100, 100)]
        public Animation FocusRange { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "ピント", Name = "ぼかしの最大値", Description = "ピントが外れた時のぼかしの最大量")]
        [AnimationSlider("F1", "", 0, 50)]
        public Animation FocusMaxBlur { get; } = new Animation(0, 0, 500);
        [Display(GroupName = "ピント", Name = "ぼかしの減衰距離", Description = "ピントが外れた時のぼかしの減衰距離")]
        [AnimationSlider("F1", "", 0, 1000)]
        public Animation FocusFallOffBlur { get; } = new Animation(500, 0, YMM4Constants.VeryLargeValue);
        [Display(GroupName = "ピント", Name = "不透明度の最小値", Description = "ピントが外れた時の不透明度の最小値")]
        [AnimationSlider("F2", "", 0, 1)]
        public Animation FocusFadeMinOpacity { get; } = new Animation(0.5f, 0, 1);
        [Display(GroupName = "残像", Name = "残像有効化", Description = "残像機能を有効化します。激重処理なので気をつけてください。")]
        [ToggleSlider]
        public bool TrailToggle { get => trailToggle; set => Set(ref trailToggle, value); }
        bool trailToggle = false;
        [Display(GroupName = "残像", Name = "残像の数", Description = "残像の数を指定できます。")]
        [AnimationSlider("F0", "個", 1, 100)]
        public Animation TrailCount { get; } = new Animation(10, 1, 100);
        [Display(GroupName = "残像", Name = "残像の間隔", Description = "残像の間隔を全体の進み具合で指定できます。(0.001 ~ 1)")]
        [AnimationSlider("F4", "", 0.001f, 1)]
        public Animation TrailInterval { get; } = new Animation(0.005f, 0.0001f, 1);
        [Display(GroupName = "残像", Name = "残像の減衰率", Description = "残像の不透明度がどれだけ上がっていくかを指定できます。")]
        [AnimationSlider("F2", "", 0, 1)]
        public Animation TrailFade { get; } = new Animation(0.5f, 0, 1);
        [Display(GroupName = "残像", Name = "残像の拡大率", Description = "残像が消えて行く際の拡大率を指定できます。")]
        [AnimationSlider("F1", "％", 0, 200)]
        public Animation TrailScale { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);



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
            CycleTime, TravelTime, GravityX, GravityY, GravityZ, CurveRange,RandomScaleCount,RandomStartScaleRange,RandomEndScaleRange,
            RandomRotXCount,RandomStartRotXRange,RandomEndRotXRange,RandomRotYCount,RandomStartRotYRange,RandomEndRotYRange,RandomRotZCount,RandomStartRotZRange,RandomEndRotZRange,
            RandomOpacityCount,RandomStartOpacityRange,RandomEndOpacityRange,ScaleStartX,ScaleStartY,ScaleStartZ,RandomHueRange,RandomSatRange, RandomLumRange,RandomColorCount,
            ForcePitch, ForceYaw, ForceVelocity,ForceRoll, ForceRandomCount, ForceRandomPitch, ForceRandomYaw, ForceRandomVelocity, ForceRandomRoll, FloorY, FloorWaitTime, FloorFadeTime,
            FocusDepth, FocusRange, FocusMaxBlur, FocusFadeMinOpacity, FocusFallOffBlur, AirResistance, BounceFactor, BounceEnergyLoss, BounceGravity, OpacityMapMidPoint, OpacityMapEase, BounceCount,
            TrailCount, TrailInterval, TrailFade, TrailScale
            ];

        public enum MovementCalculationType
        {
            [Display(Name = "最終地点指定")]
            EndPosition,
            [Display(Name = "力指定")]
            Force,
        }

        public enum FloorType
        {
            [Display(Name = "接着")]
            Glue,
            [Display(Name = "氷原")]
            Ice,
            [Display(Name = "反射")]
            Bounce
        }
        public enum FloorVisibilityType
        {
            [Display(Name = "床上")]
            abovefloor,
            [Display(Name = "床下")]
            afterfloor
        }

        public enum PresetType
        {
            [Display(Name = "カスタム")]
            Custom,
            [Display(Name = "デフォルト値")]
            Default,
            [Display(Name = "雪")]
            Snow,
            [Display(Name = "雪(床)")]
            SnowFloor,
            [Display(Name = "きらめく星")]
            Sparkle,
            [Display(Name = "雨")]
            Rain,
            [Display(Name = "雨(床)")]
            RainFloor,
            [Display(Name = "適当な炎")]
            CarelessFlame,
            [Display(Name = "火の粉")]
            sparks,
            [Display(Name = "ふわふわ")]
            Fluffy,
            [Display(Name = "紙吹雪")]
            Confetti,
            [Display(Name = "湯気")]
            Steam,
            [Display(Name = "某映像制作ソフトのデフォルトみたいなの")]
            AEDefault

        }
        public void ApplyPreset(PresetType type)
        {
            //ApplyPresetValue(変数名, 値)
            //SetRandomMovePreset(変数名, 初期値, 終端値, スパン)
            //例 : SetRandomMovePreset(GravityX, -100, 100, 0);
            switch (type)
            {
                case PresetType.Snow:

                    ApplyPresetValue(Count, 1600); // パーティクル：最大個数
                    ApplyPresetValue(CycleTime, 1.00f); // パーティクル：生成フレーム周期
                    ApplyPresetValue(TravelTime, 400f); // パーティクル：個体移動フレーム
                    FixedTrajectory = true; // パーティクル：軌道固定
                    LoopToggle = true; // パーティクル：パーティクルをループ

                    ApplyPresetValue(ReverseDraw, 0); // 描画：描画順反転
                    FixedDraw = false; // 描画：描画順固定
                    ZSortToggle = false; // 描画：旧Zソートを使用
                    BillboardDraw = false; // 描画：Y軸ビルボード化
                    BillboardXYZDraw = true; // 描画：XYZ軸ビルボード化
                    AutoOrient = false; // 描画：進行方向を向く
                    AutoOrient2D = false; // 描画：進行方向2D化
                    CalculationType = MovementCalculationType.EndPosition; // 描画：処理方法（Enum）

                    FloorToggle = false; // 床：床有効化
                    FloorJudgementType = FloorVisibilityType.abovefloor; // 床：床判定（Enum）
                    FloorActionType = FloorType.Glue; // 床：床動作（Enum）
                    ApplyPresetValue(FloorY, 0f); // 床：床座標Y
                    ApplyPresetValue(FloorWaitTime, 0f); // 床：消失開始間時間
                    ApplyPresetValue(FloorFadeTime, 0f); // 床：消失開始終了間時間
                    ApplyPresetValue(BounceEnergyLoss, 0.1f); // 床：エネルギー損失
                    ApplyPresetValue(BounceFactor, 0.5f); // 床：反発係数
                    ApplyPresetValue(BounceGravity, 100f); // 床：反射時重力
                    ApplyPresetValue(BounceCount, 10); // 床：最大反射回数

                    ApplyPresetValue(DelayTime, 0f); // 遅延：遅延

                    ApplyPresetValue(StartX, 0f); // 座標：初期X
                    ApplyPresetValue(StartY, -1600f); // 座標：初期Y
                    ApplyPresetValue(StartZ, 0f); // 座標：初期Z
                    ApplyPresetValue(EndX, 0f); // 座標：終端X
                    ApplyPresetValue(EndY, 1600f); // 座標：終端Y
                    ApplyPresetValue(EndZ, 0f); // 座標：終端Z
                    PSEToggleX = false; // 座標：初期Xと終端Xを同期
                    PSEToggleY = false; // 座標：初期Yと終端Yを同期
                    PSEToggleZ = false; // 座標：初期Zと終端Zを同期

                    ApplyPresetValue(ForcePitch, 0f); // 力：発射方向X
                    ApplyPresetValue(ForceYaw, 0f); // 力：発射方向Y
                    ApplyPresetValue(ForceRoll, 0f); // 力：発射方向Z
                    ApplyPresetValue(ForceVelocity, 0f); // 力：発射速度
                    ApplyPresetValue(ForceRandomCount, 1); // 力：ランダム力周期
                    ApplyPresetValue(ForceRandomPitch, 0f); // 力：ランダム発射方向X
                    ApplyPresetValue(ForceRandomYaw, 0f); // 力：ランダム発射方向Y
                    ApplyPresetValue(ForceRandomRoll, 0f); // 力：ランダム発射方向Z
                    ApplyPresetValue(ForceRandomVelocity, 0f); // 力：ランダム発射速度

                    ApplyPresetValue(ScaleStartX, 100f); // 拡大率：初期X拡大率
                    ApplyPresetValue(ScaleStartY, 100f); // 拡大率：初期Y拡大率
                    ApplyPresetValue(ScaleStartZ, 100f); // 拡大率：初期Z拡大率
                    ApplyPresetValue(ScaleX, 100f); // 拡大率：終端X拡大率
                    ApplyPresetValue(ScaleY, 100f); // 拡大率：終端Y拡大率
                    ApplyPresetValue(ScaleZ, 100f); // 拡大率：終端Z拡大率

                    ApplyPresetValue(StartOpacity, 100f); // 透明度：初期不透明度
                    ApplyPresetValue(EndOpacity, 100f); // 透明度：終端不透明度
                    OpacityMapToggle = false; // 透明度：不透明度マップ有効化
                    ApplyPresetValue(OpacityMapMidPoint, 100f); // 透明度：中間不透明度
                    ApplyPresetValue(OpacityMapEase, 0.7f); // 透明度：不透明度マップイーズ

                    ApplyPresetValue(StartRotationX, 0f); // 角度：初期X回転
                    ApplyPresetValue(StartRotationY, 0f); // 角度：初期Y回転
                    ApplyPresetValue(StartRotationZ, 0f); // 角度：初期Z回転
                    ApplyPresetValue(EndRotationX, 0f); // 角度：終端X回転
                    ApplyPresetValue(EndRotationY, 0f); // 角度：終端Y回転
                    ApplyPresetValue(EndRotationZ, 0f); // 角度：終端Z回転

                    RandomToggleX = true; // ランダム：X座標ランダム有効化
                    RandomSEToggleX = true; // ランダム：X初期とX終端を同期
                    ApplyPresetValue(RandomXCount, 1); // ランダム：周期X座標
                    ApplyPresetValue(RandomStartXRange, 3200f); // ランダム：初期X座標範囲
                    ApplyPresetValue(RandomEndXRange, 400f); // ランダム：終端X座標範囲

                    RandomToggleY = false; // ランダム：Y座標ランダム有効化
                    RandomSEToggleY = false; // ランダム：Y初期とY終端を同期
                    ApplyPresetValue(RandomYCount, 1); // ランダム：周期Y座標
                    ApplyPresetValue(RandomStartYRange, 0f); // ランダム：初期Y座標範囲
                    ApplyPresetValue(RandomEndYRange, 0f); // ランダム：終端Y座標範囲

                    RandomToggleZ = true; // ランダム：Z座標ランダム有効化
                    RandomSEToggleZ = true; // ランダム：Z初期とZ終端を同期
                    ApplyPresetValue(RandomZCount, 1); // ランダム：周期座標Z
                    ApplyPresetValue(RandomStartZRange, 3200f); // ランダム：初期Z座標範囲
                    ApplyPresetValue(RandomEndZRange, 400f); // ランダム：終端Z座標範囲

                    RandomScaleToggle = false; // ランダム：拡大率ランダム有効化
                    RandomSEScaleToggle = false; // ランダム：拡大率初期と終端同期
                    RandomSyScaleToggle = false; // ランダム：拡大率X,Y,Z同期
                    ApplyPresetValue(RandomScaleCount, 1); // ランダム：周期拡大率
                    ApplyPresetValue(RandomStartScaleRange, 0f); // ランダム：初期拡大率
                    ApplyPresetValue(RandomEndScaleRange, 0f); // ランダム：終端拡大率

                    RandomRotXToggle = false; // ランダム：X回転ランダム有効化
                    RandomSERotXToggle = false; // ランダム：X回転初期と終端同期
                    ApplyPresetValue(RandomRotXCount, 1); // ランダム：周期X回転
                    ApplyPresetValue(RandomStartRotXRange, 0f); // ランダム：初期X回転
                    ApplyPresetValue(RandomEndRotXRange, 0f); // ランダム：終端X回転

                    RandomRotYToggle = false; // ランダム：Y回転ランダム有効化
                    RandomSERotYToggle = false; // ランダム：Y回転初期と終端同期
                    ApplyPresetValue(RandomRotYCount, 1); // ランダム：周期Y回転
                    ApplyPresetValue(RandomStartRotYRange, 0f); // ランダム：初期Y回転
                    ApplyPresetValue(RandomEndRotYRange, 0f); // ランダム：終端Y回転

                    RandomRotZToggle = false; // ランダム：Z回転ランダム有効化
                    RandomSERotZToggle = false; // ランダム：Z回転初期と終端同期
                    ApplyPresetValue(RandomRotZCount, 1); // ランダム：周期Z回転
                    ApplyPresetValue(RandomStartRotZRange, 0f); // ランダム：初期Z回転
                    ApplyPresetValue(RandomEndRotZRange, 0f); // ランダム：終端Z回転

                    RandomOpacityToggle = true; // ランダム：不透明度ランダム有効化
                    RandomSEOpacityToggle = false; // ランダム：不透明度初期と終端同期
                    ApplyPresetValue(RandomOpacityCount, 1); // ランダム：周期不透明度
                    ApplyPresetValue(RandomStartOpacityRange, 50f); // ランダム：初期不透明度
                    ApplyPresetValue(RandomEndOpacityRange, 100f); // ランダム：終端不透明度

                    ApplyPresetValue(RandomSeed, 1); // ランダム：シード
                    ApplyPresetValue(CurveRange, 0f); // ランダム：ブレ幅
                    CurveToggle = false; // ランダム：ブレ有効化

                    ApplyPresetValue(AirResistance, 0f); // 抵抗：抵抗

                    SetRandomMovePreset(GravityX, -1600, 1600, 0); // 重力：重力X
                    SetRandomMovePreset(GravityY, -800, 800, 0); // 重力：重力Y
                    SetRandomMovePreset(GravityZ, -1600, 1600, 0); // 重力：重力Z
                    GrTerminationToggle = true; // 重力：終端値に到着

                    StartColor = Colors.White; // 色：初期色
                    EndColor = Colors.White; // 色：終端色

                    RandomColorToggle = false; // ランダム色：色ランダム有効化
                    ApplyPresetValue(RandomColorCount, 1); // ランダム色：周期色
                    ApplyPresetValue(RandomHueRange, 0f); // ランダム色：ランダム色相(H)
                    ApplyPresetValue(RandomSatRange, 0f); // ランダム色：ランダム彩度(S)
                    ApplyPresetValue(RandomLumRange, 0f); // ランダム色：ランダム輝度(L)

                    FocusToggle = true; // ピント：ピント機能有効化
                    FocusFadeToggle = true; // ピント：ピントの不透明度
                    ApplyPresetValue(FocusDepth, -300f); // ピント：深度
                    ApplyPresetValue(FocusRange, 500f); // ピント：範囲
                    ApplyPresetValue(FocusMaxBlur, 11.8f); // ピント：ぼかしの最大値
                    ApplyPresetValue(FocusFallOffBlur, 1000f); // ピント：ぼかしの減衰距離
                    ApplyPresetValue(FocusFadeMinOpacity, 0.25f); // ピント：不透明度の最小値

                    TrailToggle = false; // 残像 : 残像有効化
                    ApplyPresetValue(TrailCount, 10); // 残像 : 残像の数
                    ApplyPresetValue(TrailInterval, 0.005f); // 残像 : 残像の間隔
                    ApplyPresetValue(TrailFade, 0.5f); // 残像 : 残像の減衰率
                    ApplyPresetValue(TrailScale, 100f); // 残像 : 残像の拡大率
                    break;

                case PresetType.SnowFloor:
                    ApplyPresetValue(Count, 1600); // パーティクル：最大個数
                    ApplyPresetValue(CycleTime, 1.00f); // パーティクル：生成フレーム周期
                    ApplyPresetValue(TravelTime, 400f); // パーティクル：個体移動フレーム
                    FixedTrajectory = true; // パーティクル：軌道固定
                    LoopToggle = true; // パーティクル：パーティクルをループ

                    ApplyPresetValue(ReverseDraw, 0); // 描画：描画順反転
                    FixedDraw = false; // 描画：描画順固定
                    ZSortToggle = false; // 描画：旧Zソートを使用
                    BillboardDraw = false; // 描画：Y軸ビルボード化
                    BillboardXYZDraw = true; // 描画：XYZ軸ビルボード化
                    AutoOrient = false; // 描画：進行方向を向く
                    AutoOrient2D = false; // 描画：進行方向2D化
                    CalculationType = MovementCalculationType.EndPosition; // 描画：処理方法（Enum）

                    FloorToggle = true; // 床：床有効化
                    FloorJudgementType = FloorVisibilityType.abovefloor; // 床：床判定（Enum）
                    FloorActionType = FloorType.Glue; // 床：床動作（Enum）
                    ApplyPresetValue(FloorY, 400f); // 床：床座標Y
                    ApplyPresetValue(FloorWaitTime, 400f); // 床：消失開始間時間
                    ApplyPresetValue(FloorFadeTime, 800f); // 床：消失開始終了間時間
                    ApplyPresetValue(BounceEnergyLoss, 0.1f); // 床：エネルギー損失
                    ApplyPresetValue(BounceFactor, 0.5f); // 床：反発係数
                    ApplyPresetValue(BounceGravity, 100f); // 床：反射時重力
                    ApplyPresetValue(BounceCount, 10); // 床：最大反射回数

                    ApplyPresetValue(DelayTime, 0f); // 遅延：遅延

                    ApplyPresetValue(StartX, 0f); // 座標：初期X
                    ApplyPresetValue(StartY, -1600f); // 座標：初期Y
                    ApplyPresetValue(StartZ, 0f); // 座標：初期Z
                    ApplyPresetValue(EndX, 0f); // 座標：終端X
                    ApplyPresetValue(EndY, 1600f); // 座標：終端Y
                    ApplyPresetValue(EndZ, 0f); // 座標：終端Z
                    PSEToggleX = false; // 座標：初期Xと終端Xを同期
                    PSEToggleY = false; // 座標：初期Yと終端Yを同期
                    PSEToggleZ = false; // 座標：初期Zと終端Zを同期

                    ApplyPresetValue(ForcePitch, 0f); // 力：発射方向X
                    ApplyPresetValue(ForceYaw, 0f); // 力：発射方向Y
                    ApplyPresetValue(ForceRoll, 0f); // 力：発射方向Z
                    ApplyPresetValue(ForceVelocity, 0f); // 力：発射速度
                    ApplyPresetValue(ForceRandomCount, 1); // 力：ランダム力周期
                    ApplyPresetValue(ForceRandomPitch, 0f); // 力：ランダム発射方向X
                    ApplyPresetValue(ForceRandomYaw, 0f); // 力：ランダム発射方向Y
                    ApplyPresetValue(ForceRandomRoll, 0f); // 力：ランダム発射方向Z
                    ApplyPresetValue(ForceRandomVelocity, 0f); // 力：ランダム発射速度

                    ApplyPresetValue(ScaleStartX, 100f); // 拡大率：初期X拡大率
                    ApplyPresetValue(ScaleStartY, 100f); // 拡大率：初期Y拡大率
                    ApplyPresetValue(ScaleStartZ, 100f); // 拡大率：初期Z拡大率
                    ApplyPresetValue(ScaleX, 100f); // 拡大率：終端X拡大率
                    ApplyPresetValue(ScaleY, 100f); // 拡大率：終端Y拡大率
                    ApplyPresetValue(ScaleZ, 100f); // 拡大率：終端Z拡大率

                    ApplyPresetValue(StartOpacity, 100f); // 透明度：初期不透明度
                    ApplyPresetValue(EndOpacity, 100f); // 透明度：終端不透明度
                    OpacityMapToggle = false; // 透明度：不透明度マップ有効化
                    ApplyPresetValue(OpacityMapMidPoint, 100f); // 透明度：中間不透明度
                    ApplyPresetValue(OpacityMapEase, 0.7f); // 透明度：不透明度マップイーズ

                    ApplyPresetValue(StartRotationX, 0f); // 角度：初期X回転
                    ApplyPresetValue(StartRotationY, 0f); // 角度：初期Y回転
                    ApplyPresetValue(StartRotationZ, 0f); // 角度：初期Z回転
                    ApplyPresetValue(EndRotationX, 0f); // 角度：終端X回転
                    ApplyPresetValue(EndRotationY, 0f); // 角度：終端Y回転
                    ApplyPresetValue(EndRotationZ, 0f); // 角度：終端Z回転

                    RandomToggleX = true; // ランダム：X座標ランダム有効化
                    RandomSEToggleX = true; // ランダム：X初期とX終端を同期
                    ApplyPresetValue(RandomXCount, 1); // ランダム：周期X座標
                    ApplyPresetValue(RandomStartXRange, 3200f); // ランダム：初期X座標範囲
                    ApplyPresetValue(RandomEndXRange, 400f); // ランダム：終端X座標範囲

                    RandomToggleY = false; // ランダム：Y座標ランダム有効化
                    RandomSEToggleY = false; // ランダム：Y初期とY終端を同期
                    ApplyPresetValue(RandomYCount, 1); // ランダム：周期Y座標
                    ApplyPresetValue(RandomStartYRange, 0f); // ランダム：初期Y座標範囲
                    ApplyPresetValue(RandomEndYRange, 0f); // ランダム：終端Y座標範囲

                    RandomToggleZ = true; // ランダム：Z座標ランダム有効化
                    RandomSEToggleZ = true; // ランダム：Z初期とZ終端を同期
                    ApplyPresetValue(RandomZCount, 1); // ランダム：周期座標Z
                    ApplyPresetValue(RandomStartZRange, 3200f); // ランダム：初期Z座標範囲
                    ApplyPresetValue(RandomEndZRange, 400f); // ランダム：終端Z座標範囲

                    RandomScaleToggle = false; // ランダム：拡大率ランダム有効化
                    RandomSEScaleToggle = false; // ランダム：拡大率初期と終端同期
                    RandomSyScaleToggle = false; // ランダム：拡大率X,Y,Z同期
                    ApplyPresetValue(RandomScaleCount, 1); // ランダム：周期拡大率
                    ApplyPresetValue(RandomStartScaleRange, 0f); // ランダム：初期拡大率
                    ApplyPresetValue(RandomEndScaleRange, 0f); // ランダム：終端拡大率

                    RandomRotXToggle = false; // ランダム：X回転ランダム有効化
                    RandomSERotXToggle = false; // ランダム：X回転初期と終端同期
                    ApplyPresetValue(RandomRotXCount, 1); // ランダム：周期X回転
                    ApplyPresetValue(RandomStartRotXRange, 0f); // ランダム：初期X回転
                    ApplyPresetValue(RandomEndRotXRange, 0f); // ランダム：終端X回転

                    RandomRotYToggle = false; // ランダム：Y回転ランダム有効化
                    RandomSERotYToggle = false; // ランダム：Y回転初期と終端同期
                    ApplyPresetValue(RandomRotYCount, 1); // ランダム：周期Y回転
                    ApplyPresetValue(RandomStartRotYRange, 0f); // ランダム：初期Y回転
                    ApplyPresetValue(RandomEndRotYRange, 0f); // ランダム：終端Y回転

                    RandomRotZToggle = false; // ランダム：Z回転ランダム有効化
                    RandomSERotZToggle = false; // ランダム：Z回転初期と終端同期
                    ApplyPresetValue(RandomRotZCount, 1); // ランダム：周期Z回転
                    ApplyPresetValue(RandomStartRotZRange, 0f); // ランダム：初期Z回転
                    ApplyPresetValue(RandomEndRotZRange, 0f); // ランダム：終端Z回転

                    RandomOpacityToggle = true; // ランダム：不透明度ランダム有効化
                    RandomSEOpacityToggle = false; // ランダム：不透明度初期と終端同期
                    ApplyPresetValue(RandomOpacityCount, 1); // ランダム：周期不透明度
                    ApplyPresetValue(RandomStartOpacityRange, 50f); // ランダム：初期不透明度
                    ApplyPresetValue(RandomEndOpacityRange, 100f); // ランダム：終端不透明度

                    ApplyPresetValue(RandomSeed, 1); // ランダム：シード
                    ApplyPresetValue(CurveRange, 0f); // ランダム：ブレ幅
                    CurveToggle = false; // ランダム：ブレ有効化

                    ApplyPresetValue(AirResistance, 0f); // 抵抗：抵抗

                    SetRandomMovePreset(GravityX, -1600, 1600, 0); // 重力：重力X
                    SetRandomMovePreset(GravityY, -800, 800, 0); // 重力：重力Y
                    SetRandomMovePreset(GravityZ, -1600, 1600, 0); // 重力：重力Z
                    GrTerminationToggle = true; // 重力：終端値に到着

                    StartColor = Colors.White; // 色：初期色
                    EndColor = Colors.White; // 色：終端色

                    RandomColorToggle = false; // ランダム色：色ランダム有効化
                    ApplyPresetValue(RandomColorCount, 1); // ランダム色：周期色
                    ApplyPresetValue(RandomHueRange, 0f); // ランダム色：ランダム色相(H)
                    ApplyPresetValue(RandomSatRange, 0f); // ランダム色：ランダム彩度(S)
                    ApplyPresetValue(RandomLumRange, 0f); // ランダム色：ランダム輝度(L)

                    FocusToggle = true; // ピント：ピント機能有効化
                    FocusFadeToggle = true; // ピント：ピントの不透明度
                    ApplyPresetValue(FocusDepth, -300f); // ピント：深度
                    ApplyPresetValue(FocusRange, 500f); // ピント：範囲
                    ApplyPresetValue(FocusMaxBlur, 11.8f); // ピント：ぼかしの最大値
                    ApplyPresetValue(FocusFallOffBlur, 1000f); // ピント：ぼかしの減衰距離
                    ApplyPresetValue(FocusFadeMinOpacity, 0.25f); // ピント：不透明度の最小値

                    TrailToggle = false; // 残像 : 残像有効化
                    ApplyPresetValue(TrailCount, 10); // 残像 : 残像の数
                    ApplyPresetValue(TrailInterval, 0.005f); // 残像 : 残像の間隔
                    ApplyPresetValue(TrailFade, 0.5f); // 残像 : 残像の減衰率
                    ApplyPresetValue(TrailScale, 100f); // 残像 : 残像の拡大率
                    break;

                case PresetType.Sparkle:
                    ApplyPresetValue(Count, 400); // パーティクル：最大個数
                    ApplyPresetValue(CycleTime, 0.50f); // パーティクル：生成フレーム周期
                    ApplyPresetValue(TravelTime, 70.0f); // パーティクル：個体移動フレーム
                    FixedTrajectory = false; // パーティクル：軌道固定
                    LoopToggle = true; // パーティクル：パーティクルをループ

                    ApplyPresetValue(ReverseDraw, 0); // 描画：描画順反転
                    FixedDraw = false; // 描画：描画順固定
                    ZSortToggle = false; // 描画：旧Zソートを使用
                    BillboardDraw = false; // 描画：Y軸ビルボード化
                    BillboardXYZDraw = false; // 描画：XYZ軸ビルボード化
                    AutoOrient = false; // 描画：進行方向を向く
                    AutoOrient2D = false; // 描画：進行方向2D化
                    CalculationType = MovementCalculationType.EndPosition; // 描画：処理方法（Enum）

                    FloorToggle = false; // 床：床有効化
                    FloorJudgementType = FloorVisibilityType.abovefloor; // 床：床判定（Enum）
                    FloorActionType = FloorType.Glue; // 床：床動作（Enum）
                    ApplyPresetValue(FloorY, 0f); // 床：床座標Y
                    ApplyPresetValue(FloorWaitTime, 0f); // 床：消失開始間時間
                    ApplyPresetValue(FloorFadeTime, 0f); // 床：消失開始終了間時間
                    ApplyPresetValue(BounceEnergyLoss, 0.1f); // 床：エネルギー損失
                    ApplyPresetValue(BounceFactor, 0.5f); // 床：反発係数
                    ApplyPresetValue(BounceGravity, 100f); // 床：反射時重力
                    ApplyPresetValue(BounceCount, 10); // 床：最大反射回数

                    ApplyPresetValue(DelayTime, 0f); // 遅延：遅延

                    ApplyPresetValue(StartX, 0f); // 座標：初期X
                    ApplyPresetValue(StartY, 0f); // 座標：初期Y
                    ApplyPresetValue(StartZ, -800f); // 座標：初期Z
                    ApplyPresetValue(EndX, 0f); // 座標：終端X
                    ApplyPresetValue(EndY, 0f); // 座標：終端Y
                    ApplyPresetValue(EndZ, 0f); // 座標：終端Z
                    PSEToggleX = false; // 座標：初期Xと終端Xを同期
                    PSEToggleY = false; // 座標：初期Yと終端Yを同期
                    PSEToggleZ = true; // 座標：初期Zと終端Zを同期

                    ApplyPresetValue(ForcePitch, 0f); // 力：発射方向X
                    ApplyPresetValue(ForceYaw, 0f); // 力：発射方向Y
                    ApplyPresetValue(ForceRoll, 0f); // 力：発射方向Z
                    ApplyPresetValue(ForceVelocity, 0f); // 力：発射速度
                    ApplyPresetValue(ForceRandomCount, 1); // 力：ランダム力周期
                    ApplyPresetValue(ForceRandomPitch, 0f); // 力：ランダム発射方向X
                    ApplyPresetValue(ForceRandomYaw, 0f); // 力：ランダム発射方向Y
                    ApplyPresetValue(ForceRandomRoll, 0f); // 力：ランダム発射方向Z
                    ApplyPresetValue(ForceRandomVelocity, 0f); // 力：ランダム発射速度

                    ApplyPresetValue(ScaleStartX, 100f); // 拡大率：初期X拡大率
                    ApplyPresetValue(ScaleStartY, 100f); // 拡大率：初期Y拡大率
                    ApplyPresetValue(ScaleStartZ, 100f); // 拡大率：初期Z拡大率
                    ApplyPresetValue(ScaleX, 100f); // 拡大率：終端X拡大率
                    ApplyPresetValue(ScaleY, 100f); // 拡大率：終端Y拡大率
                    ApplyPresetValue(ScaleZ, 100f); // 拡大率：終端Z拡大率

                    ApplyPresetValue(StartOpacity, 0f); // 透明度：初期不透明度
                    ApplyPresetValue(EndOpacity, 0f); // 透明度：終端不透明度
                    OpacityMapToggle = true; // 透明度：不透明度マップ有効化
                    ApplyPresetValue(OpacityMapMidPoint, 100f); // 透明度：中間不透明度
                    ApplyPresetValue(OpacityMapEase, 0.7f); // 透明度：不透明度マップイーズ

                    ApplyPresetValue(StartRotationX, 0f); // 角度：初期X回転
                    ApplyPresetValue(StartRotationY, 0f); // 角度：初期Y回転
                    ApplyPresetValue(StartRotationZ, 0f); // 角度：初期Z回転
                    ApplyPresetValue(EndRotationX, 0f); // 角度：終端X回転
                    ApplyPresetValue(EndRotationY, 0f); // 角度：終端Y回転
                    ApplyPresetValue(EndRotationZ, 0f); // 角度：終端Z回転

                    RandomToggleX = true; // ランダム：X座標ランダム有効化
                    RandomSEToggleX = true; // ランダム：X初期とX終端を同期
                    ApplyPresetValue(RandomXCount, 1); // ランダム：周期X座標
                    ApplyPresetValue(RandomStartXRange, 3200f); // ランダム：初期X座標範囲
                    ApplyPresetValue(RandomEndXRange, 0f); // ランダム：終端X座標範囲

                    RandomToggleY = true; // ランダム：Y座標ランダム有効化
                    RandomSEToggleY = true; // ランダム：Y初期とY終端を同期
                    ApplyPresetValue(RandomYCount, 1); // ランダム：周期Y座標
                    ApplyPresetValue(RandomStartYRange, 1600f); // ランダム：初期Y座標範囲
                    ApplyPresetValue(RandomEndYRange, 0f); // ランダム：終端Y座標範囲

                    RandomToggleZ = true; // ランダム：Z座標ランダム有効化
                    RandomSEToggleZ = true; // ランダム：Z初期とZ終端を同期
                    ApplyPresetValue(RandomZCount, 1); // ランダム：周期座標Z
                    ApplyPresetValue(RandomStartZRange, 1600f); // ランダム：初期Z座標範囲
                    ApplyPresetValue(RandomEndZRange, 0f); // ランダム：終端Z座標範囲

                    RandomScaleToggle = false; // ランダム：拡大率ランダム有効化
                    RandomSEScaleToggle = false; // ランダム：拡大率初期と終端同期
                    RandomSyScaleToggle = false; // ランダム：拡大率X,Y,Z同期
                    ApplyPresetValue(RandomScaleCount, 1); // ランダム：周期拡大率
                    ApplyPresetValue(RandomStartScaleRange, 0f); // ランダム：初期拡大率
                    ApplyPresetValue(RandomEndScaleRange, 0f); // ランダム：終端拡大率

                    RandomRotXToggle = false; // ランダム：X回転ランダム有効化
                    RandomSERotXToggle = false; // ランダム：X回転初期と終端同期
                    ApplyPresetValue(RandomRotXCount, 1); // ランダム：周期X回転
                    ApplyPresetValue(RandomStartRotXRange, 0f); // ランダム：初期X回転
                    ApplyPresetValue(RandomEndRotXRange, 0f); // ランダム：終端X回転

                    RandomRotYToggle = false; // ランダム：Y回転ランダム有効化
                    RandomSERotYToggle = false; // ランダム：Y回転初期と終端同期
                    ApplyPresetValue(RandomRotYCount, 1); // ランダム：周期Y回転
                    ApplyPresetValue(RandomStartRotYRange, 0f); // ランダム：初期Y回転
                    ApplyPresetValue(RandomEndRotYRange, 0f); // ランダム：終端Y回転

                    RandomRotZToggle = false; // ランダム：Z回転ランダム有効化
                    RandomSERotZToggle = false; // ランダム：Z回転初期と終端同期
                    ApplyPresetValue(RandomRotZCount, 1); // ランダム：周期Z回転
                    ApplyPresetValue(RandomStartRotZRange, 0f); // ランダム：初期Z回転
                    ApplyPresetValue(RandomEndRotZRange, 0f); // ランダム：終端Z回転

                    RandomOpacityToggle = false; // ランダム：不透明度ランダム有効化
                    RandomSEOpacityToggle = false; // ランダム：不透明度初期と終端同期
                    ApplyPresetValue(RandomOpacityCount, 1); // ランダム：周期不透明度
                    ApplyPresetValue(RandomStartOpacityRange, 0f); // ランダム：初期不透明度
                    ApplyPresetValue(RandomEndOpacityRange, 100f); // ランダム：終端不透明度

                    ApplyPresetValue(RandomSeed, 1); // ランダム：シード
                    ApplyPresetValue(CurveRange, 0f); // ランダム：ブレ幅
                    CurveToggle = false; // ランダム：ブレ有効化

                    ApplyPresetValue(AirResistance, 0f); // 抵抗：抵抗

                    ApplyPresetValue(GravityX, 0f); // 重力：重力X
                    ApplyPresetValue(GravityY, 0f); // 重力：重力Y
                    ApplyPresetValue(GravityZ, 0f); // 重力：重力Z
                    GrTerminationToggle = true; // 重力：終端値に到着

                    StartColor = Colors.White; // 色：初期色
                    EndColor = Colors.White; // 色：終端色

                    RandomColorToggle = false; // ランダム色：色ランダム有効化
                    ApplyPresetValue(RandomColorCount, 1); // ランダム色：周期色
                    ApplyPresetValue(RandomHueRange, 0f); // ランダム色：ランダム色相(H)
                    ApplyPresetValue(RandomSatRange, 0f); // ランダム色：ランダム彩度(S)
                    ApplyPresetValue(RandomLumRange, 0f); // ランダム色：ランダム輝度(L)

                    FocusToggle = false; // ピント：ピント機能有効化
                    FocusFadeToggle = false; // ピント：ピントの不透明度
                    ApplyPresetValue(FocusDepth, 0f); // ピント：深度
                    ApplyPresetValue(FocusRange, 0f); // ピント：範囲
                    ApplyPresetValue(FocusMaxBlur, 0f); // ピント：ぼかしの最大値
                    ApplyPresetValue(FocusFallOffBlur, 500f); // ピント：ぼかしの減衰距離
                    ApplyPresetValue(FocusFadeMinOpacity, 0.5f); // ピント：不透明度の最小値

                    TrailToggle = false; // 残像 : 残像有効化
                    ApplyPresetValue(TrailCount, 10); // 残像 : 残像の数
                    ApplyPresetValue(TrailInterval, 0.005f); // 残像 : 残像の間隔
                    ApplyPresetValue(TrailFade, 0.5f); // 残像 : 残像の減衰率
                    ApplyPresetValue(TrailScale, 100f); // 残像 : 残像の拡大率
                    break;
                case PresetType.Rain:
                    ApplyPresetValue(Count, 800); // パーティクル：最大個数
                    ApplyPresetValue(CycleTime, 0.25f); // パーティクル：生成フレーム周期
                    ApplyPresetValue(TravelTime, 10.0f); // パーティクル：個体移動フレーム
                    FixedTrajectory = false; // パーティクル：軌道固定
                    LoopToggle = true; // パーティクル：パーティクルをループ

                    ApplyPresetValue(ReverseDraw, 0); // 描画：描画順反転
                    FixedDraw = false; // 描画：描画順固定
                    ZSortToggle = false; // 描画：旧Zソートを使用
                    BillboardDraw = true; // 描画：Y軸ビルボード化
                    BillboardXYZDraw = false; // 描画：XYZ軸ビルボード化
                    AutoOrient = true; // 描画：進行方向を向く
                    AutoOrient2D = true; // 描画：進行方向2D化
                    CalculationType = MovementCalculationType.EndPosition; // 描画：処理方法（Enum）

                    FloorToggle = false; // 床：床有効化
                    FloorJudgementType = FloorVisibilityType.abovefloor; // 床：床判定（Enum）
                    FloorActionType = FloorType.Glue; // 床：床動作（Enum）
                    ApplyPresetValue(FloorY, 0f); // 床：床座標Y
                    ApplyPresetValue(FloorWaitTime, 0f); // 床：消失開始間時間
                    ApplyPresetValue(FloorFadeTime, 0f); // 床：消失開始終了間時間
                    ApplyPresetValue(BounceEnergyLoss, 0.1f); // 床：エネルギー損失
                    ApplyPresetValue(BounceFactor, 0.5f); // 床：反発係数
                    ApplyPresetValue(BounceGravity, 100f); // 床：反射時重力
                    ApplyPresetValue(BounceCount, 10); // 床：最大反射回数

                    ApplyPresetValue(DelayTime, 0f); // 遅延：遅延

                    ApplyPresetValue(StartX, 0f); // 座標：初期X
                    ApplyPresetValue(StartY, -1600f); // 座標：初期Y
                    ApplyPresetValue(StartZ, 0f); // 座標：初期Z
                    ApplyPresetValue(EndX, 0f); // 座標：終端X
                    ApplyPresetValue(EndY, 1600f); // 座標：終端Y
                    ApplyPresetValue(EndZ, 0f); // 座標：終端Z
                    PSEToggleX = false; // 座標：初期Xと終端Xを同期
                    PSEToggleY = false; // 座標：初期Yと終端Yを同期
                    PSEToggleZ = false; // 座標：初期Zと終端Zを同期

                    ApplyPresetValue(ForcePitch, 0f); // 力：発射方向X
                    ApplyPresetValue(ForceYaw, 0f); // 力：発射方向Y
                    ApplyPresetValue(ForceRoll, 0f); // 力：発射方向Z
                    ApplyPresetValue(ForceVelocity, 0f); // 力：発射速度
                    ApplyPresetValue(ForceRandomCount, 1); // 力：ランダム力周期
                    ApplyPresetValue(ForceRandomPitch, 0f); // 力：ランダム発射方向X
                    ApplyPresetValue(ForceRandomYaw, 0f); // 力：ランダム発射方向Y
                    ApplyPresetValue(ForceRandomRoll, 0f); // 力：ランダム発射方向Z
                    ApplyPresetValue(ForceRandomVelocity, 0f); // 力：ランダム発射速度

                    ApplyPresetValue(ScaleStartX, 100f); // 拡大率：初期X拡大率
                    ApplyPresetValue(ScaleStartY, 100f); // 拡大率：初期Y拡大率
                    ApplyPresetValue(ScaleStartZ, 100f); // 拡大率：初期Z拡大率
                    ApplyPresetValue(ScaleX, 100f); // 拡大率：終端X拡大率
                    ApplyPresetValue(ScaleY, 100f); // 拡大率：終端Y拡大率
                    ApplyPresetValue(ScaleZ, 100f); // 拡大率：終端Z拡大率

                    ApplyPresetValue(StartOpacity, 100f); // 透明度：初期不透明度
                    ApplyPresetValue(EndOpacity, 100f); // 透明度：終端不透明度
                    OpacityMapToggle = false; // 透明度：不透明度マップ有効化
                    ApplyPresetValue(OpacityMapMidPoint, 100f); // 透明度：中間不透明度
                    ApplyPresetValue(OpacityMapEase, 0.7f); // 透明度：不透明度マップイーズ

                    ApplyPresetValue(StartRotationX, 0f); // 角度：初期X回転
                    ApplyPresetValue(StartRotationY, 0f); // 角度：初期Y回転
                    ApplyPresetValue(StartRotationZ, 0f); // 角度：初期Z回転
                    ApplyPresetValue(EndRotationX, 0f); // 角度：終端X回転
                    ApplyPresetValue(EndRotationY, 0f); // 角度：終端Y回転
                    ApplyPresetValue(EndRotationZ, 0f); // 角度：終端Z回転

                    RandomToggleX = true; // ランダム：X座標ランダム有効化
                    RandomSEToggleX = true; // ランダム：X初期とX終端を同期
                    ApplyPresetValue(RandomXCount, 1); // ランダム：周期X座標
                    ApplyPresetValue(RandomStartXRange, 3200f); // ランダム：初期X座標範囲
                    ApplyPresetValue(RandomEndXRange, 400f); // ランダム：終端X座標範囲

                    RandomToggleY = false; // ランダム：Y座標ランダム有効化
                    RandomSEToggleY = false; // ランダム：Y初期とY終端を同期
                    ApplyPresetValue(RandomYCount, 1); // ランダム：周期Y座標
                    ApplyPresetValue(RandomStartYRange, 0f); // ランダム：初期Y座標範囲
                    ApplyPresetValue(RandomEndYRange, 0f); // ランダム：終端Y座標範囲

                    RandomToggleZ = true; // ランダム：Z座標ランダム有効化
                    RandomSEToggleZ = true; // ランダム：Z初期とZ終端を同期
                    ApplyPresetValue(RandomZCount, 1); // ランダム：周期座標Z
                    ApplyPresetValue(RandomStartZRange, 3200f); // ランダム：初期Z座標範囲
                    ApplyPresetValue(RandomEndZRange, 400f); // ランダム：終端Z座標範囲

                    RandomScaleToggle = false; // ランダム：拡大率ランダム有効化
                    RandomSEScaleToggle = false; // ランダム：拡大率初期と終端同期
                    RandomSyScaleToggle = false; // ランダム：拡大率X,Y,Z同期
                    ApplyPresetValue(RandomScaleCount, 1); // ランダム：周期拡大率
                    ApplyPresetValue(RandomStartScaleRange, 0f); // ランダム：初期拡大率
                    ApplyPresetValue(RandomEndScaleRange, 0f); // ランダム：終端拡大率

                    RandomRotXToggle = false; // ランダム：X回転ランダム有効化
                    RandomSERotXToggle = false; // ランダム：X回転初期と終端同期
                    ApplyPresetValue(RandomRotXCount, 1); // ランダム：周期X回転
                    ApplyPresetValue(RandomStartRotXRange, 0f); // ランダム：初期X回転
                    ApplyPresetValue(RandomEndRotXRange, 0f); // ランダム：終端X回転

                    RandomRotYToggle = false; // ランダム：Y回転ランダム有効化
                    RandomSERotYToggle = false; // ランダム：Y回転初期と終端同期
                    ApplyPresetValue(RandomRotYCount, 1); // ランダム：周期Y回転
                    ApplyPresetValue(RandomStartRotYRange, 0f); // ランダム：初期Y回転
                    ApplyPresetValue(RandomEndRotYRange, 0f); // ランダム：終端Y回転

                    RandomRotZToggle = false; // ランダム：Z回転ランダム有効化
                    RandomSERotZToggle = false; // ランダム：Z回転初期と終端同期
                    ApplyPresetValue(RandomRotZCount, 1); // ランダム：周期Z回転
                    ApplyPresetValue(RandomStartRotZRange, 0f); // ランダム：初期Z回転
                    ApplyPresetValue(RandomEndRotZRange, 0f); // ランダム：終端Z回転

                    RandomOpacityToggle = true; // ランダム：不透明度ランダム有効化
                    RandomSEOpacityToggle = true; // ランダム：不透明度初期と終端同期
                    ApplyPresetValue(RandomOpacityCount, 1); // ランダム：周期不透明度
                    ApplyPresetValue(RandomStartOpacityRange, 0f); // ランダム：初期不透明度
                    ApplyPresetValue(RandomEndOpacityRange, 40f); // ランダム：終端不透明度

                    ApplyPresetValue(RandomSeed, 1); // ランダム：シード
                    ApplyPresetValue(CurveRange, 0f); // ランダム：ブレ幅
                    CurveToggle = false; // ランダム：ブレ有効化

                    ApplyPresetValue(AirResistance, 0f); // 抵抗：抵抗

                    ApplyPresetValue(GravityX, 0f); // 重力：重力X
                    ApplyPresetValue(GravityY, 0f); // 重力：重力Y
                    ApplyPresetValue(GravityZ, 0f); // 重力：重力Z
                    GrTerminationToggle = true; // 重力：終端値に到着

                    StartColor = Colors.White; // 色：初期色
                    EndColor = Colors.White; // 色：終端色

                    RandomColorToggle = false; // ランダム色：色ランダム有効化
                    ApplyPresetValue(RandomColorCount, 1); // ランダム色：周期色
                    ApplyPresetValue(RandomHueRange, 0f); // ランダム色：ランダム色相(H)
                    ApplyPresetValue(RandomSatRange, 0f); // ランダム色：ランダム彩度(S)
                    ApplyPresetValue(RandomLumRange, 0f); // ランダム色：ランダム輝度(L)

                    FocusToggle = true; // ピント：ピント機能有効化
                    FocusFadeToggle = true; // ピント：ピントの不透明度
                    ApplyPresetValue(FocusDepth, -1000f); // ピント：深度
                    ApplyPresetValue(FocusRange, 400f); // ピント：範囲
                    ApplyPresetValue(FocusMaxBlur, 20f); // ピント：ぼかしの最大値
                    ApplyPresetValue(FocusFallOffBlur, 1000f); // ピント：ぼかしの減衰距離
                    ApplyPresetValue(FocusFadeMinOpacity, 0.5f); // ピント：不透明度の最小値

                    TrailToggle = false; // 残像 : 残像有効化
                    ApplyPresetValue(TrailCount, 10); // 残像 : 残像の数
                    ApplyPresetValue(TrailInterval, 0.005f); // 残像 : 残像の間隔
                    ApplyPresetValue(TrailFade, 0.5f); // 残像 : 残像の減衰率
                    ApplyPresetValue(TrailScale, 100f); // 残像 : 残像の拡大率
                    break;
                case PresetType.RainFloor:
                    ApplyPresetValue(Count, 800); // パーティクル：最大個数
                    ApplyPresetValue(CycleTime, 0.25f); // パーティクル：生成フレーム周期
                    ApplyPresetValue(TravelTime, 10.0f); // パーティクル：個体移動フレーム
                    FixedTrajectory = false; // パーティクル：軌道固定
                    LoopToggle = true; // パーティクル：パーティクルをループ

                    ApplyPresetValue(ReverseDraw, 0); // 描画：描画順反転
                    FixedDraw = false; // 描画：描画順固定
                    ZSortToggle = false; // 描画：旧Zソートを使用
                    BillboardDraw = true; // 描画：Y軸ビルボード化
                    BillboardXYZDraw = false; // 描画：XYZ軸ビルボード化
                    AutoOrient = true; // 描画：進行方向を向く
                    AutoOrient2D = true; // 描画：進行方向2D化
                    CalculationType = MovementCalculationType.EndPosition; // 描画：処理方法（Enum）

                    FloorToggle = true; // 床：床有効化
                    FloorJudgementType = FloorVisibilityType.abovefloor; // 床：床判定（Enum）
                    FloorActionType = FloorType.Bounce; // 床：床動作（Enum）
                    ApplyPresetValue(FloorY, 800f); // 床：床座標Y
                    ApplyPresetValue(FloorWaitTime, 10f); // 床：消失開始間時間
                    ApplyPresetValue(FloorFadeTime, 100f); // 床：消失開始終了間時間
                    ApplyPresetValue(BounceEnergyLoss, 0.1f); // 床：エネルギー損失
                    ApplyPresetValue(BounceFactor, 0.5f); // 床：反発係数
                    ApplyPresetValue(BounceGravity, 100f); // 床：反射時重力
                    ApplyPresetValue(BounceCount, 2); // 床：最大反射回数

                    ApplyPresetValue(DelayTime, 0f); // 遅延：遅延

                    ApplyPresetValue(StartX, 0f); // 座標：初期X
                    ApplyPresetValue(StartY, -1600f); // 座標：初期Y
                    ApplyPresetValue(StartZ, 0f); // 座標：初期Z
                    ApplyPresetValue(EndX, 0f); // 座標：終端X
                    ApplyPresetValue(EndY, 1600f); // 座標：終端Y
                    ApplyPresetValue(EndZ, 0f); // 座標：終端Z
                    PSEToggleX = false; // 座標：初期Xと終端Xを同期
                    PSEToggleY = false; // 座標：初期Yと終端Yを同期
                    PSEToggleZ = false; // 座標：初期Zと終端Zを同期

                    ApplyPresetValue(ForcePitch, 0f); // 力：発射方向X
                    ApplyPresetValue(ForceYaw, 0f); // 力：発射方向Y
                    ApplyPresetValue(ForceRoll, 0f); // 力：発射方向Z
                    ApplyPresetValue(ForceVelocity, 0f); // 力：発射速度
                    ApplyPresetValue(ForceRandomCount, 1); // 力：ランダム力周期
                    ApplyPresetValue(ForceRandomPitch, 0f); // 力：ランダム発射方向X
                    ApplyPresetValue(ForceRandomYaw, 0f); // 力：ランダム発射方向Y
                    ApplyPresetValue(ForceRandomRoll, 0f); // 力：ランダム発射方向Z
                    ApplyPresetValue(ForceRandomVelocity, 0f); // 力：ランダム発射速度

                    ApplyPresetValue(ScaleStartX, 100f); // 拡大率：初期X拡大率
                    ApplyPresetValue(ScaleStartY, 100f); // 拡大率：初期Y拡大率
                    ApplyPresetValue(ScaleStartZ, 100f); // 拡大率：初期Z拡大率
                    ApplyPresetValue(ScaleX, 100f); // 拡大率：終端X拡大率
                    ApplyPresetValue(ScaleY, 100f); // 拡大率：終端Y拡大率
                    ApplyPresetValue(ScaleZ, 100f); // 拡大率：終端Z拡大率

                    ApplyPresetValue(StartOpacity, 100f); // 透明度：初期不透明度
                    ApplyPresetValue(EndOpacity, 100f); // 透明度：終端不透明度
                    OpacityMapToggle = false; // 透明度：不透明度マップ有効化
                    ApplyPresetValue(OpacityMapMidPoint, 100f); // 透明度：中間不透明度
                    ApplyPresetValue(OpacityMapEase, 0.7f); // 透明度：不透明度マップイーズ

                    ApplyPresetValue(StartRotationX, 0f); // 角度：初期X回転
                    ApplyPresetValue(StartRotationY, 0f); // 角度：初期Y回転
                    ApplyPresetValue(StartRotationZ, 0f); // 角度：初期Z回転
                    ApplyPresetValue(EndRotationX, 0f); // 角度：終端X回転
                    ApplyPresetValue(EndRotationY, 0f); // 角度：終端Y回転
                    ApplyPresetValue(EndRotationZ, 0f); // 角度：終端Z回転

                    RandomToggleX = true; // ランダム：X座標ランダム有効化
                    RandomSEToggleX = true; // ランダム：X初期とX終端を同期
                    ApplyPresetValue(RandomXCount, 1); // ランダム：周期X座標
                    ApplyPresetValue(RandomStartXRange, 3200f); // ランダム：初期X座標範囲
                    ApplyPresetValue(RandomEndXRange, 400f); // ランダム：終端X座標範囲

                    RandomToggleY = false; // ランダム：Y座標ランダム有効化
                    RandomSEToggleY = false; // ランダム：Y初期とY終端を同期
                    ApplyPresetValue(RandomYCount, 1); // ランダム：周期Y座標
                    ApplyPresetValue(RandomStartYRange, 0f); // ランダム：初期Y座標範囲
                    ApplyPresetValue(RandomEndYRange, 0f); // ランダム：終端Y座標範囲

                    RandomToggleZ = true; // ランダム：Z座標ランダム有効化
                    RandomSEToggleZ = true; // ランダム：Z初期とZ終端を同期
                    ApplyPresetValue(RandomZCount, 1); // ランダム：周期座標Z
                    ApplyPresetValue(RandomStartZRange, 3200f); // ランダム：初期Z座標範囲
                    ApplyPresetValue(RandomEndZRange, 400f); // ランダム：終端Z座標範囲

                    RandomScaleToggle = false; // ランダム：拡大率ランダム有効化
                    RandomSEScaleToggle = false; // ランダム：拡大率初期と終端同期
                    RandomSyScaleToggle = false; // ランダム：拡大率X,Y,Z同期
                    ApplyPresetValue(RandomScaleCount, 1); // ランダム：周期拡大率
                    ApplyPresetValue(RandomStartScaleRange, 0f); // ランダム：初期拡大率
                    ApplyPresetValue(RandomEndScaleRange, 0f); // ランダム：終端拡大率

                    RandomRotXToggle = false; // ランダム：X回転ランダム有効化
                    RandomSERotXToggle = false; // ランダム：X回転初期と終端同期
                    ApplyPresetValue(RandomRotXCount, 1); // ランダム：周期X回転
                    ApplyPresetValue(RandomStartRotXRange, 0f); // ランダム：初期X回転
                    ApplyPresetValue(RandomEndRotXRange, 0f); // ランダム：終端X回転

                    RandomRotYToggle = false; // ランダム：Y回転ランダム有効化
                    RandomSERotYToggle = false; // ランダム：Y回転初期と終端同期
                    ApplyPresetValue(RandomRotYCount, 1); // ランダム：周期Y回転
                    ApplyPresetValue(RandomStartRotYRange, 0f); // ランダム：初期Y回転
                    ApplyPresetValue(RandomEndRotYRange, 0f); // ランダム：終端Y回転

                    RandomRotZToggle = false; // ランダム：Z回転ランダム有効化
                    RandomSERotZToggle = false; // ランダム：Z回転初期と終端同期
                    ApplyPresetValue(RandomRotZCount, 1); // ランダム：周期Z回転
                    ApplyPresetValue(RandomStartRotZRange, 0f); // ランダム：初期Z回転
                    ApplyPresetValue(RandomEndRotZRange, 0f); // ランダム：終端Z回転

                    RandomOpacityToggle = true; // ランダム：不透明度ランダム有効化
                    RandomSEOpacityToggle = true; // ランダム：不透明度初期と終端同期
                    ApplyPresetValue(RandomOpacityCount, 1); // ランダム：周期不透明度
                    ApplyPresetValue(RandomStartOpacityRange, 0f); // ランダム：初期不透明度
                    ApplyPresetValue(RandomEndOpacityRange, 40f); // ランダム：終端不透明度

                    ApplyPresetValue(RandomSeed, 1); // ランダム：シード
                    ApplyPresetValue(CurveRange, 0f); // ランダム：ブレ幅
                    CurveToggle = false; // ランダム：ブレ有効化

                    ApplyPresetValue(AirResistance, 0f); // 抵抗：抵抗

                    ApplyPresetValue(GravityX, 0f); // 重力：重力X
                    ApplyPresetValue(GravityY, 0f); // 重力：重力Y
                    ApplyPresetValue(GravityZ, 0f); // 重力：重力Z
                    GrTerminationToggle = true; // 重力：終端値に到着

                    StartColor = Colors.White; // 色：初期色
                    EndColor = Colors.White; // 色：終端色

                    RandomColorToggle = false; // ランダム色：色ランダム有効化
                    ApplyPresetValue(RandomColorCount, 1); // ランダム色：周期色
                    ApplyPresetValue(RandomHueRange, 0f); // ランダム色：ランダム色相(H)
                    ApplyPresetValue(RandomSatRange, 0f); // ランダム色：ランダム彩度(S)
                    ApplyPresetValue(RandomLumRange, 0f); // ランダム色：ランダム輝度(L)

                    FocusToggle = true; // ピント：ピント機能有効化
                    FocusFadeToggle = true; // ピント：ピントの不透明度
                    ApplyPresetValue(FocusDepth, -1000f); // ピント：深度
                    ApplyPresetValue(FocusRange, 400f); // ピント：範囲
                    ApplyPresetValue(FocusMaxBlur, 20f); // ピント：ぼかしの最大値
                    ApplyPresetValue(FocusFallOffBlur, 1000f); // ピント：ぼかしの減衰距離
                    ApplyPresetValue(FocusFadeMinOpacity, 0.5f); // ピント：不透明度の最小値

                    TrailToggle = false; // 残像 : 残像有効化
                    ApplyPresetValue(TrailCount, 10); // 残像 : 残像の数
                    ApplyPresetValue(TrailInterval, 0.005f); // 残像 : 残像の間隔
                    ApplyPresetValue(TrailFade, 0.5f); // 残像 : 残像の減衰率
                    ApplyPresetValue(TrailScale, 100f); // 残像 : 残像の拡大率
                    break;
                case PresetType.CarelessFlame:
                    ApplyPresetValue(Count, 800); // パーティクル：最大個数
                    ApplyPresetValue(CycleTime, 1.00f); // パーティクル：生成フレーム周期
                    ApplyPresetValue(TravelTime, 100.0f); // パーティクル：個体移動フレーム
                    FixedTrajectory = true; // パーティクル：軌道固定
                    LoopToggle = true; // パーティクル：パーティクルをループ

                    ApplyPresetValue(ReverseDraw, 0); // 描画：描画順反転
                    FixedDraw = false; // 描画：描画順固定
                    ZSortToggle = false; // 描画：旧Zソートを使用
                    BillboardDraw = false; // 描画：Y軸ビルボード化
                    BillboardXYZDraw = true; // 描画：XYZ軸ビルボード化
                    AutoOrient = false; // 描画：進行方向を向く
                    AutoOrient2D = false; // 描画：進行方向2D化
                    CalculationType = MovementCalculationType.EndPosition; // 描画：処理方法（Enum）

                    FloorToggle = false; // 床：床有効化
                    FloorJudgementType = FloorVisibilityType.abovefloor; // 床：床判定（Enum）
                    FloorActionType = FloorType.Glue; // 床：床動作（Enum）
                    ApplyPresetValue(FloorY, 0f); // 床：床座標Y
                    ApplyPresetValue(FloorWaitTime, 0f); // 床：消失開始間時間
                    ApplyPresetValue(FloorFadeTime, 0f); // 床：消失開始終了間時間
                    ApplyPresetValue(BounceEnergyLoss, 0.1f); // 床：エネルギー損失
                    ApplyPresetValue(BounceFactor, 0.5f); // 床：反発係数
                    ApplyPresetValue(BounceGravity, 100f); // 床：反射時重力
                    ApplyPresetValue(BounceCount, 10); // 床：最大反射回数

                    ApplyPresetValue(DelayTime, 0f); // 遅延：遅延

                    ApplyPresetValue(StartX, 0f); // 座標：初期X
                    ApplyPresetValue(StartY, 400f); // 座標：初期Y
                    ApplyPresetValue(StartZ, 0f); // 座標：初期Z
                    ApplyPresetValue(EndX, 0f); // 座標：終端X
                    ApplyPresetValue(EndY, -400f); // 座標：終端Y
                    ApplyPresetValue(EndZ, 0f); // 座標：終端Z
                    PSEToggleX = false; // 座標：初期Xと終端Xを同期
                    PSEToggleY = false; // 座標：初期Yと終端Yを同期
                    PSEToggleZ = false; // 座標：初期Zと終端Zを同期

                    ApplyPresetValue(ForcePitch, 0f); // 力：発射方向X
                    ApplyPresetValue(ForceYaw, 0f); // 力：発射方向Y
                    ApplyPresetValue(ForceRoll, 0f); // 力：発射方向Z
                    ApplyPresetValue(ForceVelocity, 0f); // 力：発射速度
                    ApplyPresetValue(ForceRandomCount, 1); // 力：ランダム力周期
                    ApplyPresetValue(ForceRandomPitch, 0f); // 力：ランダム発射方向X
                    ApplyPresetValue(ForceRandomYaw, 0f); // 力：ランダム発射方向Y
                    ApplyPresetValue(ForceRandomRoll, 0f); // 力：ランダム発射方向Z
                    ApplyPresetValue(ForceRandomVelocity, 0f); // 力：ランダム発射速度

                    ApplyPresetValue(ScaleStartX, 200f); // 拡大率：初期X拡大率
                    ApplyPresetValue(ScaleStartY, 200f); // 拡大率：初期Y拡大率
                    ApplyPresetValue(ScaleStartZ, 200f); // 拡大率：初期Z拡大率
                    ApplyPresetValue(ScaleX, 50f); // 拡大率：終端X拡大率
                    ApplyPresetValue(ScaleY, 50f); // 拡大率：終端Y拡大率
                    ApplyPresetValue(ScaleZ, 50f); // 拡大率：終端Z拡大率

                    ApplyPresetValue(StartOpacity, 100f); // 透明度：初期不透明度
                    ApplyPresetValue(EndOpacity, 0f); // 透明度：終端不透明度
                    OpacityMapToggle = false; // 透明度：不透明度マップ有効化
                    ApplyPresetValue(OpacityMapMidPoint, 100f); // 透明度：中間不透明度
                    ApplyPresetValue(OpacityMapEase, 0.7f); // 透明度：不透明度マップイーズ

                    ApplyPresetValue(StartRotationX, 0f); // 角度：初期X回転
                    ApplyPresetValue(StartRotationY, 0f); // 角度：初期Y回転
                    ApplyPresetValue(StartRotationZ, 0f); // 角度：初期Z回転
                    ApplyPresetValue(EndRotationX, 0f); // 角度：終端X回転
                    ApplyPresetValue(EndRotationY, 0f); // 角度：終端Y回転
                    ApplyPresetValue(EndRotationZ, 0f); // 角度：終端Z回転

                    RandomToggleX = true; // ランダム：X座標ランダム有効化
                    RandomSEToggleX = false; // ランダム：X初期とX終端を同期
                    ApplyPresetValue(RandomXCount, 1); // ランダム：周期X座標
                    ApplyPresetValue(RandomStartXRange, 100f); // ランダム：初期X座標範囲
                    ApplyPresetValue(RandomEndXRange, 50f); // ランダム：終端X座標範囲

                    RandomToggleY = true; // ランダム：Y座標ランダム有効化
                    RandomSEToggleY = false; // ランダム：Y初期とY終端を同期
                    ApplyPresetValue(RandomYCount, 1); // ランダム：周期Y座標
                    ApplyPresetValue(RandomStartYRange, 0f); // ランダム：初期Y座標範囲
                    ApplyPresetValue(RandomEndYRange, 200f); // ランダム：終端Y座標範囲

                    RandomToggleZ = true; // ランダム：Z座標ランダム有効化
                    RandomSEToggleZ = false; // ランダム：Z初期とZ終端を同期
                    ApplyPresetValue(RandomZCount, 1); // ランダム：周期座標Z
                    ApplyPresetValue(RandomStartZRange, 100f); // ランダム：初期Z座標範囲
                    ApplyPresetValue(RandomEndZRange, 50f); // ランダム：終端Z座標範囲

                    RandomScaleToggle = false; // ランダム：拡大率ランダム有効化
                    RandomSEScaleToggle = false; // ランダム：拡大率初期と終端同期
                    RandomSyScaleToggle = false; // ランダム：拡大率X,Y,Z同期
                    ApplyPresetValue(RandomScaleCount, 1); // ランダム：周期拡大率
                    ApplyPresetValue(RandomStartScaleRange, 0f); // ランダム：初期拡大率
                    ApplyPresetValue(RandomEndScaleRange, 0f); // ランダム：終端拡大率

                    RandomRotXToggle = false; // ランダム：X回転ランダム有効化
                    RandomSERotXToggle = false; // ランダム：X回転初期と終端同期
                    ApplyPresetValue(RandomRotXCount, 1); // ランダム：周期X回転
                    ApplyPresetValue(RandomStartRotXRange, 0f); // ランダム：初期X回転
                    ApplyPresetValue(RandomEndRotXRange, 0f); // ランダム：終端X回転

                    RandomRotYToggle = false; // ランダム：Y回転ランダム有効化
                    RandomSERotYToggle = false; // ランダム：Y回転初期と終端同期
                    ApplyPresetValue(RandomRotYCount, 1); // ランダム：周期Y回転
                    ApplyPresetValue(RandomStartRotYRange, 0f); // ランダム：初期Y回転
                    ApplyPresetValue(RandomEndRotYRange, 0f); // ランダム：終端Y回転

                    RandomRotZToggle = false; // ランダム：Z回転ランダム有効化
                    RandomSERotZToggle = false; // ランダム：Z回転初期と終端同期
                    ApplyPresetValue(RandomRotZCount, 1); // ランダム：周期Z回転
                    ApplyPresetValue(RandomStartRotZRange, 0f); // ランダム：初期Z回転
                    ApplyPresetValue(RandomEndRotZRange, 0f); // ランダム：終端Z回転

                    RandomOpacityToggle = false; // ランダム：不透明度ランダム有効化
                    RandomSEOpacityToggle = false; // ランダム：不透明度初期と終端同期
                    ApplyPresetValue(RandomOpacityCount, 1); // ランダム：周期不透明度
                    ApplyPresetValue(RandomStartOpacityRange, 0f); // ランダム：初期不透明度
                    ApplyPresetValue(RandomEndOpacityRange, 100f); // ランダム：終端不透明度

                    ApplyPresetValue(RandomSeed, 1); // ランダム：シード
                    ApplyPresetValue(CurveRange, 0f); // ランダム：ブレ幅
                    CurveToggle = false; // ランダム：ブレ有効化

                    ApplyPresetValue(AirResistance, 0f); // 抵抗：抵抗

                    SetRandomMovePreset(GravityX, -800, 800, 0.37f); // 重力：重力X
                    SetRandomMovePreset(GravityY, -800, 800, 0.15f); // 重力：重力Y
                    SetRandomMovePreset(GravityZ, -800, 800, 0.32f); // 重力：重力Z
                    GrTerminationToggle = true; // 重力：終端値に到着

                    StartColor = Colors.Yellow; // 色：初期色
                    EndColor = Colors.Red; // 色：終端色

                    RandomColorToggle = true; // ランダム色：色ランダム有効化
                    ApplyPresetValue(RandomColorCount, 1); // ランダム色：周期色
                    ApplyPresetValue(RandomHueRange, 20f); // ランダム色：ランダム色相(H)
                    ApplyPresetValue(RandomSatRange, 0f); // ランダム色：ランダム彩度(S)
                    ApplyPresetValue(RandomLumRange, 0f); // ランダム色：ランダム輝度(L)

                    FocusToggle = false; // ピント：ピント機能有効化
                    FocusFadeToggle = false; // ピント：ピントの不透明度
                    ApplyPresetValue(FocusDepth, 0f); // ピント：深度
                    ApplyPresetValue(FocusRange, 0f); // ピント：範囲
                    ApplyPresetValue(FocusMaxBlur, 0f); // ピント：ぼかしの最大値
                    ApplyPresetValue(FocusFallOffBlur, 500f); // ピント：ぼかしの減衰距離
                    ApplyPresetValue(FocusFadeMinOpacity, 0.5f); // ピント：不透明度の最小値

                    TrailToggle = false; // 残像 : 残像有効化
                    ApplyPresetValue(TrailCount, 10); // 残像 : 残像の数
                    ApplyPresetValue(TrailInterval, 0.005f); // 残像 : 残像の間隔
                    ApplyPresetValue(TrailFade, 0.5f); // 残像 : 残像の減衰率
                    ApplyPresetValue(TrailScale, 100f); // 残像 : 残像の拡大率
                    break;
                case PresetType.sparks:
                    ApplyPresetValue(Count, 800); // パーティクル：最大個数
                    ApplyPresetValue(CycleTime, 1.00f); // パーティクル：生成フレーム周期
                    ApplyPresetValue(TravelTime, 100.0f); // パーティクル：個体移動フレーム
                    FixedTrajectory = true; // パーティクル：軌道固定
                    LoopToggle = true; // パーティクル：パーティクルをループ

                    ApplyPresetValue(ReverseDraw, 0); // 描画：描画順反転
                    FixedDraw = false; // 描画：描画順固定
                    ZSortToggle = false; // 描画：旧Zソートを使用
                    BillboardDraw = false; // 描画：Y軸ビルボード化
                    BillboardXYZDraw = false; // 描画：XYZ軸ビルボード化
                    AutoOrient = false; // 描画：進行方向を向く
                    AutoOrient2D = false; // 描画：進行方向2D化
                    CalculationType = MovementCalculationType.EndPosition; // 描画：処理方法（Enum）

                    FloorToggle = false; // 床：床有効化
                    FloorJudgementType = FloorVisibilityType.abovefloor; // 床：床判定（Enum）
                    FloorActionType = FloorType.Glue; // 床：床動作（Enum）
                    ApplyPresetValue(FloorY, 0f); // 床：床座標Y
                    ApplyPresetValue(FloorWaitTime, 0f); // 床：消失開始間時間
                    ApplyPresetValue(FloorFadeTime, 0f); // 床：消失開始終了間時間
                    ApplyPresetValue(BounceEnergyLoss, 0.1f); // 床：エネルギー損失
                    ApplyPresetValue(BounceFactor, 0.5f); // 床：反発係数
                    ApplyPresetValue(BounceGravity, 100f); // 床：反射時重力
                    ApplyPresetValue(BounceCount, 10); // 床：最大反射回数

                    ApplyPresetValue(DelayTime, 0f); // 遅延：遅延

                    ApplyPresetValue(StartX, -1600f); // 座標：初期X
                    ApplyPresetValue(StartY, 800f); // 座標：初期Y
                    ApplyPresetValue(StartZ, 0f); // 座標：初期Z
                    ApplyPresetValue(EndX, 1600f); // 座標：終端X
                    ApplyPresetValue(EndY, -800f); // 座標：終端Y
                    ApplyPresetValue(EndZ, 0f); // 座標：終端Z
                    PSEToggleX = false; // 座標：初期Xと終端Xを同期
                    PSEToggleY = false; // 座標：初期Yと終端Yを同期
                    PSEToggleZ = false; // 座標：初期Zと終端Zを同期

                    ApplyPresetValue(ForcePitch, 0f); // 力：発射方向X
                    ApplyPresetValue(ForceYaw, 0f); // 力：発射方向Y
                    ApplyPresetValue(ForceRoll, 0f); // 力：発射方向Z
                    ApplyPresetValue(ForceVelocity, 0f); // 力：発射速度
                    ApplyPresetValue(ForceRandomCount, 1); // 力：ランダム力周期
                    ApplyPresetValue(ForceRandomPitch, 0f); // 力：ランダム発射方向X
                    ApplyPresetValue(ForceRandomYaw, 0f); // 力：ランダム発射方向Y
                    ApplyPresetValue(ForceRandomRoll, 0f); // 力：ランダム発射方向Z
                    ApplyPresetValue(ForceRandomVelocity, 0f); // 力：ランダム発射速度

                    ApplyPresetValue(ScaleStartX, 100f); // 拡大率：初期X拡大率
                    ApplyPresetValue(ScaleStartY, 100f); // 拡大率：初期Y拡大率
                    ApplyPresetValue(ScaleStartZ, 100f); // 拡大率：初期Z拡大率
                    ApplyPresetValue(ScaleX, 100f); // 拡大率：終端X拡大率
                    ApplyPresetValue(ScaleY, 100f); // 拡大率：終端Y拡大率
                    ApplyPresetValue(ScaleZ, 100f); // 拡大率：終端Z拡大率

                    ApplyPresetValue(StartOpacity, 100f); // 透明度：初期不透明度
                    ApplyPresetValue(EndOpacity, 100f); // 透明度：終端不透明度
                    OpacityMapToggle = false; // 透明度：不透明度マップ有効化
                    ApplyPresetValue(OpacityMapMidPoint, 100f); // 透明度：中間不透明度
                    ApplyPresetValue(OpacityMapEase, 0.7f); // 透明度：不透明度マップイーズ

                    ApplyPresetValue(StartRotationX, 0f); // 角度：初期X回転
                    ApplyPresetValue(StartRotationY, 0f); // 角度：初期Y回転
                    ApplyPresetValue(StartRotationZ, 0f); // 角度：初期Z回転
                    ApplyPresetValue(EndRotationX, 0f); // 角度：終端X回転
                    ApplyPresetValue(EndRotationY, 0f); // 角度：終端Y回転
                    ApplyPresetValue(EndRotationZ, 0f); // 角度：終端Z回転

                    RandomToggleX = true; // ランダム：X座標ランダム有効化
                    RandomSEToggleX = true; // ランダム：X初期とX終端を同期
                    ApplyPresetValue(RandomXCount, 1); // ランダム：周期X座標
                    ApplyPresetValue(RandomStartXRange, 400f); // ランダム：初期X座標範囲
                    ApplyPresetValue(RandomEndXRange, 400f); // ランダム：終端X座標範囲

                    RandomToggleY = true; // ランダム：Y座標ランダム有効化
                    RandomSEToggleY = true; // ランダム：Y初期とY終端を同期
                    ApplyPresetValue(RandomYCount, 1); // ランダム：周期Y座標
                    ApplyPresetValue(RandomStartYRange, 3200f); // ランダム：初期Y座標範囲
                    ApplyPresetValue(RandomEndYRange, 800f); // ランダム：終端Y座標範囲

                    RandomToggleZ = true; // ランダム：Z座標ランダム有効化
                    RandomSEToggleZ = true; // ランダム：Z初期とZ終端を同期
                    ApplyPresetValue(RandomZCount, 1); // ランダム：周期座標Z
                    ApplyPresetValue(RandomStartZRange, 1600f); // ランダム：初期Z座標範囲
                    ApplyPresetValue(RandomEndZRange, 800f); // ランダム：終端Z座標範囲

                    RandomScaleToggle = true; // ランダム：拡大率ランダム有効化
                    RandomSEScaleToggle = true; // ランダム：拡大率初期と終端同期
                    RandomSyScaleToggle = true; // ランダム：拡大率X,Y,Z同期
                    ApplyPresetValue(RandomScaleCount, 1); // ランダム：周期拡大率
                    ApplyPresetValue(RandomStartScaleRange, 50f); // ランダム：初期拡大率
                    ApplyPresetValue(RandomEndScaleRange, 0f); // ランダム：終端拡大率

                    RandomRotXToggle = true; // ランダム：X回転ランダム有効化
                    RandomSERotXToggle = false; // ランダム：X回転初期と終端同期
                    ApplyPresetValue(RandomRotXCount, 1); // ランダム：周期X回転
                    ApplyPresetValue(RandomStartRotXRange, 360f); // ランダム：初期X回転
                    ApplyPresetValue(RandomEndRotXRange, 360f); // ランダム：終端X回転

                    RandomRotYToggle = true; // ランダム：Y回転ランダム有効化
                    RandomSERotYToggle = false; // ランダム：Y回転初期と終端同期
                    ApplyPresetValue(RandomRotYCount, 1); // ランダム：周期Y回転
                    ApplyPresetValue(RandomStartRotYRange, 360f); // ランダム：初期Y回転
                    ApplyPresetValue(RandomEndRotYRange, 360f); // ランダム：終端Y回転

                    RandomRotZToggle = true; // ランダム：Z回転ランダム有効化
                    RandomSERotZToggle = false; // ランダム：Z回転初期と終端同期
                    ApplyPresetValue(RandomRotZCount, 1); // ランダム：周期Z回転
                    ApplyPresetValue(RandomStartRotZRange, 360f); // ランダム：初期Z回転
                    ApplyPresetValue(RandomEndRotZRange, 360f); // ランダム：終端Z回転

                    RandomOpacityToggle = true; // ランダム：不透明度ランダム有効化
                    RandomSEOpacityToggle = false; // ランダム：不透明度初期と終端同期
                    ApplyPresetValue(RandomOpacityCount, 1); // ランダム：周期不透明度
                    ApplyPresetValue(RandomStartOpacityRange, 0f); // ランダム：初期不透明度
                    ApplyPresetValue(RandomEndOpacityRange, 100f); // ランダム：終端不透明度

                    ApplyPresetValue(RandomSeed, 1); // ランダム：シード
                    ApplyPresetValue(CurveRange, 0f); // ランダム：ブレ幅
                    CurveToggle = false; // ランダム：ブレ有効化

                    ApplyPresetValue(AirResistance, 0f); // 抵抗：抵抗

                    SetRandomMovePreset(GravityX, -800, 800, 0f); // 重力：重力X
                    SetRandomMovePreset(GravityY, -800, 800, 0f); // 重力：重力Y
                    SetRandomMovePreset(GravityZ, -800, 800, 0f); // 重力：重力Z
                    GrTerminationToggle = true; // 重力：終端値に到着

                    StartColor = Colors.White; // 色：初期色
                    EndColor = Colors.White; // 色：終端色

                    RandomColorToggle = false; // ランダム色：色ランダム有効化
                    ApplyPresetValue(RandomColorCount, 1); // ランダム色：周期色
                    ApplyPresetValue(RandomHueRange, 0f); // ランダム色：ランダム色相(H)
                    ApplyPresetValue(RandomSatRange, 0f); // ランダム色：ランダム彩度(S)
                    ApplyPresetValue(RandomLumRange, 0f); // ランダム色：ランダム輝度(L)

                    FocusToggle = true; // ピント：ピント機能有効化
                    FocusFadeToggle = false; // ピント：ピントの不透明度
                    ApplyPresetValue(FocusDepth, 0f); // ピント：深度
                    ApplyPresetValue(FocusRange, 0f); // ピント：範囲
                    ApplyPresetValue(FocusMaxBlur, 4f); // ピント：ぼかしの最大値
                    ApplyPresetValue(FocusFallOffBlur, 1000f); // ピント：ぼかしの減衰距離
                    ApplyPresetValue(FocusFadeMinOpacity, 0.5f); // ピント：不透明度の最小値

                    TrailToggle = false; // 残像 : 残像有効化
                    ApplyPresetValue(TrailCount, 10); // 残像 : 残像の数
                    ApplyPresetValue(TrailInterval, 0.005f); // 残像 : 残像の間隔
                    ApplyPresetValue(TrailFade, 0.5f); // 残像 : 残像の減衰率
                    ApplyPresetValue(TrailScale, 100f); // 残像 : 残像の拡大率
                    break;
                case PresetType.Fluffy:
                    ApplyPresetValue(Count, 400); // パーティクル：最大個数
                    ApplyPresetValue(CycleTime, 1.00f); // パーティクル：生成フレーム周期
                    ApplyPresetValue(TravelTime, 100.0f); // パーティクル：個体移動フレーム
                    FixedTrajectory = true; // パーティクル：軌道固定
                    LoopToggle = true; // パーティクル：パーティクルをループ

                    ApplyPresetValue(ReverseDraw, 0); // 描画：描画順反転
                    FixedDraw = false; // 描画：描画順固定
                    ZSortToggle = false; // 描画：旧Zソートを使用
                    BillboardDraw = false; // 描画：Y軸ビルボード化
                    BillboardXYZDraw = true; // 描画：XYZ軸ビルボード化
                    AutoOrient = false; // 描画：進行方向を向く
                    AutoOrient2D = false; // 描画：進行方向2D化
                    CalculationType = MovementCalculationType.EndPosition; // 描画：処理方法（Enum）

                    FloorToggle = false; // 床：床有効化
                    FloorJudgementType = FloorVisibilityType.abovefloor; // 床：床判定（Enum）
                    FloorActionType = FloorType.Glue; // 床：床動作（Enum）
                    ApplyPresetValue(FloorY, 0f); // 床：床座標Y
                    ApplyPresetValue(FloorWaitTime, 0f); // 床：消失開始間時間
                    ApplyPresetValue(FloorFadeTime, 0f); // 床：消失開始終了間時間
                    ApplyPresetValue(BounceEnergyLoss, 0.1f); // 床：エネルギー損失
                    ApplyPresetValue(BounceFactor, 0.5f); // 床：反発係数
                    ApplyPresetValue(BounceGravity, 100f); // 床：反射時重力
                    ApplyPresetValue(BounceCount, 10); // 床：最大反射回数

                    ApplyPresetValue(DelayTime, 0f); // 遅延：遅延

                    ApplyPresetValue(StartX, 0f); // 座標：初期X
                    ApplyPresetValue(StartY, 0f); // 座標：初期Y
                    ApplyPresetValue(StartZ, 0f); // 座標：初期Z
                    ApplyPresetValue(EndX, 0f); // 座標：終端X
                    ApplyPresetValue(EndY, 0f); // 座標：終端Y
                    ApplyPresetValue(EndZ, 0f); // 座標：終端Z
                    PSEToggleX = false; // 座標：初期Xと終端Xを同期
                    PSEToggleY = false; // 座標：初期Yと終端Yを同期
                    PSEToggleZ = false; // 座標：初期Zと終端Zを同期

                    ApplyPresetValue(ForcePitch, 0f); // 力：発射方向X
                    ApplyPresetValue(ForceYaw, 0f); // 力：発射方向Y
                    ApplyPresetValue(ForceRoll, 0f); // 力：発射方向Z
                    ApplyPresetValue(ForceVelocity, 0f); // 力：発射速度
                    ApplyPresetValue(ForceRandomCount, 1); // 力：ランダム力周期
                    ApplyPresetValue(ForceRandomPitch, 0f); // 力：ランダム発射方向X
                    ApplyPresetValue(ForceRandomYaw, 0f); // 力：ランダム発射方向Y
                    ApplyPresetValue(ForceRandomRoll, 0f); // 力：ランダム発射方向Z
                    ApplyPresetValue(ForceRandomVelocity, 0f); // 力：ランダム発射速度

                    ApplyPresetValue(ScaleStartX, 100f); // 拡大率：初期X拡大率
                    ApplyPresetValue(ScaleStartY, 100f); // 拡大率：初期Y拡大率
                    ApplyPresetValue(ScaleStartZ, 100f); // 拡大率：初期Z拡大率
                    ApplyPresetValue(ScaleX, 100f); // 拡大率：終端X拡大率
                    ApplyPresetValue(ScaleY, 100f); // 拡大率：終端Y拡大率
                    ApplyPresetValue(ScaleZ, 100f); // 拡大率：終端Z拡大率

                    ApplyPresetValue(StartOpacity, 0f); // 透明度：初期不透明度
                    ApplyPresetValue(EndOpacity, 0f); // 透明度：終端不透明度
                    OpacityMapToggle = true; // 透明度：不透明度マップ有効化
                    SetRandomMovePreset(OpacityMapMidPoint, 50f, 100f, 0f); // 透明度：中間不透明度
                    ApplyPresetValue(OpacityMapEase, 0.5f); // 透明度：不透明度マップイーズ

                    ApplyPresetValue(StartRotationX, 0f); // 角度：初期X回転
                    ApplyPresetValue(StartRotationY, 0f); // 角度：初期Y回転
                    ApplyPresetValue(StartRotationZ, 0f); // 角度：初期Z回転
                    ApplyPresetValue(EndRotationX, 0f); // 角度：終端X回転
                    ApplyPresetValue(EndRotationY, 0f); // 角度：終端Y回転
                    ApplyPresetValue(EndRotationZ, 0f); // 角度：終端Z回転

                    RandomToggleX = true; // ランダム：X座標ランダム有効化
                    RandomSEToggleX = true; // ランダム：X初期とX終端を同期
                    ApplyPresetValue(RandomXCount, 1); // ランダム：周期X座標
                    ApplyPresetValue(RandomStartXRange, 3200f); // ランダム：初期X座標範囲
                    ApplyPresetValue(RandomEndXRange, 200f); // ランダム：終端X座標範囲

                    RandomToggleY = true; // ランダム：Y座標ランダム有効化
                    RandomSEToggleY = true; // ランダム：Y初期とY終端を同期
                    ApplyPresetValue(RandomYCount, 1); // ランダム：周期Y座標
                    ApplyPresetValue(RandomStartYRange, 2000f); // ランダム：初期Y座標範囲
                    ApplyPresetValue(RandomEndYRange, 200f); // ランダム：終端Y座標範囲

                    RandomToggleZ = true; // ランダム：Z座標ランダム有効化
                    RandomSEToggleZ = true; // ランダム：Z初期とZ終端を同期
                    ApplyPresetValue(RandomZCount, 1); // ランダム：周期座標Z
                    ApplyPresetValue(RandomStartZRange, 3200f); // ランダム：初期Z座標範囲
                    ApplyPresetValue(RandomEndZRange, 200f); // ランダム：終端Z座標範囲

                    RandomScaleToggle = true; // ランダム：拡大率ランダム有効化
                    RandomSEScaleToggle = true; // ランダム：拡大率初期と終端同期
                    RandomSyScaleToggle = true; // ランダム：拡大率X,Y,Z同期
                    ApplyPresetValue(RandomScaleCount, 1); // ランダム：周期拡大率
                    ApplyPresetValue(RandomStartScaleRange, 25f); // ランダム：初期拡大率
                    ApplyPresetValue(RandomEndScaleRange, 0f); // ランダム：終端拡大率

                    RandomRotXToggle = false; // ランダム：X回転ランダム有効化
                    RandomSERotXToggle = false; // ランダム：X回転初期と終端同期
                    ApplyPresetValue(RandomRotXCount, 1); // ランダム：周期X回転
                    ApplyPresetValue(RandomStartRotXRange, 0f); // ランダム：初期X回転
                    ApplyPresetValue(RandomEndRotXRange, 0f); // ランダム：終端X回転

                    RandomRotYToggle = false; // ランダム：Y回転ランダム有効化
                    RandomSERotYToggle = false; // ランダム：Y回転初期と終端同期
                    ApplyPresetValue(RandomRotYCount, 1); // ランダム：周期Y回転
                    ApplyPresetValue(RandomStartRotYRange, 0f); // ランダム：初期Y回転
                    ApplyPresetValue(RandomEndRotYRange, 0f); // ランダム：終端Y回転

                    RandomRotZToggle = false; // ランダム：Z回転ランダム有効化
                    RandomSERotZToggle = false; // ランダム：Z回転初期と終端同期
                    ApplyPresetValue(RandomRotZCount, 1); // ランダム：周期Z回転
                    ApplyPresetValue(RandomStartRotZRange, 0f); // ランダム：初期Z回転
                    ApplyPresetValue(RandomEndRotZRange, 0f); // ランダム：終端Z回転

                    RandomOpacityToggle = false; // ランダム：不透明度ランダム有効化
                    RandomSEOpacityToggle = false; // ランダム：不透明度初期と終端同期
                    ApplyPresetValue(RandomOpacityCount, 1); // ランダム：周期不透明度
                    ApplyPresetValue(RandomStartOpacityRange, 0f); // ランダム：初期不透明度
                    ApplyPresetValue(RandomEndOpacityRange, 100f); // ランダム：終端不透明度

                    ApplyPresetValue(RandomSeed, 1); // ランダム：シード
                    ApplyPresetValue(CurveRange, 0f); // ランダム：ブレ幅
                    CurveToggle = false; // ランダム：ブレ有効化

                    ApplyPresetValue(AirResistance, 0f); // 抵抗：抵抗

                    ApplyPresetValue(GravityX, 0f); // 重力：重力X
                    ApplyPresetValue(GravityY, 0f); // 重力：重力Y
                    ApplyPresetValue(GravityZ, 0f); // 重力：重力Z
                    GrTerminationToggle = true; // 重力：終端値に到着

                    StartColor = Colors.White; // 色：初期色
                    EndColor = Colors.White; // 色：終端色

                    RandomColorToggle = false; // ランダム色：色ランダム有効化
                    ApplyPresetValue(RandomColorCount, 1); // ランダム色：周期色
                    ApplyPresetValue(RandomHueRange, 0f); // ランダム色：ランダム色相(H)
                    ApplyPresetValue(RandomSatRange, 0f); // ランダム色：ランダム彩度(S)
                    ApplyPresetValue(RandomLumRange, 0f); // ランダム色：ランダム輝度(L)

                    FocusToggle = false; // ピント：ピント機能有効化
                    FocusFadeToggle = false; // ピント：ピントの不透明度
                    ApplyPresetValue(FocusDepth, 0f); // ピント：深度
                    ApplyPresetValue(FocusRange, 0f); // ピント：範囲
                    ApplyPresetValue(FocusMaxBlur, 0f); // ピント：ぼかしの最大値
                    ApplyPresetValue(FocusFallOffBlur, 500f); // ピント：ぼかしの減衰距離
                    ApplyPresetValue(FocusFadeMinOpacity, 0.5f); // ピント：不透明度の最小値

                    TrailToggle = false; // 残像 : 残像有効化
                    ApplyPresetValue(TrailCount, 10); // 残像 : 残像の数
                    ApplyPresetValue(TrailInterval, 0.005f); // 残像 : 残像の間隔
                    ApplyPresetValue(TrailFade, 0.5f); // 残像 : 残像の減衰率
                    ApplyPresetValue(TrailScale, 100f); // 残像 : 残像の拡大率
                    break;
                case PresetType.AEDefault:
                    ApplyPresetValue(Count, 1600); // パーティクル：最大個数
                    ApplyPresetValue(CycleTime, 0.25f); // パーティクル：生成フレーム周期
                    ApplyPresetValue(TravelTime, 130.0f); // パーティクル：個体移動フレーム
                    FixedTrajectory = true; // パーティクル：軌道固定
                    LoopToggle = true; // パーティクル：パーティクルをループ

                    ApplyPresetValue(ReverseDraw, 0); // 描画：描画順反転
                    FixedDraw = false; // 描画：描画順固定
                    ZSortToggle = false; // 描画：旧Zソートを使用
                    BillboardDraw = false; // 描画：Y軸ビルボード化
                    BillboardXYZDraw = false; // 描画：XYZ軸ビルボード化
                    AutoOrient = true; // 描画：進行方向を向く
                    AutoOrient2D = false; // 描画：進行方向2D化
                    CalculationType = MovementCalculationType.Force; // 描画：処理方法（Enum）

                    FloorToggle = false; // 床：床有効化
                    FloorJudgementType = FloorVisibilityType.abovefloor; // 床：床判定（Enum）
                    FloorActionType = FloorType.Glue; // 床：床動作（Enum）
                    ApplyPresetValue(FloorY, 0f); // 床：床座標Y
                    ApplyPresetValue(FloorWaitTime, 0f); // 床：消失開始間時間
                    ApplyPresetValue(FloorFadeTime, 0f); // 床：消失開始終了間時間
                    ApplyPresetValue(BounceEnergyLoss, 0.1f); // 床：エネルギー損失
                    ApplyPresetValue(BounceFactor, 0.5f); // 床：反発係数
                    ApplyPresetValue(BounceGravity, 100f); // 床：反射時重力
                    ApplyPresetValue(BounceCount, 10); // 床：最大反射回数

                    ApplyPresetValue(DelayTime, 0f); // 遅延：遅延

                    ApplyPresetValue(StartX, 0f); // 座標：初期X
                    ApplyPresetValue(StartY, 0f); // 座標：初期Y
                    ApplyPresetValue(StartZ, 0f); // 座標：初期Z
                    ApplyPresetValue(EndX, 0f); // 座標：終端X
                    ApplyPresetValue(EndY, 0f); // 座標：終端Y
                    ApplyPresetValue(EndZ, 0f); // 座標：終端Z
                    PSEToggleX = false; // 座標：初期Xと終端Xを同期
                    PSEToggleY = false; // 座標：初期Yと終端Yを同期
                    PSEToggleZ = false; // 座標：初期Zと終端Zを同期

                    ApplyPresetValue(ForcePitch, 90f); // 力：発射方向X
                    ApplyPresetValue(ForceYaw, 0f); // 力：発射方向Y
                    ApplyPresetValue(ForceRoll, 0f); // 力：発射方向Z
                    ApplyPresetValue(ForceVelocity, 600f); // 力：発射速度
                    ApplyPresetValue(ForceRandomCount, 1); // 力：ランダム力周期
                    ApplyPresetValue(ForceRandomPitch, 90f); // 力：ランダム発射方向X
                    ApplyPresetValue(ForceRandomYaw, 180f); // 力：ランダム発射方向Y
                    ApplyPresetValue(ForceRandomRoll, 0f); // 力：ランダム発射方向Z
                    ApplyPresetValue(ForceRandomVelocity, 0f); // 力：ランダム発射速度

                    ApplyPresetValue(ScaleStartX, 100f); // 拡大率：初期X拡大率
                    ApplyPresetValue(ScaleStartY, 100f); // 拡大率：初期Y拡大率
                    ApplyPresetValue(ScaleStartZ, 100f); // 拡大率：初期Z拡大率
                    ApplyPresetValue(ScaleX, 100f); // 拡大率：終端X拡大率
                    ApplyPresetValue(ScaleY, 100f); // 拡大率：終端Y拡大率
                    ApplyPresetValue(ScaleZ, 100f); // 拡大率：終端Z拡大率

                    ApplyPresetValue(StartOpacity, 100f); // 透明度：初期不透明度
                    ApplyPresetValue(EndOpacity, 100f); // 透明度：終端不透明度
                    OpacityMapToggle = false; // 透明度：不透明度マップ有効化
                    ApplyPresetValue(OpacityMapMidPoint, 100f); // 透明度：中間不透明度
                    ApplyPresetValue(OpacityMapEase, 0.7f); // 透明度：不透明度マップイーズ

                    ApplyPresetValue(StartRotationX, 0f); // 角度：初期X回転
                    ApplyPresetValue(StartRotationY, 0f); // 角度：初期Y回転
                    ApplyPresetValue(StartRotationZ, 0f); // 角度：初期Z回転
                    ApplyPresetValue(EndRotationX, 0f); // 角度：終端X回転
                    ApplyPresetValue(EndRotationY, 0f); // 角度：終端Y回転
                    ApplyPresetValue(EndRotationZ, 0f); // 角度：終端Z回転

                    RandomToggleX = false; // ランダム：X座標ランダム有効化
                    RandomSEToggleX = false; // ランダム：X初期とX終端を同期
                    ApplyPresetValue(RandomXCount, 1); // ランダム：周期X座標
                    ApplyPresetValue(RandomStartXRange, 0f); // ランダム：初期X座標範囲
                    ApplyPresetValue(RandomEndXRange, 0f); // ランダム：終端X座標範囲

                    RandomToggleY = false; // ランダム：Y座標ランダム有効化
                    RandomSEToggleY = false; // ランダム：Y初期とY終端を同期
                    ApplyPresetValue(RandomYCount, 1); // ランダム：周期Y座標
                    ApplyPresetValue(RandomStartYRange, 0f); // ランダム：初期Y座標範囲
                    ApplyPresetValue(RandomEndYRange, 0f); // ランダム：終端Y座標範囲

                    RandomToggleZ = false; // ランダム：Z座標ランダム有効化
                    RandomSEToggleZ = false; // ランダム：Z初期とZ終端を同期
                    ApplyPresetValue(RandomZCount, 1); // ランダム：周期座標Z
                    ApplyPresetValue(RandomStartZRange, 0f); // ランダム：初期Z座標範囲
                    ApplyPresetValue(RandomEndZRange, 0f); // ランダム：終端Z座標範囲

                    RandomScaleToggle = false; // ランダム：拡大率ランダム有効化
                    RandomSEScaleToggle = false; // ランダム：拡大率初期と終端同期
                    RandomSyScaleToggle = false; // ランダム：拡大率X,Y,Z同期
                    ApplyPresetValue(RandomScaleCount, 1); // ランダム：周期拡大率
                    ApplyPresetValue(RandomStartScaleRange, 0f); // ランダム：初期拡大率
                    ApplyPresetValue(RandomEndScaleRange, 0f); // ランダム：終端拡大率

                    RandomRotXToggle = false; // ランダム：X回転ランダム有効化
                    RandomSERotXToggle = false; // ランダム：X回転初期と終端同期
                    ApplyPresetValue(RandomRotXCount, 1); // ランダム：周期X回転
                    ApplyPresetValue(RandomStartRotXRange, 0f); // ランダム：初期X回転
                    ApplyPresetValue(RandomEndRotXRange, 0f); // ランダム：終端X回転

                    RandomRotYToggle = false; // ランダム：Y回転ランダム有効化
                    RandomSERotYToggle = false; // ランダム：Y回転初期と終端同期
                    ApplyPresetValue(RandomRotYCount, 1); // ランダム：周期Y回転
                    ApplyPresetValue(RandomStartRotYRange, 0f); // ランダム：初期Y回転
                    ApplyPresetValue(RandomEndRotYRange, 0f); // ランダム：終端Y回転

                    RandomRotZToggle = false; // ランダム：Z回転ランダム有効化
                    RandomSERotZToggle = false; // ランダム：Z回転初期と終端同期
                    ApplyPresetValue(RandomRotZCount, 1); // ランダム：周期Z回転
                    ApplyPresetValue(RandomStartRotZRange, 0f); // ランダム：初期Z回転
                    ApplyPresetValue(RandomEndRotZRange, 0f); // ランダム：終端Z回転

                    RandomOpacityToggle = false; // ランダム：不透明度ランダム有効化
                    RandomSEOpacityToggle = false; // ランダム：不透明度初期と終端同期
                    ApplyPresetValue(RandomOpacityCount, 1); // ランダム：周期不透明度
                    ApplyPresetValue(RandomStartOpacityRange, 0f); // ランダム：初期不透明度
                    ApplyPresetValue(RandomEndOpacityRange, 100f); // ランダム：終端不透明度

                    ApplyPresetValue(RandomSeed, 1); // ランダム：シード
                    ApplyPresetValue(CurveRange, 0f); // ランダム：ブレ幅
                    CurveToggle = false; // ランダム：ブレ有効化

                    ApplyPresetValue(AirResistance, 0f); // 抵抗：抵抗

                    ApplyPresetValue(GravityX, 0f); // 重力：重力X
                    ApplyPresetValue(GravityY, 800f); // 重力：重力Y
                    ApplyPresetValue(GravityZ, 0f); // 重力：重力Z
                    GrTerminationToggle = true; // 重力：終端値に到着

                    StartColor = Colors.Yellow; // 色：初期色
                    EndColor = Colors.Orange; // 色：終端色

                    RandomColorToggle = false; // ランダム色：色ランダム有効化
                    ApplyPresetValue(RandomColorCount, 1); // ランダム色：周期色
                    ApplyPresetValue(RandomHueRange, 0f); // ランダム色：ランダム色相(H)
                    ApplyPresetValue(RandomSatRange, 0f); // ランダム色：ランダム彩度(S)
                    ApplyPresetValue(RandomLumRange, 0f); // ランダム色：ランダム輝度(L)

                    FocusToggle = false; // ピント：ピント機能有効化
                    FocusFadeToggle = false; // ピント：ピントの不透明度
                    ApplyPresetValue(FocusDepth, 0f); // ピント：深度
                    ApplyPresetValue(FocusRange, 0f); // ピント：範囲
                    ApplyPresetValue(FocusMaxBlur, 0f); // ピント：ぼかしの最大値
                    ApplyPresetValue(FocusFallOffBlur, 500f); // ピント：ぼかしの減衰距離
                    ApplyPresetValue(FocusFadeMinOpacity, 0.5f); // ピント：不透明度の最小値

                    TrailToggle = false; // 残像 : 残像有効化
                    ApplyPresetValue(TrailCount, 10); // 残像 : 残像の数
                    ApplyPresetValue(TrailInterval, 0.005f); // 残像 : 残像の間隔
                    ApplyPresetValue(TrailFade, 0.5f); // 残像 : 残像の減衰率
                    ApplyPresetValue(TrailScale, 100f); // 残像 : 残像の拡大率
                    break;
                case PresetType.Confetti:
                    ApplyPresetValue(Count, 200); // パーティクル：最大個数
                    ApplyPresetValue(CycleTime, 5.00f); // パーティクル：生成フレーム周期
                    ApplyPresetValue(TravelTime, 400.0f); // パーティクル：個体移動フレーム
                    FixedTrajectory = true; // パーティクル：軌道固定
                    LoopToggle = false; // パーティクル：パーティクルをループ

                    ApplyPresetValue(ReverseDraw, 0); // 描画：描画順反転
                    FixedDraw = false; // 描画：描画順固定
                    ZSortToggle = false; // 描画：旧Zソートを使用
                    BillboardDraw = false; // 描画：Y軸ビルボード化
                    BillboardXYZDraw = false; // 描画：XYZ軸ビルボード化
                    AutoOrient = false; // 描画：進行方向を向く
                    AutoOrient2D = false; // 描画：進行方向2D化
                    CalculationType = MovementCalculationType.EndPosition; // 描画：処理方法（Enum）

                    FloorToggle = false; // 床：床有効化
                    FloorJudgementType = FloorVisibilityType.abovefloor; // 床：床判定（Enum）
                    FloorActionType = FloorType.Glue; // 床：床動作（Enum）
                    ApplyPresetValue(FloorY, 0f); // 床：床座標Y
                    ApplyPresetValue(FloorWaitTime, 0f); // 床：消失開始間時間
                    ApplyPresetValue(FloorFadeTime, 0f); // 床：消失開始終了間時間
                    ApplyPresetValue(BounceEnergyLoss, 0.1f); // 床：エネルギー損失
                    ApplyPresetValue(BounceFactor, 0.5f); // 床：反発係数
                    ApplyPresetValue(BounceGravity, 100f); // 床：反射時重力
                    ApplyPresetValue(BounceCount, 10); // 床：最大反射回数

                    ApplyPresetValue(DelayTime, 0f); // 遅延：遅延

                    ApplyPresetValue(StartX, 0f); // 座標：初期X
                    ApplyPresetValue(StartY, -800f); // 座標：初期Y
                    ApplyPresetValue(StartZ, 0f); // 座標：初期Z
                    ApplyPresetValue(EndX, 0f); // 座標：終端X
                    ApplyPresetValue(EndY, 800f); // 座標：終端Y
                    ApplyPresetValue(EndZ, 0f); // 座標：終端Z
                    PSEToggleX = false; // 座標：初期Xと終端Xを同期
                    PSEToggleY = false; // 座標：初期Yと終端Yを同期
                    PSEToggleZ = false; // 座標：初期Zと終端Zを同期

                    ApplyPresetValue(ForcePitch, 0f); // 力：発射方向X
                    ApplyPresetValue(ForceYaw, 0f); // 力：発射方向Y
                    ApplyPresetValue(ForceRoll, 0f); // 力：発射方向Z
                    ApplyPresetValue(ForceVelocity, 0f); // 力：発射速度
                    ApplyPresetValue(ForceRandomCount, 1); // 力：ランダム力周期
                    ApplyPresetValue(ForceRandomPitch, 0f); // 力：ランダム発射方向X
                    ApplyPresetValue(ForceRandomYaw, 0f); // 力：ランダム発射方向Y
                    ApplyPresetValue(ForceRandomRoll, 0f); // 力：ランダム発射方向Z
                    ApplyPresetValue(ForceRandomVelocity, 0f); // 力：ランダム発射速度

                    ApplyPresetValue(ScaleStartX, 100f); // 拡大率：初期X拡大率
                    ApplyPresetValue(ScaleStartY, 100f); // 拡大率：初期Y拡大率
                    ApplyPresetValue(ScaleStartZ, 100f); // 拡大率：初期Z拡大率
                    ApplyPresetValue(ScaleX, 100f); // 拡大率：終端X拡大率
                    ApplyPresetValue(ScaleY, 100f); // 拡大率：終端Y拡大率
                    ApplyPresetValue(ScaleZ, 100f); // 拡大率：終端Z拡大率

                    ApplyPresetValue(StartOpacity, 100f); // 透明度：初期不透明度
                    ApplyPresetValue(EndOpacity, 100f); // 透明度：終端不透明度
                    OpacityMapToggle = false; // 透明度：不透明度マップ有効化
                    ApplyPresetValue(OpacityMapMidPoint, 100f); // 透明度：中間不透明度
                    ApplyPresetValue(OpacityMapEase, 0.7f); // 透明度：不透明度マップイーズ

                    ApplyPresetValue(StartRotationX, 0f); // 角度：初期X回転
                    ApplyPresetValue(StartRotationY, 0f); // 角度：初期Y回転
                    ApplyPresetValue(StartRotationZ, 0f); // 角度：初期Z回転
                    ApplyPresetValue(EndRotationX, 0f); // 角度：終端X回転
                    ApplyPresetValue(EndRotationY, 0f); // 角度：終端Y回転
                    ApplyPresetValue(EndRotationZ, 0f); // 角度：終端Z回転

                    RandomToggleX = true; // ランダム：X座標ランダム有効化
                    RandomSEToggleX = true; // ランダム：X初期とX終端を同期
                    ApplyPresetValue(RandomXCount, 1); // ランダム：周期X座標
                    ApplyPresetValue(RandomStartXRange, 2000f); // ランダム：初期X座標範囲
                    ApplyPresetValue(RandomEndXRange, 400f); // ランダム：終端X座標範囲

                    RandomToggleY = false; // ランダム：Y座標ランダム有効化
                    RandomSEToggleY = false; // ランダム：Y初期とY終端を同期
                    ApplyPresetValue(RandomYCount, 1); // ランダム：周期Y座標
                    ApplyPresetValue(RandomStartYRange, 0f); // ランダム：初期Y座標範囲
                    ApplyPresetValue(RandomEndYRange, 0f); // ランダム：終端Y座標範囲

                    RandomToggleZ = true; // ランダム：Z座標ランダム有効化
                    RandomSEToggleZ = true; // ランダム：Z初期とZ終端を同期
                    ApplyPresetValue(RandomZCount, 1); // ランダム：周期座標Z
                    ApplyPresetValue(RandomStartZRange, 1000f); // ランダム：初期Z座標範囲
                    ApplyPresetValue(RandomEndZRange, 200f); // ランダム：終端Z座標範囲

                    RandomScaleToggle = false; // ランダム：拡大率ランダム有効化
                    RandomSEScaleToggle = false; // ランダム：拡大率初期と終端同期
                    RandomSyScaleToggle = false; // ランダム：拡大率X,Y,Z同期
                    ApplyPresetValue(RandomScaleCount, 1); // ランダム：周期拡大率
                    ApplyPresetValue(RandomStartScaleRange, 0f); // ランダム：初期拡大率
                    ApplyPresetValue(RandomEndScaleRange, 0f); // ランダム：終端拡大率

                    RandomRotXToggle = true; // ランダム：X回転ランダム有効化
                    RandomSERotXToggle = true; // ランダム：X回転初期と終端同期
                    ApplyPresetValue(RandomRotXCount, 1); // ランダム：周期X回転
                    ApplyPresetValue(RandomStartRotXRange, 360f); // ランダム：初期X回転
                    ApplyPresetValue(RandomEndRotXRange, 360f); // ランダム：終端X回転

                    RandomRotYToggle = true; // ランダム：Y回転ランダム有効化
                    RandomSERotYToggle = true; // ランダム：Y回転初期と終端同期
                    ApplyPresetValue(RandomRotYCount, 1); // ランダム：周期Y回転
                    ApplyPresetValue(RandomStartRotYRange, 360f); // ランダム：初期Y回転
                    ApplyPresetValue(RandomEndRotYRange, 360f); // ランダム：終端Y回転

                    RandomRotZToggle = true; // ランダム：Z回転ランダム有効化
                    RandomSERotZToggle = true; // ランダム：Z回転初期と終端同期
                    ApplyPresetValue(RandomRotZCount, 1); // ランダム：周期Z回転
                    ApplyPresetValue(RandomStartRotZRange, 360f); // ランダム：初期Z回転
                    ApplyPresetValue(RandomEndRotZRange, 360f); // ランダム：終端Z回転

                    RandomOpacityToggle = false; // ランダム：不透明度ランダム有効化
                    RandomSEOpacityToggle = false; // ランダム：不透明度初期と終端同期
                    ApplyPresetValue(RandomOpacityCount, 1); // ランダム：周期不透明度
                    ApplyPresetValue(RandomStartOpacityRange, 0f); // ランダム：初期不透明度
                    ApplyPresetValue(RandomEndOpacityRange, 100f); // ランダム：終端不透明度

                    ApplyPresetValue(RandomSeed, 1); // ランダム：シード
                    ApplyPresetValue(CurveRange, 0f); // ランダム：ブレ幅
                    CurveToggle = false; // ランダム：ブレ有効化

                    ApplyPresetValue(AirResistance, 0f); // 抵抗：抵抗

                    SetRandomMovePreset(GravityX, 800, -800, 0); // 重力：重力X
                    ApplyPresetValue(GravityY, -400f); // 重力：重力Y
                    SetRandomMovePreset(GravityZ, 800, -800, 0); // 重力：重力Z
                    GrTerminationToggle = true; // 重力：終端値に到着

                    StartColor = Colors.White; // 色：初期色
                    EndColor = Colors.White; // 色：終端色

                    RandomColorToggle = true; // ランダム色：色ランダム有効化
                    ApplyPresetValue(RandomColorCount, 1); // ランダム色：周期色
                    ApplyPresetValue(RandomHueRange, 360f); // ランダム色：ランダム色相(H)
                    ApplyPresetValue(RandomSatRange, 0f); // ランダム色：ランダム彩度(S)
                    ApplyPresetValue(RandomLumRange, 0f); // ランダム色：ランダム輝度(L)

                    FocusToggle = false; // ピント：ピント機能有効化
                    FocusFadeToggle = false; // ピント：ピントの不透明度
                    ApplyPresetValue(FocusDepth, 0f); // ピント：深度
                    ApplyPresetValue(FocusRange, 0f); // ピント：範囲
                    ApplyPresetValue(FocusMaxBlur, 0f); // ピント：ぼかしの最大値
                    ApplyPresetValue(FocusFallOffBlur, 500f); // ピント：ぼかしの減衰距離
                    ApplyPresetValue(FocusFadeMinOpacity, 0.5f); // ピント：不透明度の最小値

                    TrailToggle = false; // 残像 : 残像有効化
                    ApplyPresetValue(TrailCount, 10); // 残像 : 残像の数
                    ApplyPresetValue(TrailInterval, 0.005f); // 残像 : 残像の間隔
                    ApplyPresetValue(TrailFade, 0.5f); // 残像 : 残像の減衰率
                    ApplyPresetValue(TrailScale, 100f); // 残像 : 残像の拡大率
                    break;
                case PresetType.Steam:
                    ApplyPresetValue(Count, 400); // パーティクル：最大個数
                    ApplyPresetValue(CycleTime, 1.00f); // パーティクル：生成フレーム周期
                    ApplyPresetValue(TravelTime, 400.0f); // パーティクル：個体移動フレーム
                    FixedTrajectory = true; // パーティクル：軌道固定
                    LoopToggle = false; // パーティクル：パーティクルをループ

                    ApplyPresetValue(ReverseDraw, 0); // 描画：描画順反転
                    FixedDraw = false; // 描画：描画順固定
                    ZSortToggle = false; // 描画：旧Zソートを使用
                    BillboardDraw = false; // 描画：Y軸ビルボード化
                    BillboardXYZDraw = true; // 描画：XYZ軸ビルボード化
                    AutoOrient = false; // 描画：進行方向を向く
                    AutoOrient2D = false; // 描画：進行方向2D化
                    CalculationType = MovementCalculationType.EndPosition; // 描画：処理方法（Enum）

                    FloorToggle = false; // 床：床有効化
                    FloorJudgementType = FloorVisibilityType.abovefloor; // 床：床判定（Enum）
                    FloorActionType = FloorType.Glue; // 床：床動作（Enum）
                    ApplyPresetValue(FloorY, 0f); // 床：床座標Y
                    ApplyPresetValue(FloorWaitTime, 0f); // 床：消失開始間時間
                    ApplyPresetValue(FloorFadeTime, 0f); // 床：消失開始終了間時間
                    ApplyPresetValue(BounceEnergyLoss, 0.1f); // 床：エネルギー損失
                    ApplyPresetValue(BounceFactor, 0.5f); // 床：反発係数
                    ApplyPresetValue(BounceGravity, 100f); // 床：反射時重力
                    ApplyPresetValue(BounceCount, 10); // 床：最大反射回数

                    ApplyPresetValue(DelayTime, 0f); // 遅延：遅延

                    ApplyPresetValue(StartX, 0f); // 座標：初期X
                    ApplyPresetValue(StartY, 500f); // 座標：初期Y
                    ApplyPresetValue(StartZ, 0f); // 座標：初期Z
                    SetRandomMovePreset(EndX, -200f, 200f, 0.18f); // 座標：終端X
                    ApplyPresetValue(EndY, -500f); // 座標：終端Y
                    SetRandomMovePreset(EndZ, -200f, 200f, 0.29f); // 座標：終端Z
                    PSEToggleX = false; // 座標：初期Xと終端Xを同期
                    PSEToggleY = false; // 座標：初期Yと終端Yを同期
                    PSEToggleZ = false; // 座標：初期Zと終端Zを同期

                    ApplyPresetValue(ForcePitch, 0f); // 力：発射方向X
                    ApplyPresetValue(ForceYaw, 0f); // 力：発射方向Y
                    ApplyPresetValue(ForceRoll, 0f); // 力：発射方向Z
                    ApplyPresetValue(ForceVelocity, 0f); // 力：発射速度
                    ApplyPresetValue(ForceRandomCount, 1); // 力：ランダム力周期
                    ApplyPresetValue(ForceRandomPitch, 0f); // 力：ランダム発射方向X
                    ApplyPresetValue(ForceRandomYaw, 0f); // 力：ランダム発射方向Y
                    ApplyPresetValue(ForceRandomRoll, 0f); // 力：ランダム発射方向Z
                    ApplyPresetValue(ForceRandomVelocity, 0f); // 力：ランダム発射速度

                    ApplyPresetValue(ScaleStartX, 100f); // 拡大率：初期X拡大率
                    ApplyPresetValue(ScaleStartY, 100f); // 拡大率：初期Y拡大率
                    ApplyPresetValue(ScaleStartZ, 100f); // 拡大率：初期Z拡大率
                    ApplyPresetValue(ScaleX, 100f); // 拡大率：終端X拡大率
                    ApplyPresetValue(ScaleY, 100f); // 拡大率：終端Y拡大率
                    ApplyPresetValue(ScaleZ, 100f); // 拡大率：終端Z拡大率

                    ApplyPresetValue(StartOpacity, 50f); // 透明度：初期不透明度
                    ApplyPresetValue(EndOpacity, 0); // 透明度：終端不透明度
                    OpacityMapToggle = false; // 透明度：不透明度マップ有効化
                    ApplyPresetValue(OpacityMapMidPoint, 100f); // 透明度：中間不透明度
                    ApplyPresetValue(OpacityMapEase, 0.7f); // 透明度：不透明度マップイーズ

                    ApplyPresetValue(StartRotationX, 0f); // 角度：初期X回転
                    ApplyPresetValue(StartRotationY, 0f); // 角度：初期Y回転
                    ApplyPresetValue(StartRotationZ, 0f); // 角度：初期Z回転
                    ApplyPresetValue(EndRotationX, 0f); // 角度：終端X回転
                    ApplyPresetValue(EndRotationY, 0f); // 角度：終端Y回転
                    ApplyPresetValue(EndRotationZ, 0f); // 角度：終端Z回転

                    RandomToggleX = true; // ランダム：X座標ランダム有効化
                    RandomSEToggleX = true; // ランダム：X初期とX終端を同期
                    ApplyPresetValue(RandomXCount, 1); // ランダム：周期X座標
                    ApplyPresetValue(RandomStartXRange, 100f); // ランダム：初期X座標範囲
                    ApplyPresetValue(RandomEndXRange, 0f); // ランダム：終端X座標範囲

                    RandomToggleY = false; // ランダム：Y座標ランダム有効化
                    RandomSEToggleY = false; // ランダム：Y初期とY終端を同期
                    ApplyPresetValue(RandomYCount, 1); // ランダム：周期Y座標
                    ApplyPresetValue(RandomStartYRange, 0f); // ランダム：初期Y座標範囲
                    ApplyPresetValue(RandomEndYRange, 0f); // ランダム：終端Y座標範囲

                    RandomToggleZ = true; // ランダム：Z座標ランダム有効化
                    RandomSEToggleZ = true; // ランダム：Z初期とZ終端を同期
                    ApplyPresetValue(RandomZCount, 1); // ランダム：周期座標Z
                    ApplyPresetValue(RandomStartZRange, 100f); // ランダム：初期Z座標範囲
                    ApplyPresetValue(RandomEndZRange, 0f); // ランダム：終端Z座標範囲

                    RandomScaleToggle = false; // ランダム：拡大率ランダム有効化
                    RandomSEScaleToggle = false; // ランダム：拡大率初期と終端同期
                    RandomSyScaleToggle = false; // ランダム：拡大率X,Y,Z同期
                    ApplyPresetValue(RandomScaleCount, 1); // ランダム：周期拡大率
                    ApplyPresetValue(RandomStartScaleRange, 0f); // ランダム：初期拡大率
                    ApplyPresetValue(RandomEndScaleRange, 0f); // ランダム：終端拡大率

                    RandomRotXToggle = false; // ランダム：X回転ランダム有効化
                    RandomSERotXToggle = false; // ランダム：X回転初期と終端同期
                    ApplyPresetValue(RandomRotXCount, 1); // ランダム：周期X回転
                    ApplyPresetValue(RandomStartRotXRange, 0f); // ランダム：初期X回転
                    ApplyPresetValue(RandomEndRotXRange, 0f); // ランダム：終端X回転

                    RandomRotYToggle = false; // ランダム：Y回転ランダム有効化
                    RandomSERotYToggle = false; // ランダム：Y回転初期と終端同期
                    ApplyPresetValue(RandomRotYCount, 1); // ランダム：周期Y回転
                    ApplyPresetValue(RandomStartRotYRange, 0f); // ランダム：初期Y回転
                    ApplyPresetValue(RandomEndRotYRange, 0f); // ランダム：終端Y回転

                    RandomRotZToggle = false; // ランダム：Z回転ランダム有効化
                    RandomSERotZToggle = false; // ランダム：Z回転初期と終端同期
                    ApplyPresetValue(RandomRotZCount, 1); // ランダム：周期Z回転
                    ApplyPresetValue(RandomStartRotZRange, 0f); // ランダム：初期Z回転
                    ApplyPresetValue(RandomEndRotZRange, 0f); // ランダム：終端Z回転

                    RandomOpacityToggle = false; // ランダム：不透明度ランダム有効化
                    RandomSEOpacityToggle = false; // ランダム：不透明度初期と終端同期
                    ApplyPresetValue(RandomOpacityCount, 1); // ランダム：周期不透明度
                    ApplyPresetValue(RandomStartOpacityRange, 0f); // ランダム：初期不透明度
                    ApplyPresetValue(RandomEndOpacityRange, 100f); // ランダム：終端不透明度

                    ApplyPresetValue(RandomSeed, 1); // ランダム：シード
                    ApplyPresetValue(CurveRange, 0f); // ランダム：ブレ幅
                    CurveToggle = false; // ランダム：ブレ有効化

                    ApplyPresetValue(AirResistance, 0.05f); // 抵抗：抵抗

                    SetRandomMovePreset(GravityX, -800f, 800f, 0.13f); // 重力：重力X
                    SetRandomMovePreset(GravityY, -600f, 1000f, 0.22f); // 重力：重力Y
                    SetRandomMovePreset(GravityZ, -800f, 800f, 0.31f); // 重力：重力Z
                    GrTerminationToggle = true; // 重力：終端値に到着

                    StartColor = Colors.White; // 色：初期色
                    EndColor = Colors.White; // 色：終端色

                    RandomColorToggle = false; // ランダム色：色ランダム有効化
                    ApplyPresetValue(RandomColorCount, 1); // ランダム色：周期色
                    ApplyPresetValue(RandomHueRange, 0f); // ランダム色：ランダム色相(H)
                    ApplyPresetValue(RandomSatRange, 0f); // ランダム色：ランダム彩度(S)
                    ApplyPresetValue(RandomLumRange, 0f); // ランダム色：ランダム輝度(L)

                    FocusToggle = false; // ピント：ピント機能有効化
                    FocusFadeToggle = false; // ピント：ピントの不透明度
                    ApplyPresetValue(FocusDepth, 0f); // ピント：深度
                    ApplyPresetValue(FocusRange, 0f); // ピント：範囲
                    ApplyPresetValue(FocusMaxBlur, 0f); // ピント：ぼかしの最大値
                    ApplyPresetValue(FocusFallOffBlur, 500f); // ピント：ぼかしの減衰距離
                    ApplyPresetValue(FocusFadeMinOpacity, 0.5f); // ピント：不透明度の最小値

                    TrailToggle = false; // 残像 : 残像有効化
                    ApplyPresetValue(TrailCount, 10); // 残像 : 残像の数
                    ApplyPresetValue(TrailInterval, 0.005f); // 残像 : 残像の間隔
                    ApplyPresetValue(TrailFade, 0.5f); // 残像 : 残像の減衰率
                    ApplyPresetValue(TrailScale, 100f); // 残像 : 残像の拡大率
                    break;
                case PresetType.Custom:
                    break;
                case PresetType.Default:
                    ApplyPresetValue(Count, 1); // パーティクル：最大個数
                    ApplyPresetValue(CycleTime, 1.00f); // パーティクル：生成フレーム周期
                    ApplyPresetValue(TravelTime, 1.0f); // パーティクル：個体移動フレーム
                    FixedTrajectory = false; // パーティクル：軌道固定
                    LoopToggle = false; // パーティクル：パーティクルをループ

                    ApplyPresetValue(ReverseDraw, 0); // 描画：描画順反転
                    FixedDraw = false; // 描画：描画順固定
                    ZSortToggle = false; // 描画：旧Zソートを使用
                    BillboardDraw = false; // 描画：Y軸ビルボード化
                    BillboardXYZDraw = false; // 描画：XYZ軸ビルボード化
                    AutoOrient = false; // 描画：進行方向を向く
                    AutoOrient2D = false; // 描画：進行方向2D化
                    CalculationType = MovementCalculationType.EndPosition; // 描画：処理方法（Enum）

                    FloorToggle = false; // 床：床有効化
                    FloorJudgementType = FloorVisibilityType.abovefloor; // 床：床判定（Enum）
                    FloorActionType = FloorType.Glue; // 床：床動作（Enum）
                    ApplyPresetValue(FloorY, 0f); // 床：床座標Y
                    ApplyPresetValue(FloorWaitTime, 0f); // 床：消失開始間時間
                    ApplyPresetValue(FloorFadeTime, 0f); // 床：消失開始終了間時間
                    ApplyPresetValue(BounceEnergyLoss, 0.1f); // 床：エネルギー損失
                    ApplyPresetValue(BounceFactor, 0.5f); // 床：反発係数
                    ApplyPresetValue(BounceGravity, 100f); // 床：反射時重力
                    ApplyPresetValue(BounceCount, 10); // 床：最大反射回数

                    ApplyPresetValue(DelayTime, 0f); // 遅延：遅延

                    ApplyPresetValue(StartX, 0f); // 座標：初期X
                    ApplyPresetValue(StartY, 0f); // 座標：初期Y
                    ApplyPresetValue(StartZ, 0f); // 座標：初期Z
                    ApplyPresetValue(EndX, 0f); // 座標：終端X
                    ApplyPresetValue(EndY, 0f); // 座標：終端Y
                    ApplyPresetValue(EndZ, 0f); // 座標：終端Z
                    PSEToggleX = false; // 座標：初期Xと終端Xを同期
                    PSEToggleY = false; // 座標：初期Yと終端Yを同期
                    PSEToggleZ = false; // 座標：初期Zと終端Zを同期

                    ApplyPresetValue(ForcePitch, 0f); // 力：発射方向X
                    ApplyPresetValue(ForceYaw, 0f); // 力：発射方向Y
                    ApplyPresetValue(ForceRoll, 0f); // 力：発射方向Z
                    ApplyPresetValue(ForceVelocity, 0f); // 力：発射速度
                    ApplyPresetValue(ForceRandomCount, 1); // 力：ランダム力周期
                    ApplyPresetValue(ForceRandomPitch, 0f); // 力：ランダム発射方向X
                    ApplyPresetValue(ForceRandomYaw, 0f); // 力：ランダム発射方向Y
                    ApplyPresetValue(ForceRandomRoll, 0f); // 力：ランダム発射方向Z
                    ApplyPresetValue(ForceRandomVelocity, 0f); // 力：ランダム発射速度

                    ApplyPresetValue(ScaleStartX, 100f); // 拡大率：初期X拡大率
                    ApplyPresetValue(ScaleStartY, 100f); // 拡大率：初期Y拡大率
                    ApplyPresetValue(ScaleStartZ, 100f); // 拡大率：初期Z拡大率
                    ApplyPresetValue(ScaleX, 100f); // 拡大率：終端X拡大率
                    ApplyPresetValue(ScaleY, 100f); // 拡大率：終端Y拡大率
                    ApplyPresetValue(ScaleZ, 100f); // 拡大率：終端Z拡大率

                    ApplyPresetValue(StartOpacity, 100f); // 透明度：初期不透明度
                    ApplyPresetValue(EndOpacity, 100f); // 透明度：終端不透明度
                    OpacityMapToggle = false; // 透明度：不透明度マップ有効化
                    ApplyPresetValue(OpacityMapMidPoint, 100f); // 透明度：中間不透明度
                    ApplyPresetValue(OpacityMapEase, 0.7f); // 透明度：不透明度マップイーズ

                    ApplyPresetValue(StartRotationX, 0f); // 角度：初期X回転
                    ApplyPresetValue(StartRotationY, 0f); // 角度：初期Y回転
                    ApplyPresetValue(StartRotationZ, 0f); // 角度：初期Z回転
                    ApplyPresetValue(EndRotationX, 0f); // 角度：終端X回転
                    ApplyPresetValue(EndRotationY, 0f); // 角度：終端Y回転
                    ApplyPresetValue(EndRotationZ, 0f); // 角度：終端Z回転

                    RandomToggleX = false; // ランダム：X座標ランダム有効化
                    RandomSEToggleX = false; // ランダム：X初期とX終端を同期
                    ApplyPresetValue(RandomXCount, 1); // ランダム：周期X座標
                    ApplyPresetValue(RandomStartXRange, 0f); // ランダム：初期X座標範囲
                    ApplyPresetValue(RandomEndXRange, 0f); // ランダム：終端X座標範囲

                    RandomToggleY = false; // ランダム：Y座標ランダム有効化
                    RandomSEToggleY = false; // ランダム：Y初期とY終端を同期
                    ApplyPresetValue(RandomYCount, 1); // ランダム：周期Y座標
                    ApplyPresetValue(RandomStartYRange, 0f); // ランダム：初期Y座標範囲
                    ApplyPresetValue(RandomEndYRange, 0f); // ランダム：終端Y座標範囲

                    RandomToggleZ = false; // ランダム：Z座標ランダム有効化
                    RandomSEToggleZ = false; // ランダム：Z初期とZ終端を同期
                    ApplyPresetValue(RandomZCount, 1); // ランダム：周期座標Z
                    ApplyPresetValue(RandomStartZRange, 0f); // ランダム：初期Z座標範囲
                    ApplyPresetValue(RandomEndZRange, 0f); // ランダム：終端Z座標範囲

                    RandomScaleToggle = false; // ランダム：拡大率ランダム有効化
                    RandomSEScaleToggle = false; // ランダム：拡大率初期と終端同期
                    RandomSyScaleToggle = false; // ランダム：拡大率X,Y,Z同期
                    ApplyPresetValue(RandomScaleCount, 1); // ランダム：周期拡大率
                    ApplyPresetValue(RandomStartScaleRange, 0f); // ランダム：初期拡大率
                    ApplyPresetValue(RandomEndScaleRange, 0f); // ランダム：終端拡大率

                    RandomRotXToggle = false; // ランダム：X回転ランダム有効化
                    RandomSERotXToggle = false; // ランダム：X回転初期と終端同期
                    ApplyPresetValue(RandomRotXCount, 1); // ランダム：周期X回転
                    ApplyPresetValue(RandomStartRotXRange, 0f); // ランダム：初期X回転
                    ApplyPresetValue(RandomEndRotXRange, 0f); // ランダム：終端X回転

                    RandomRotYToggle = false; // ランダム：Y回転ランダム有効化
                    RandomSERotYToggle = false; // ランダム：Y回転初期と終端同期
                    ApplyPresetValue(RandomRotYCount, 1); // ランダム：周期Y回転
                    ApplyPresetValue(RandomStartRotYRange, 0f); // ランダム：初期Y回転
                    ApplyPresetValue(RandomEndRotYRange, 0f); // ランダム：終端Y回転

                    RandomRotZToggle = false; // ランダム：Z回転ランダム有効化
                    RandomSERotZToggle = false; // ランダム：Z回転初期と終端同期
                    ApplyPresetValue(RandomRotZCount, 1); // ランダム：周期Z回転
                    ApplyPresetValue(RandomStartRotZRange, 0f); // ランダム：初期Z回転
                    ApplyPresetValue(RandomEndRotZRange, 0f); // ランダム：終端Z回転

                    RandomOpacityToggle = false; // ランダム：不透明度ランダム有効化
                    RandomSEOpacityToggle = false; // ランダム：不透明度初期と終端同期
                    ApplyPresetValue(RandomOpacityCount, 1); // ランダム：周期不透明度
                    ApplyPresetValue(RandomStartOpacityRange, 0f); // ランダム：初期不透明度
                    ApplyPresetValue(RandomEndOpacityRange, 100f); // ランダム：終端不透明度

                    ApplyPresetValue(RandomSeed, 1); // ランダム：シード
                    ApplyPresetValue(CurveRange, 0f); // ランダム：ブレ幅
                    CurveToggle = false; // ランダム：ブレ有効化

                    ApplyPresetValue(AirResistance, 0f); // 抵抗：抵抗

                    ApplyPresetValue(GravityX, 0f); // 重力：重力X
                    ApplyPresetValue(GravityY, 0f); // 重力：重力Y
                    ApplyPresetValue(GravityZ, 0f); // 重力：重力Z
                    GrTerminationToggle = true; // 重力：終端値に到着

                    StartColor = Colors.White; // 色：初期色
                    EndColor = Colors.White; // 色：終端色

                    RandomColorToggle = false; // ランダム色：色ランダム有効化
                    ApplyPresetValue(RandomColorCount, 1); // ランダム色：周期色
                    ApplyPresetValue(RandomHueRange, 0f); // ランダム色：ランダム色相(H)
                    ApplyPresetValue(RandomSatRange, 0f); // ランダム色：ランダム彩度(S)
                    ApplyPresetValue(RandomLumRange, 0f); // ランダム色：ランダム輝度(L)

                    FocusToggle = false; // ピント：ピント機能有効化
                    FocusFadeToggle = false; // ピント：ピントの不透明度
                    ApplyPresetValue(FocusDepth, 0f); // ピント：深度
                    ApplyPresetValue(FocusRange, 0f); // ピント：範囲
                    ApplyPresetValue(FocusMaxBlur, 0f); // ピント：ぼかしの最大値
                    ApplyPresetValue(FocusFallOffBlur, 500f); // ピント：ぼかしの減衰距離
                    ApplyPresetValue(FocusFadeMinOpacity, 0.5f); // ピント：不透明度の最小値

                    TrailToggle = false; // 残像 : 残像有効化
                    ApplyPresetValue(TrailCount, 10); // 残像 : 残像の数
                    ApplyPresetValue(TrailInterval, 0.005f); // 残像 : 残像の間隔
                    ApplyPresetValue(TrailFade, 0.5f); // 残像 : 残像の減衰率
                    ApplyPresetValue(TrailScale, 100f); // 残像 : 残像の拡大率
                    break;
            }
        }
        static void ApplyPresetValue(Animation anim, double value)
        {
            var clone = new Animation(value, anim.MinValue, anim.MaxValue, anim.Loop);

            if (anim.AnimationType.IsKeyFrameSupported() && anim.KeyFrames != null)
            {
                clone.SetKeyFrames(anim.KeyFrames);
            }

            anim.CopyFrom(clone);
        }

        static void SetRandomMovePreset(Animation anim, double minValue, double maxValue, double span)
        {
            anim.From = minValue;
            anim.To = maxValue;
            anim.AnimationType = AnimationType.ランダム移動;
            anim.Span = span;
            // Delayで少し待ってからTo値を設定しないと、UI(Value[1]の値)が更新されない。まじで何で？わかる人教えてください！！
            // あと普通にanim.FromとかToとか以外のやり方がわからん。助けてください。任意.Values.SetItem()とかじゃないんですか？？？？？
            System.Threading.Tasks.Task.Delay(500).ContinueWith(_ => anim.To = maxValue);
        }
    }
}

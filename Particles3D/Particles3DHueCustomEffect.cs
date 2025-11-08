using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace Particles3D
{
    internal class Particles3DHueCustomEffect : D2D1CustomShaderEffectBase
    {
        // Factor のみ残します
        public float Factor { set => SetValue((int)EffectImpl.Properties.Factor, value); }

        // ★ HSLの単一シフト値を設定する新しいメソッドとプロパティ
        // HLSLの cbuffer に直接対応するフィールドに値を設定します
        public float HueShift { set => SetValue((int)EffectImpl.Properties.HueShift, value); }
        public float SaturationFactor { set => SetValue((int)EffectImpl.Properties.SaturationFactor, value); }
        public float LuminanceFactor { set => SetValue((int)EffectImpl.Properties.LuminanceFactor, value); }


        public Particles3DHueCustomEffect(IGraphicsDevicesAndContext devices) : base(Create<EffectImpl>(devices)) { }

        // ShaderPoint は不要になりましたが、Vector4として扱うため削除してもOK。

        [StructLayout(LayoutKind.Sequential)]
        struct ConstantBuffer
        {
            // ★ HLSLの新しい cbuffer に完全に一致させる必要があります
            public float HueShift;
            public float SaturationFactor;
            public float LuminanceFactor;
            public float Factor; // float4 の倍数にするため、float4 * 1 のサイズで定義
        }

        [CustomEffect(1)]
        private class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer constants;
            protected override void UpdateConstants()
            {
                if (drawInformation is not null)
                {
                    // 非常に軽量な ConstantBuffer を転送します
                    drawInformation.SetPixelShaderConstantBuffer(constants);
                }
            }
            private static byte[] LoadShader()
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("Particles3D.Shaders.Particles3DHueShader.cso");
                if (stream is null)
                {
                    MessageBox.Show("シェーダーリソース 'Particles3D.Shaders.Particles3DHueShader.cso' が見つかりません。", "シェーダーエラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    throw new FileNotFoundException("Shader resource not found.");
                }
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            }

            public EffectImpl() : base(LoadShader()) => constants = new ConstantBuffer();

            public enum Properties
            {
                //プロパティ名
                HueShift, SaturationFactor, LuminanceFactor, Factor
            }

            //プロパティ定義
            [CustomEffectProperty(PropertyType.Float, (int)Properties.HueShift)]
            public float HueShift { get => constants.HueShift; set { constants.HueShift = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.SaturationFactor)]
            public float SaturationFactor { get => constants.SaturationFactor; set { constants.SaturationFactor = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.LuminanceFactor)]
            public float LuminanceFactor { get => constants.LuminanceFactor; set { constants.LuminanceFactor = value; UpdateConstants(); } }
            // Factor は残します
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Factor)]
            public float Factor { get => constants.Factor; set { constants.Factor = value; UpdateConstants(); } }

        }
    }
}
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DuneMaterials.Rendering
{
    // Applied once to the shared artwork scene; text and icons never enter this pass.
    internal sealed class MaterialColorEffect : ShaderEffect
    {
        static readonly PixelShader shader = LoadShader();
        public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty(
            "Input", typeof(MaterialColorEffect), 0);
        public Brush Input
        {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }
        static PixelShader LoadShader()
        {
            var result = new PixelShader();
            using (var stream = typeof(MaterialColorEffect).Assembly.GetManifestResourceStream("DuneMaterials.MaterialColor.ps"))
            {
                if (stream == null) throw new InvalidOperationException("Material color shader is missing.");
                result.SetStreamSource(stream);
            }
            result.Freeze(); return result;
        }
        public MaterialColorEffect()
        {
            PixelShader = shader;
            UpdateShaderValue(InputProperty);
        }
    }
}

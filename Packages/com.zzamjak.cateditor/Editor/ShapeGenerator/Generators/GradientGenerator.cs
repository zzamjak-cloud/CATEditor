using UnityEngine;
using UnityEditor;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 그라디언트 생성기 - Color Gradient (검은색 → 흰색)
    /// </summary>
    public class GradientGenerator : BaseShapeGenerator
    {
        private readonly GradientSettings _settings;

        public override string ShapeName => "Gradient (Color)";

        public GradientGenerator() : this(new GradientSettings()) { }

        public GradientGenerator(GradientSettings settings)
        {
            _settings = settings;
        }

        public override void DrawSettingsGUI()
        {
            _settings.UniformSize = EditorGUILayout.Toggle(
                new GUIContent("Uniform Size", "Width와 Height를 동일하게 유지"),
                _settings.UniformSize);

            if (_settings.UniformSize)
            {
                _settings.Width = EditorGUILayout.IntSlider(
                    new GUIContent("Size", "텍스처 크기 (정사각형)"),
                    _settings.Width, 16, 2048);
                _settings.Height = _settings.Width;
            }
            else
            {
                _settings.Width = EditorGUILayout.IntSlider(
                    new GUIContent("Width", "텍스처 가로 크기"),
                    _settings.Width, 16, 2048);

                _settings.Height = EditorGUILayout.IntSlider(
                    new GUIContent("Height", "텍스처 세로 크기"),
                    _settings.Height, 16, 2048);
            }

            EditorGUILayout.Space(5);

            _settings.Direction = (GradientDirection)EditorGUILayout.EnumPopup(
                new GUIContent("Direction", "그라디언트 방향"),
                _settings.Direction);

            EditorGUILayout.Space(5);

            _settings.CurvePower = EditorGUILayout.Slider(
                new GUIContent("Curve Power", "그라디언트 곡선 가중치 (0.5=선형, <0.5=EaseOut, >0.5=EaseIn)"),
                _settings.CurvePower, 0f, 1f);

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"0.5 = 선형, {_settings.CurvePower:F2} = {GetCurveTypeName()}", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        private string GetCurveTypeName()
        {
            if (Mathf.Approximately(_settings.CurvePower, 0.5f))
                return "선형 (Linear)";
            else if (_settings.CurvePower > 0.5f)
                return "EaseIn (느리게 시작)";
            else
                return "EaseOut (빠르게 시작)";
        }

        /// <summary>
        /// 0~1 범위의 Curve Power를 실제 지수 값으로 변환
        /// 0 → 0.1 (강한 EaseOut)
        /// 0.5 → 1.0 (선형)
        /// 1.0 → 5.0 (강한 EaseIn)
        /// </summary>
        private float ConvertCurvePowerToExponent(float curvePower)
        {
            if (curvePower < 0.5f)
            {
                // 0~0.5 → 0.1~1.0 (EaseOut)
                return Mathf.Lerp(0.1f, 1.0f, curvePower * 2f);
            }
            else
            {
                // 0.5~1.0 → 1.0~5.0 (EaseIn)
                return Mathf.Lerp(1.0f, 5.0f, (curvePower - 0.5f) * 2f);
            }
        }

        public override Vector2Int GetTextureSize()
        {
            return new Vector2Int(_settings.Width, _settings.Height);
        }

        public override string GetFileName()
        {
            string dirName = _settings.Direction == GradientDirection.Horizontal ? "H" : "V";
            string curveSuffix = !Mathf.Approximately(_settings.CurvePower, 0.5f)
                ? $"_C{_settings.CurvePower:F2}"
                : "";

            if (_settings.UniformSize)
            {
                return $"Gradient_Color_{dirName}_{_settings.Width}{curveSuffix}.png";
            }
            else
            {
                return $"Gradient_Color_{dirName}_{_settings.Width}x{_settings.Height}{curveSuffix}.png";
            }
        }

        public override Texture2D Generate()
        {
            int width = _settings.Width;
            int height = _settings.Height;
            var (texture, pixels) = CreateTextureWithPixels(width, height);

            // 0~1 범위를 실제 지수 값으로 변환
            float exponent = ConvertCurvePowerToExponent(_settings.CurvePower);

            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    float t = _settings.Direction == GradientDirection.Horizontal
                        ? (float)x / (width - 1)
                        : (float)y / (height - 1);

                    // Curve Power 적용 (0.5가 아닐 때만)
                    if (!Mathf.Approximately(_settings.CurvePower, 0.5f))
                    {
                        t = Mathf.Pow(t, exponent);
                    }

                    // 검은색(0) → 흰색(255)
                    byte gray = (byte)(t * 255f + 0.5f);
                    pixels[rowOffset + x] = new Color32(gray, gray, gray, 255);
                }
            }

            ApplyPixels(texture, pixels);
            return texture;
        }
    }
}

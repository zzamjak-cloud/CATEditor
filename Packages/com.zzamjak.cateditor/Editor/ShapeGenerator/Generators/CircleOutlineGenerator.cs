using UnityEngine;
using UnityEditor;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 원 외곽선(Circle Outline) 생성기
    /// </summary>
    public class CircleOutlineGenerator : BaseShapeGenerator
    {
        private readonly CircleSettings _settings;

        public override string ShapeName => "Circle Outline";

        public CircleOutlineGenerator() : this(new CircleSettings()) { }

        public CircleOutlineGenerator(CircleSettings settings)
        {
            _settings = settings;
        }

        public override void DrawSettingsGUI()
        {
            _settings.Round = EditorGUILayout.IntSlider(
                new GUIContent("Round", "원의 반지름"),
                _settings.Round, 1, 256);

            int maxWidth = Mathf.Max(1, _settings.Round * 2);
            _settings.Width = EditorGUILayout.IntSlider(
                new GUIContent("Width", "외곽선 두께"),
                _settings.Width, 1, maxWidth);
        }

        public override Vector2Int GetTextureSize()
        {
            int outerWidth = _settings.Width - (_settings.Width / 2);
            int size = (_settings.Round + outerWidth) * 2;
            return new Vector2Int(size, size);
        }

        public override string GetFileName()
        {
            return $"Circle_R{_settings.Round}_Out{_settings.Width}.png";
        }

        public override Vector4 GetSpriteBorder()
        {
            var size = GetTextureSize();
            float border = size.x * 0.5f;
            return new Vector4(border, border, border, border);
        }

        public override Texture2D Generate()
        {
            int innerWidthPx = _settings.Width / 2;
            int outerWidthPx = _settings.Width - innerWidthPx;

            int size = (_settings.Round + outerWidthPx) * 2;
            var (texture, pixels) = CreateTextureWithPixels(size, size);

            float center = size * 0.5f - 0.5f;
            float baseRadius = _settings.Round;
            float outerRadius = baseRadius + outerWidthPx;
            float innerRadius = baseRadius - innerWidthPx;

            for (int y = 0; y < size; y++)
            {
                int rowOffset = y * size;
                float dy = y - center;
                float dySq = dy * dy;

                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float distance = Mathf.Sqrt(dx * dx + dySq);

                    float outerSD = distance - outerRadius;
                    float innerSD = innerRadius - distance;

                    float outerAlpha = CalculateAntiAliasedAlpha(outerSD);
                    float innerAlpha = CalculateAntiAliasedAlpha(innerSD);
                    float alpha = Mathf.Min(outerAlpha, innerAlpha);

                    if (alpha > 0f)
                    {
                        pixels[rowOffset + x] = new Color32(255, 255, 255, AlphaToByte(alpha));
                    }
                }
            }

            ApplyPixels(texture, pixels);
            return texture;
        }
    }
}

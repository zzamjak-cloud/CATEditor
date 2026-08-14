using UnityEngine;
using UnityEditor;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 원(Circle) 생성기 - Fill 타입
    /// </summary>
    public class CircleGenerator : BaseShapeGenerator
    {
        private readonly CircleSettings _settings;

        public override string ShapeName => "Circle";

        public CircleGenerator() : this(new CircleSettings()) { }

        public CircleGenerator(CircleSettings settings)
        {
            _settings = settings;
        }

        public override void DrawSettingsGUI()
        {
            _settings.Round = EditorGUILayout.IntSlider(
                new GUIContent("Round", "원의 반지름"),
                _settings.Round, 1, 256);
        }

        public override Vector2Int GetTextureSize()
        {
            return new Vector2Int(_settings.Round * 2, _settings.Round * 2);
        }

        public override string GetFileName()
        {
            return $"Circle_R{_settings.Round}.png";
        }

        public override Vector4 GetSpriteBorder()
        {
            float border = _settings.Round;
            return new Vector4(border, border, border, border);
        }

        public override Texture2D Generate()
        {
            int size = _settings.Round * 2;
            var (texture, pixels) = CreateTextureWithPixels(size, size);

            float center = _settings.Round - 0.5f;
            float radius = _settings.Round;

            for (int y = 0; y < size; y++)
            {
                int rowOffset = y * size;
                float dy = y - center;
                float dySq = dy * dy;

                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float distance = Mathf.Sqrt(dx * dx + dySq);
                    float signedDistance = distance - radius;
                    float alpha = CalculateAntiAliasedAlpha(signedDistance);

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

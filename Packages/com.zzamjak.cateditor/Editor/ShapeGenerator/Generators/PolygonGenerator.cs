using UnityEngine;
using UnityEditor;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 다각형(Polygon) 생성기
    /// </summary>
    public class PolygonGenerator : BaseShapeGenerator
    {
        private readonly PolygonSettings _settings;

        public override string ShapeName => "Polygon";

        public PolygonGenerator() : this(new PolygonSettings()) { }

        public PolygonGenerator(PolygonSettings settings)
        {
            _settings = settings;
        }

        public override void DrawSettingsGUI()
        {
            _settings.Size = EditorGUILayout.IntSlider(
                new GUIContent("Size", "다각형 크기"),
                _settings.Size, 4, 256);

            _settings.Sides = EditorGUILayout.IntSlider(
                new GUIContent("Sides", "변의 개수 (3~12)"),
                _settings.Sides, 3, 12);

            int maxRadius = _settings.Size / 4;
            _settings.CornerRadius = EditorGUILayout.IntSlider(
                new GUIContent("Corner Radius", "모서리 둥글기"),
                _settings.CornerRadius, 0, maxRadius);

            _settings.Rotation = EditorGUILayout.Slider(
                new GUIContent("Rotation", "회전 각도"),
                _settings.Rotation, 0f, 360f);
        }

        public override Vector2Int GetTextureSize()
        {
            return new Vector2Int(_settings.Size, _settings.Size);
        }

        public override string GetFileName()
        {
            string rotStr = _settings.Rotation > 0 ? $"_Rot{Mathf.RoundToInt(_settings.Rotation)}" : "";
            return $"Polygon{_settings.Sides}_S{_settings.Size}_R{_settings.CornerRadius}{rotStr}.png";
        }

        public override Texture2D Generate()
        {
            var (texture, pixels) = CreateTextureWithPixels(_settings.Size, _settings.Size);

            float center = _settings.Size * 0.5f;
            float radius = _settings.Size * 0.45f;

            Vector2[] vertices = new Vector2[_settings.Sides];
            float angleStep = 360f / _settings.Sides;
            float startAngle = -90f + _settings.Rotation;

            for (int i = 0; i < _settings.Sides; i++)
            {
                float angle = (startAngle + angleStep * i) * Mathf.Deg2Rad;
                vertices[i] = new Vector2(
                    center + radius * Mathf.Cos(angle),
                    center + radius * Mathf.Sin(angle)
                );
            }

            for (int y = 0; y < _settings.Size; y++)
            {
                int rowOffset = y * _settings.Size;
                float py = y + 0.5f;

                for (int x = 0; x < _settings.Size; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, py);
                    float sdf = RoundedPolygonSDF(point, vertices, _settings.CornerRadius);
                    float alpha = CalculateAntiAliasedAlpha(sdf);

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

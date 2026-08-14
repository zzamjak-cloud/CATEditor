using UnityEngine;
using UnityEditor;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 다각형 + 외곽선(Polygon + Outline) 생성기
    /// 내부는 흰색, 외곽선은 그레이스케일
    /// </summary>
    public class PolygonWithOutlineGenerator : BaseShapeGenerator
    {
        private readonly PolygonSettings _settings;

        public override string ShapeName => "Polygon + Outline";

        public PolygonWithOutlineGenerator() : this(new PolygonSettings()) { }

        public PolygonWithOutlineGenerator(PolygonSettings settings)
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

            int maxWidth = _settings.Size / 2;
            _settings.OutlineWidth = EditorGUILayout.IntSlider(
                new GUIContent("Outline Width", "외곽선 두께"),
                _settings.OutlineWidth, 1, maxWidth);

            _settings.OutlineGray = EditorGUILayout.Slider(
                new GUIContent("Outline Gray", "외곽선 그레이스케일 (0=검정, 1=흰색)"),
                _settings.OutlineGray, 0f, 1f);

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
            int outerWidth = _settings.OutlineWidth - (_settings.OutlineWidth / 2);
            int size = _settings.Size + outerWidth * 2;
            return new Vector2Int(size, size);
        }

        public override string GetFileName()
        {
            int grayInt = Mathf.RoundToInt(_settings.OutlineGray * 100);
            string rotStr = _settings.Rotation > 0 ? $"_Rot{Mathf.RoundToInt(_settings.Rotation)}" : "";
            return $"Polygon{_settings.Sides}_S{_settings.Size}_OutF{_settings.OutlineWidth}_G{grayInt}_R{_settings.CornerRadius}{rotStr}.png";
        }

        public override Texture2D Generate()
        {
            int innerWidthPx = _settings.OutlineWidth / 2;
            int outerWidthPx = _settings.OutlineWidth - innerWidthPx;

            int texSize = _settings.Size + outerWidthPx * 2;
            var (texture, pixels) = CreateTextureWithPixels(texSize, texSize);

            float center = texSize * 0.5f;
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

            byte outlineGrayByte = (byte)(_settings.OutlineGray * 255f + 0.5f);

            for (int y = 0; y < texSize; y++)
            {
                int rowOffset = y * texSize;
                float py = y + 0.5f;

                for (int x = 0; x < texSize; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, py);
                    float baseSDF = RoundedPolygonSDF(point, vertices, _settings.CornerRadius);

                    float outerSDF = baseSDF - outerWidthPx;
                    float outerAlpha = CalculateAntiAliasedAlpha(outerSDF);

                    if (outerAlpha <= 0f) continue;

                    float fillSDF = baseSDF + innerWidthPx;
                    float fillAlpha = CalculateAntiAliasedAlpha(fillSDF);

                    byte a = AlphaToByte(outerAlpha);

                    if (fillAlpha >= 1f)
                    {
                        pixels[rowOffset + x] = new Color32(255, 255, 255, a);
                    }
                    else if (fillAlpha <= 0f)
                    {
                        pixels[rowOffset + x] = new Color32(outlineGrayByte, outlineGrayByte, outlineGrayByte, a);
                    }
                    else
                    {
                        byte blended = (byte)(outlineGrayByte + (255 - outlineGrayByte) * fillAlpha + 0.5f);
                        pixels[rowOffset + x] = new Color32(blended, blended, blended, a);
                    }
                }
            }

            ApplyPixels(texture, pixels);
            return texture;
        }
    }
}

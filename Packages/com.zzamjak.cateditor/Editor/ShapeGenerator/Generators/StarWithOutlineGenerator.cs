using UnityEngine;
using UnityEditor;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 별 + 외곽선(Star + Outline) 생성기
    /// 내부는 흰색, 외곽선은 그레이스케일
    /// </summary>
    public class StarWithOutlineGenerator : BaseShapeGenerator
    {
        private readonly StarSettings _settings;

        public override string ShapeName => "Star + Outline";

        public StarWithOutlineGenerator() : this(new StarSettings()) { }

        public StarWithOutlineGenerator(StarSettings settings)
        {
            _settings = settings;
        }

        public override void DrawSettingsGUI()
        {
            _settings.Size = EditorGUILayout.IntSlider(
                new GUIContent("Size", "별 크기"),
                _settings.Size, 4, 256);

            _settings.Points = EditorGUILayout.IntSlider(
                new GUIContent("Points", "꼭지점 개수 (3~12)"),
                _settings.Points, 3, 12);

            _settings.InnerRatio = EditorGUILayout.Slider(
                new GUIContent("Inner Ratio", "내부 반지름 비율 (0.1~0.9)"),
                _settings.InnerRatio, 0.1f, 0.9f);

            int maxWidth = _settings.Size / 4;
            _settings.OutlineWidth = EditorGUILayout.IntSlider(
                new GUIContent("Outline Width", "외곽선 두께"),
                _settings.OutlineWidth, 1, maxWidth);

            _settings.OutlineGray = EditorGUILayout.Slider(
                new GUIContent("Outline Gray", "외곽선 그레이스케일 (0=검정, 1=흰색)"),
                _settings.OutlineGray, 0f, 1f);

            int maxRadius = _settings.Size / 8;
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
            int innerInt = Mathf.RoundToInt(_settings.InnerRatio * 100);
            int grayInt = Mathf.RoundToInt(_settings.OutlineGray * 100);
            string rotStr = _settings.Rotation > 0 ? $"_Rot{Mathf.RoundToInt(_settings.Rotation)}" : "";
            return $"Star{_settings.Points}_S{_settings.Size}_OutF{_settings.OutlineWidth}_I{innerInt}_G{grayInt}_R{_settings.CornerRadius}{rotStr}.png";
        }

        public override Texture2D Generate()
        {
            int innerWidthPx = _settings.OutlineWidth / 2;
            int outerWidthPx = _settings.OutlineWidth - innerWidthPx;

            int texSize = _settings.Size + outerWidthPx * 2;
            var (texture, pixels) = CreateTextureWithPixels(texSize, texSize);

            float center = texSize * 0.5f;
            float outerRadius = _settings.Size * 0.45f;
            float innerRadius = outerRadius * _settings.InnerRatio;

            int vertexCount = _settings.Points * 2;
            Vector2[] vertices = new Vector2[vertexCount];
            float angleStep = 360f / vertexCount;
            float startAngle = 90f + _settings.Rotation; // 군인 계급 별 모양 (위를 향함)

            for (int i = 0; i < vertexCount; i++)
            {
                float angle = (startAngle + angleStep * i) * Mathf.Deg2Rad;
                float radius = (i % 2 == 0) ? outerRadius : innerRadius;
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
                    // Corner Radius는 외부 정점에만 적용하여 Inner Ratio와 독립적으로 동작
                    float baseSDF = RoundedStarSDF(point, vertices, _settings.CornerRadius);

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

        /// <summary>
        /// 별 모양 전용 SDF: Corner Radius를 외부 정점에만 적용
        /// </summary>
        private float RoundedStarSDF(Vector2 point, Vector2[] vertices, float cornerRadius)
        {
            if (cornerRadius <= 0)
                return PolygonSDF(point, vertices);

            int n = vertices.Length;

            // 외부 정점에만 Corner Radius 적용
            Vector2[] cornerCenters = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                // 외부 정점 (i % 2 == 0)에만 Corner Radius 적용
                if (i % 2 == 0)
                {
                    int prevIdx = (i + n - 1) % n;
                    int nextIdx = (i + 1) % n;

                    Vector2 curr = vertices[i];
                    float dirPrevX = vertices[prevIdx].x - curr.x;
                    float dirPrevY = vertices[prevIdx].y - curr.y;
                    float dirNextX = vertices[nextIdx].x - curr.x;
                    float dirNextY = vertices[nextIdx].y - curr.y;

                    // Normalize
                    float lenPrev = Mathf.Sqrt(dirPrevX * dirPrevX + dirPrevY * dirPrevY);
                    float lenNext = Mathf.Sqrt(dirNextX * dirNextX + dirNextY * dirNextY);

                    if (lenPrev > 0.0001f) { dirPrevX /= lenPrev; dirPrevY /= lenPrev; }
                    if (lenNext > 0.0001f) { dirNextX /= lenNext; dirNextY /= lenNext; }

                    float bisectorX = dirPrevX + dirNextX;
                    float bisectorY = dirPrevY + dirNextY;
                    float lenBisector = Mathf.Sqrt(bisectorX * bisectorX + bisectorY * bisectorY);

                    if (lenBisector > 0.0001f)
                    {
                        bisectorX /= lenBisector;
                        bisectorY /= lenBisector;
                    }

                    float dot = dirPrevX * dirNextX + dirPrevY * dirNextY;
                    float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));
                    float halfAngle = angle * 0.5f;

                    if (halfAngle > 0.001f)
                    {
                        float insetDist = cornerRadius / Mathf.Sin(halfAngle);
                        cornerCenters[i] = new Vector2(curr.x + bisectorX * insetDist, curr.y + bisectorY * insetDist);
                    }
                    else
                    {
                        cornerCenters[i] = curr;
                    }
                }
                else
                {
                    // 내부 정점은 그대로 유지
                    cornerCenters[i] = vertices[i];
                }
            }

            return PolygonSDF(point, cornerCenters) - cornerRadius;
        }
    }
}

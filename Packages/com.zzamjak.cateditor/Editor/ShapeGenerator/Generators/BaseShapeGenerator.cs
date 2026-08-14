using UnityEngine;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 도형 생성기 기본 클래스
    /// 공통 유틸리티 메서드 제공
    /// </summary>
    public abstract class BaseShapeGenerator : IShapeGenerator
    {
        protected static readonly Color32 ShapeColor = new Color32(255, 255, 255, 255);
        protected static readonly Color32 ClearColor = new Color32(0, 0, 0, 0);

        public abstract string ShapeName { get; }
        public abstract void DrawSettingsGUI();
        public abstract Texture2D Generate();
        public abstract string GetFileName();
        public abstract Vector2Int GetTextureSize();

        /// <summary>
        /// Sprite Border 값 반환 (기본값: 0,0,0,0)
        /// Circle 계열만 오버라이드하여 Border 적용
        /// </summary>
        public virtual Vector4 GetSpriteBorder()
        {
            return Vector4.zero;
        }

        /// <summary>
        /// 텍스처의 투명 영역을 트림하여 새 텍스처 반환 (Color32 최적화)
        /// </summary>
        public static Texture2D TrimTexture(Texture2D source)
        {
            int width = source.width;
            int height = source.height;
            Color32[] pixels = source.GetPixels32();

            // 알파가 있는 픽셀의 경계 찾기
            int minX = width, maxX = 0;
            int minY = height, maxY = 0;
            bool hasContent = false;

            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[rowOffset + x].a > 0)
                    {
                        hasContent = true;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            // 콘텐츠가 없으면 원본 반환
            if (!hasContent)
                return source;

            // 이미 트림된 상태면 원본 반환
            if (minX == 0 && minY == 0 && maxX == width - 1 && maxY == height - 1)
                return source;

            // 새 크기 계산
            int newWidth = maxX - minX + 1;
            int newHeight = maxY - minY + 1;

            // 새 텍스처 생성
            var trimmed = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave,
                filterMode = source.filterMode,
                wrapMode = source.wrapMode
            };

            // 픽셀 복사 (행 단위로 복사하여 최적화)
            Color32[] newPixels = new Color32[newWidth * newHeight];
            for (int y = 0; y < newHeight; y++)
            {
                int srcOffset = (minY + y) * width + minX;
                int dstOffset = y * newWidth;
                System.Array.Copy(pixels, srcOffset, newPixels, dstOffset, newWidth);
            }

            trimmed.SetPixels32(newPixels);
            trimmed.Apply();

            // 원본 텍스처 정리
            Object.DestroyImmediate(source);

            return trimmed;
        }

        /// <summary>
        /// 투명 배경의 텍스처 및 픽셀 배열 생성 (배열 기반 처리용)
        /// </summary>
        protected (Texture2D texture, Color32[] pixels) CreateTextureWithPixels(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            // 투명하게 초기화된 배열 (Color32의 기본값이 (0,0,0,0)이므로 별도 초기화 불필요)
            var pixels = new Color32[width * height];

            return (texture, pixels);
        }

        /// <summary>
        /// 픽셀 배열을 텍스처에 적용 (최종 단계)
        /// </summary>
        protected void ApplyPixels(Texture2D texture, Color32[] pixels)
        {
            texture.SetPixels32(pixels);
            texture.Apply();
        }

        /// <summary>
        /// 안티앨리어싱 알파값 계산 (Signed Distance 기반)
        /// </summary>
        /// <param name="signedDistance">음수: 내부, 양수: 외부</param>
        /// <param name="edgeWidth">안티앨리어싱 엣지 너비</param>
        protected float CalculateAntiAliasedAlpha(float signedDistance, float edgeWidth = 1.0f)
        {
            return Mathf.Clamp01(0.5f - signedDistance / edgeWidth);
        }

        /// <summary>
        /// float 알파를 byte로 변환
        /// </summary>
        protected byte AlphaToByte(float alpha)
        {
            return (byte)(alpha * 255f + 0.5f);
        }

        /// <summary>
        /// 점과 선분 사이의 최소 거리 계산
        /// </summary>
        protected float PointToSegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            Vector2 ap = point - a;

            float abDotAb = ab.x * ab.x + ab.y * ab.y;
            if (abDotAb < 0.0001f) return Vector2.Distance(point, a);

            float t = Mathf.Clamp01((ap.x * ab.x + ap.y * ab.y) / abDotAb);
            float closestX = a.x + t * ab.x;
            float closestY = a.y + t * ab.y;

            float dx = point.x - closestX;
            float dy = point.y - closestY;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 점이 다각형 내부에 있는지 확인 (Ray casting)
        /// </summary>
        protected bool IsPointInPolygon(Vector2 point, Vector2[] vertices)
        {
            int n = vertices.Length;
            bool inside = false;
            float px = point.x;
            float py = point.y;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float viy = vertices[i].y;
                float vjy = vertices[j].y;

                if ((viy > py) != (vjy > py))
                {
                    float vix = vertices[i].x;
                    float vjx = vertices[j].x;
                    if (px < (vjx - vix) * (py - viy) / (vjy - viy) + vix)
                    {
                        inside = !inside;
                    }
                }
            }

            return inside;
        }

        /// <summary>
        /// 점에서 다각형 경계까지의 Signed Distance 계산
        /// </summary>
        protected float PolygonSDF(Vector2 point, Vector2[] vertices)
        {
            float minDist = float.MaxValue;
            int n = vertices.Length;

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                float dist = PointToSegmentDistance(point, vertices[i], vertices[next]);
                if (dist < minDist) minDist = dist;
            }

            bool inside = IsPointInPolygon(point, vertices);
            return inside ? -minDist : minDist;
        }

        /// <summary>
        /// 둥근 모서리가 적용된 다각형의 SDF 계산
        /// Minkowski sum 원리: 축소된 다각형 SDF - cornerRadius
        /// </summary>
        protected float RoundedPolygonSDF(Vector2 point, Vector2[] vertices, float cornerRadius)
        {
            if (cornerRadius <= 0)
                return PolygonSDF(point, vertices);

            int n = vertices.Length;

            // 각 모서리의 원 중심 계산 (축소된 다각형의 꼭지점)
            Vector2[] cornerCenters = new Vector2[n];
            for (int i = 0; i < n; i++)
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

            return PolygonSDF(point, cornerCenters) - cornerRadius;
        }
    }
}

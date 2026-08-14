namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 그라디언트 방향
    /// </summary>
    public enum GradientDirection
    {
        Horizontal,
        Vertical
    }

    /// <summary>
    /// Circle 공통 설정 (Fill, Outline, FillOutline 간 공유)
    /// </summary>
    public class CircleSettings
    {
        public int Round = 10;
        public int Width = 2;
        public float OutlineGray = 0.5f;
    }

    /// <summary>
    /// Polygon 공통 설정 (Fill, Outline, FillOutline 간 공유)
    /// </summary>
    public class PolygonSettings
    {
        public int Size = 20;
        public int Sides = 6;
        public int OutlineWidth = 2;
        public int CornerRadius = 0;
        public float Rotation = 0f;
        public float OutlineGray = 0.5f;
    }

    /// <summary>
    /// Star 공통 설정 (Fill, Outline, FillOutline 간 공유)
    /// </summary>
    public class StarSettings
    {
        public int Size = 20;
        public int Points = 5;
        public float InnerRatio = 0.5f;
        public int OutlineWidth = 2;
        public int CornerRadius = 0;
        public float Rotation = 0f;
        public float OutlineGray = 0.5f;
    }

    /// <summary>
    /// Gradient 공통 설정 (Color, Alpha 간 공유)
    /// </summary>
    public class GradientSettings
    {
        public int Width = 256;
        public int Height = 256;
        public bool UniformSize = true;
        public GradientDirection Direction = GradientDirection.Horizontal;
        public float CurvePower = 0.5f; // 0~1 범위, 0.5가 선형
    }

    /// <summary>
    /// 노이즈 타입
    /// </summary>
    public enum NoiseType
    {
        Perlin,
        Billow,
        Ridged
    }

    /// <summary>
    /// Noise 공통 설정
    /// </summary>
    public class NoiseSettings
    {
        public int Size = 256;
        public NoiseType NoiseType = NoiseType.Perlin;
        public float Scale = 50f;
        public int Octaves = 4;
        public float Persistence = 0.5f;
        public float Lacunarity = 2f;
        public int Seed = 0;
        public bool Invert = false;
        public bool Seamless = false;
        public float CurvePower = 0.5f; // 0~1 범위, 0.5가 선형
    }
}

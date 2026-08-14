using UnityEngine;
using UnityEditor;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 노이즈 텍스처 생성기 - Perlin Noise 기반
    /// </summary>
    public class NoiseGenerator : BaseShapeGenerator
    {
        private readonly NoiseSettings _settings;

        public override string ShapeName => "Noise";

        public NoiseGenerator() : this(new NoiseSettings()) { }

        public NoiseGenerator(NoiseSettings settings)
        {
            _settings = settings;
        }

        public override void DrawSettingsGUI()
        {
            _settings.Size = EditorGUILayout.IntSlider(
                new GUIContent("Size", "텍스처 크기 (정사각형)"),
                _settings.Size, 64, 1024);

            EditorGUILayout.Space(5);

            _settings.NoiseType = (NoiseType)EditorGUILayout.EnumPopup(
                new GUIContent("Noise Type", "노이즈 타입"),
                _settings.NoiseType);

            EditorGUILayout.Space(5);

            _settings.Scale = EditorGUILayout.Slider(
                new GUIContent("Scale", "노이즈 스케일 (클수록 넓은 패턴)"),
                _settings.Scale, 1f, 200f);

            _settings.Octaves = EditorGUILayout.IntSlider(
                new GUIContent("Octaves", "디테일 레벨 (많을수록 세밀)"),
                _settings.Octaves, 1, 8);

            _settings.Persistence = EditorGUILayout.Slider(
                new GUIContent("Persistence", "각 옥타브의 진폭 감쇠"),
                _settings.Persistence, 0f, 1f);

            _settings.Lacunarity = EditorGUILayout.Slider(
                new GUIContent("Lacunarity", "각 옥타브의 주파수 증가"),
                _settings.Lacunarity, 1f, 4f);

            EditorGUILayout.Space(5);

            _settings.Seed = EditorGUILayout.IntField(
                new GUIContent("Seed", "랜덤 시드 (같은 시드면 같은 결과)"),
                _settings.Seed);

            EditorGUILayout.BeginHorizontal();
            _settings.Invert = EditorGUILayout.Toggle(
                new GUIContent("Invert", "노이즈 반전"),
                _settings.Invert);

            _settings.Seamless = EditorGUILayout.Toggle(
                new GUIContent("Seamless", "타일 가능 (실험적)"),
                _settings.Seamless);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            _settings.CurvePower = EditorGUILayout.Slider(
                new GUIContent("Curve Power", "노이즈 곡선 가중치 (0.5=선형, <0.5=어두움 강조, >0.5=밝음 강조)"),
                _settings.CurvePower, 0f, 1f);

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"0.5 = 선형, {_settings.CurvePower:F2} = {GetCurveTypeName()}", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Random Seed"))
            {
                _settings.Seed = Random.Range(0, 10000);
            }
        }

        private string GetCurveTypeName()
        {
            if (Mathf.Approximately(_settings.CurvePower, 0.5f))
                return "선형 (Linear)";
            else if (_settings.CurvePower > 0.5f)
                return "밝음 강조 (Brighten)";
            else
                return "어두움 강조 (Darken)";
        }

        /// <summary>
        /// 0~1 범위의 Curve Power를 실제 지수 값으로 변환
        /// 0 → 0.1 (어두움 강조)
        /// 0.5 → 1.0 (선형)
        /// 1.0 → 5.0 (밝음 강조)
        /// </summary>
        private float ConvertCurvePowerToExponent(float curvePower)
        {
            if (curvePower < 0.5f)
            {
                // 0~0.5 → 0.1~1.0 (어두움 강조)
                return Mathf.Lerp(0.1f, 1.0f, curvePower * 2f);
            }
            else
            {
                // 0.5~1.0 → 1.0~5.0 (밝음 강조)
                return Mathf.Lerp(1.0f, 5.0f, (curvePower - 0.5f) * 2f);
            }
        }

        public override Vector2Int GetTextureSize()
        {
            return new Vector2Int(_settings.Size, _settings.Size);
        }

        public override string GetFileName()
        {
            string typeName = _settings.NoiseType.ToString();
            string invertSuffix = _settings.Invert ? "_Inv" : "";
            string seamlessSuffix = _settings.Seamless ? "_Tile" : "";
            string curveSuffix = !Mathf.Approximately(_settings.CurvePower, 0.5f)
                ? $"_C{_settings.CurvePower:F2}"
                : "";
            return $"Noise_{typeName}_S{_settings.Scale:F0}_O{_settings.Octaves}{curveSuffix}{invertSuffix}{seamlessSuffix}_{_settings.Size}.png";
        }

        public override Texture2D Generate()
        {
            int size = _settings.Size;
            var (texture, pixels) = CreateTextureWithPixels(size, size);

            // 시드 기반 오프셋
            float seedOffsetX = _settings.Seed * 100f;
            float seedOffsetY = _settings.Seed * 100f + 1000f;

            // 노이즈 값 범위 추적 (정규화용)
            float minValue = float.MaxValue;
            float maxValue = float.MinValue;
            float[] noiseValues = new float[size * size];

            // 1단계: 노이즈 값 계산
            for (int y = 0; y < size; y++)
            {
                int rowOffset = y * size;
                for (int x = 0; x < size; x++)
                {
                    float noiseValue = GenerateNoise(x, y, seedOffsetX, seedOffsetY);
                    noiseValues[rowOffset + x] = noiseValue;

                    if (noiseValue < minValue) minValue = noiseValue;
                    if (noiseValue > maxValue) maxValue = noiseValue;
                }
            }

            // 2단계: 정규화 및 픽셀 색상 설정
            float range = maxValue - minValue;
            if (range < 0.0001f) range = 1f; // 0으로 나누기 방지

            // 0~1 범위를 실제 지수 값으로 변환
            float exponent = ConvertCurvePowerToExponent(_settings.CurvePower);

            for (int y = 0; y < size; y++)
            {
                int rowOffset = y * size;
                for (int x = 0; x < size; x++)
                {
                    float noiseValue = noiseValues[rowOffset + x];
                    float normalized = (noiseValue - minValue) / range;

                    // Curve Power 적용 (0.5가 아닐 때만)
                    if (!Mathf.Approximately(_settings.CurvePower, 0.5f))
                    {
                        normalized = Mathf.Pow(normalized, exponent);
                    }

                    if (_settings.Invert)
                        normalized = 1f - normalized;

                    byte gray = (byte)(normalized * 255f + 0.5f);
                    pixels[rowOffset + x] = new Color32(gray, gray, gray, 255);
                }
            }

            ApplyPixels(texture, pixels);
            return texture;
        }

        /// <summary>
        /// 노이즈 값 생성 (fbm - Fractional Brownian Motion)
        /// </summary>
        private float GenerateNoise(int x, int y, float seedOffsetX, float seedOffsetY)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int i = 0; i < _settings.Octaves; i++)
            {
                float sampleX, sampleY;

                if (_settings.Seamless)
                {
                    // Seamless 타일링을 위한 도메인 워핑
                    float nx = (float)x / _settings.Size;
                    float ny = (float)y / _settings.Size;
                    float scale = _settings.Scale * frequency;

                    float s = nx * 2f * Mathf.PI;
                    float t = ny * 2f * Mathf.PI;

                    float dx = Mathf.Cos(s) * scale / (2f * Mathf.PI);
                    float dy = Mathf.Sin(s) * scale / (2f * Mathf.PI);
                    float dz = Mathf.Cos(t) * scale / (2f * Mathf.PI);
                    float dw = Mathf.Sin(t) * scale / (2f * Mathf.PI);

                    sampleX = dx + seedOffsetX;
                    sampleY = dy + seedOffsetY;
                    float sampleZ = dz + seedOffsetX + 500f;
                    float sampleW = dw + seedOffsetY + 500f;

                    // 4D 노이즈를 2D로 근사
                    float noise1 = Mathf.PerlinNoise(sampleX, sampleY);
                    float noise2 = Mathf.PerlinNoise(sampleZ, sampleW);
                    total += ApplyNoiseType((noise1 + noise2) * 0.5f) * amplitude;
                }
                else
                {
                    // 일반 노이즈
                    sampleX = (x / _settings.Scale) * frequency + seedOffsetX;
                    sampleY = (y / _settings.Scale) * frequency + seedOffsetY;

                    float noise = Mathf.PerlinNoise(sampleX, sampleY);
                    total += ApplyNoiseType(noise) * amplitude;
                }

                maxValue += amplitude;
                amplitude *= _settings.Persistence;
                frequency *= _settings.Lacunarity;
            }

            return total / maxValue;
        }

        /// <summary>
        /// 노이즈 타입에 따른 변형 적용
        /// </summary>
        private float ApplyNoiseType(float noise)
        {
            switch (_settings.NoiseType)
            {
                case NoiseType.Perlin:
                    return noise;

                case NoiseType.Billow:
                    // 구름 같은 효과
                    return Mathf.Abs(noise * 2f - 1f);

                case NoiseType.Ridged:
                    // 산맥 같은 효과
                    return 1f - Mathf.Abs(noise * 2f - 1f);

                default:
                    return noise;
            }
        }
    }
}

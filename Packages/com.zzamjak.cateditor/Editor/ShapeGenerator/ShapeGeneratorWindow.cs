using UnityEngine;
using UnityEditor;
using System.IO;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 기본 도형 PNG 이미지 생성 에디터 윈도우
    /// </summary>
    public class ShapeGeneratorWindow : EditorWindow
    {
        private const string PREFS_KEY_SAVE_FOLDER = "CAT_ShapeGenerator_SaveFolder";
        private const string PREFS_KEY_SHAPE_TYPE = "CAT_ShapeGenerator_ShapeType";
        private const string PREFS_KEY_FILL_TYPE = "CAT_ShapeGenerator_FillType";
        private const double DEBOUNCE_DELAY = 0.15;

        private enum ShapeType { Circle, Polygon, Star, Gradient, Noise }
        private enum FillType { Fill, Outline, FillOutline }

        // Generator 2D 배열: [ShapeType][FillType]
        private IShapeGenerator[,] _generators;
        private readonly string[] _shapeNames = { "Circle", "Polygon", "Star", "Gradient", "Noise" };
        private readonly string[] _fillTypeNames = { "Fill", "Outline", "FillOutline" };

        // 공유 설정 인스턴스
        private CircleSettings _circleSettings;
        private PolygonSettings _polygonSettings;
        private StarSettings _starSettings;
        private GradientSettings _gradientSettings;
        private NoiseSettings _noiseSettings;

        private ShapeType _shapeType = ShapeType.Circle;
        private FillType _fillType = FillType.Fill;

        private string _saveFolderPath;
        private DefaultAsset _saveFolderAsset;

        private Texture2D _previewTexture;
        private Vector2 _scrollPosition;

        private double _lastChangeTime;
        private bool _pendingUpdate;

        private IShapeGenerator CurrentGenerator => _generators[(int)_shapeType, (int)_fillType];

        [MenuItem("CAT/Utility/Shape Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<ShapeGeneratorWindow>("Shape Generator");
            window.minSize = new Vector2(350, 400);
        }

        private void OnEnable()
        {
            InitializeGenerators();
            LoadSettings();
            UpdatePreview();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            SaveSettings();
            CleanupPreview();
        }

        private void OnDestroy()
        {
            EditorApplication.update -= OnEditorUpdate;
            CleanupPreview();
        }

        private void InitializeGenerators()
        {
            // 공유 설정 인스턴스 생성
            _circleSettings = new CircleSettings();
            _polygonSettings = new PolygonSettings();
            _starSettings = new StarSettings();
            _gradientSettings = new GradientSettings();
            _noiseSettings = new NoiseSettings();

            // [ShapeType][FillType] 순서로 배열 - 동일한 Settings 인스턴스 공유
            _generators = new IShapeGenerator[5, 3]
            {
                // Circle: Fill, Outline, FillOutline (모두 _circleSettings 공유)
                { new CircleGenerator(_circleSettings), new CircleOutlineGenerator(_circleSettings), new CircleWithOutlineGenerator(_circleSettings) },
                // Polygon: Fill, Outline, FillOutline (모두 _polygonSettings 공유)
                { new PolygonGenerator(_polygonSettings), new PolygonOutlineGenerator(_polygonSettings), new PolygonWithOutlineGenerator(_polygonSettings) },
                // Star: Fill, Outline, FillOutline (모두 _starSettings 공유)
                { new StarGenerator(_starSettings), new StarOutlineGenerator(_starSettings), new StarWithOutlineGenerator(_starSettings) },
                // Gradient: Color, Alpha, (미사용) (모두 _gradientSettings 공유)
                { new GradientGenerator(_gradientSettings), new GradientAlphaGenerator(_gradientSettings), null },
                // Noise: Fill만 사용 (모두 _noiseSettings 공유)
                { new NoiseGenerator(_noiseSettings), null, null }
            };
        }

        private void OnEditorUpdate()
        {
            if (_pendingUpdate && EditorApplication.timeSinceStartup - _lastChangeTime >= DEBOUNCE_DELAY)
            {
                _pendingUpdate = false;
                UpdatePreview();
            }
        }

        private void RequestPreviewUpdate()
        {
            _lastChangeTime = EditorApplication.timeSinceStartup;
            _pendingUpdate = true;
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawPreviewSection();
            EditorGUILayout.Space(10);

            DrawSaveFolderSection();
            EditorGUILayout.Space(10);

            DrawShapeSettingsSection();
            EditorGUILayout.Space(10);

            DrawGenerateSection();

            EditorGUILayout.EndScrollView();

            if (EditorGUI.EndChangeCheck())
            {
                RequestPreviewUpdate();
            }
        }

        private void DrawSaveFolderSection()
        {
            EditorGUILayout.LabelField("저장 위치", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            DefaultAsset newFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                _saveFolderAsset, typeof(DefaultAsset), false);

            if (EditorGUI.EndChangeCheck() && newFolder != _saveFolderAsset)
            {
                string path = AssetDatabase.GetAssetPath(newFolder);
                if (AssetDatabase.IsValidFolder(path))
                {
                    _saveFolderAsset = newFolder;
                    _saveFolderPath = path;
                    SaveSettings();
                }
                else if (newFolder == null)
                {
                    _saveFolderAsset = null;
                    _saveFolderPath = null;
                    SaveSettings();
                }
            }

            EditorGUI.BeginDisabledGroup(_saveFolderAsset == null);
            if (GUILayout.Button("Open", GUILayout.Width(50)))
            {
                EditorGUIUtility.PingObject(_saveFolderAsset);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            string displayPath = string.IsNullOrEmpty(_saveFolderPath) ? "폴더를 선택해주세요" : _saveFolderPath;
            EditorGUILayout.LabelField(displayPath, EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        private void DrawShapeSettingsSection()
        {
            EditorGUILayout.LabelField("도형 설정", EditorStyles.boldLabel);

            // Shape 드롭다운
            ShapeType newShapeType = (ShapeType)EditorGUILayout.Popup("Shape", (int)_shapeType, _shapeNames);
            if (newShapeType != _shapeType)
            {
                _shapeType = newShapeType;

                // Gradient 타입 선택 시 FillOutline이면 Fill로 자동 전환
                if (_shapeType == ShapeType.Gradient && _fillType == FillType.FillOutline)
                {
                    _fillType = FillType.Fill;
                }

                // Noise 타입 선택 시 Fill이 아니면 Fill로 자동 전환
                if (_shapeType == ShapeType.Noise && _fillType != FillType.Fill)
                {
                    _fillType = FillType.Fill;
                }

                SaveSettings();
            }

            // Fill Type 라디오 버튼
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Type");

            bool isGradient = _shapeType == ShapeType.Gradient;
            bool isNoise = _shapeType == ShapeType.Noise;

            for (int i = 0; i < _fillTypeNames.Length; i++)
            {
                // Gradient 타입일 때 FillOutline 비활성화
                // Noise 타입일 때 Outline, FillOutline 비활성화
                bool isDisabled = (isGradient && i == (int)FillType.FillOutline) ||
                                  (isNoise && i != (int)FillType.Fill);

                EditorGUI.BeginDisabledGroup(isDisabled);
                bool isSelected = (int)_fillType == i;
                bool newSelected = GUILayout.Toggle(isSelected, _fillTypeNames[i], EditorStyles.miniButtonMid);
                if (newSelected && !isSelected)
                {
                    _fillType = (FillType)i;
                    SaveSettings();
                }
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();

            // Generator 옵션
            CurrentGenerator.DrawSettingsGUI();
            EditorGUI.indentLevel--;
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel);

            if (_previewTexture != null)
            {
                EditorGUILayout.LabelField($"크기: {_previewTexture.width} x {_previewTexture.height} px", EditorStyles.miniLabel);

                EditorGUILayout.Space(5);

                float previewSize = Mathf.Min(200, position.width - 40);
                Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);

                EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 1f));
                DrawCheckerboard(previewRect);
                GUI.DrawTexture(previewRect, _previewTexture, ScaleMode.ScaleToFit, true);
            }
        }

        private void DrawCheckerboard(Rect rect)
        {
            int checkSize = 10;
            Color lightColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            Color darkColor = new Color(0.2f, 0.2f, 0.2f, 1f);

            int cols = Mathf.CeilToInt(rect.width / checkSize);
            int rows = Mathf.CeilToInt(rect.height / checkSize);

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    bool isLight = (x + y) % 2 == 0;
                    Rect checkRect = new Rect(
                        rect.x + x * checkSize,
                        rect.y + y * checkSize,
                        Mathf.Min(checkSize, rect.xMax - (rect.x + x * checkSize)),
                        Mathf.Min(checkSize, rect.yMax - (rect.y + y * checkSize))
                    );
                    EditorGUI.DrawRect(checkRect, isLight ? lightColor : darkColor);
                }
            }
        }

        private void DrawGenerateSection()
        {
            string fileName = CurrentGenerator.GetFileName();
            EditorGUILayout.LabelField($"파일명: {fileName}", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_saveFolderPath));
            if (GUILayout.Button("PNG 생성", GUILayout.Height(35)))
            {
                GenerateAndSave();
            }
            EditorGUI.EndDisabledGroup();

            if (string.IsNullOrEmpty(_saveFolderPath))
            {
                EditorGUILayout.HelpBox("저장 폴더를 먼저 선택해주세요.", MessageType.Warning);
            }
        }

        private void UpdatePreview()
        {
            CleanupPreview();

            if (_generators != null)
            {
                _previewTexture = CurrentGenerator.Generate();
                _previewTexture = BaseShapeGenerator.TrimTexture(_previewTexture);
            }

            Repaint();
        }

        private void CleanupPreview()
        {
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }
        }

        private void GenerateAndSave()
        {
            var generator = CurrentGenerator;
            var texture = generator.Generate();
            texture = BaseShapeGenerator.TrimTexture(texture);

            string fileName = generator.GetFileName();
            string fullPath = Path.Combine(_saveFolderPath, fileName);

            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(fullPath, pngData);

            DestroyImmediate(texture);

            AssetDatabase.Refresh();

            Vector4 border = generator.GetSpriteBorder();
            ApplyTextureImportSettings(fullPath, border);

            var createdAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
            if (createdAsset != null)
            {
                Selection.activeObject = createdAsset;
                EditorGUIUtility.PingObject(createdAsset);
            }

            Debug.Log($"[ShapeGenerator] 생성 완료: {fullPath}");
        }

        private void ApplyTextureImportSettings(string assetPath, Vector4 border)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;

                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = border;
                importer.SetTextureSettings(settings);

                importer.textureCompression = TextureImporterCompression.Compressed;

                TextureImporterPlatformSettings androidSettings = importer.GetPlatformTextureSettings("Android");
                androidSettings.overridden = true;
                androidSettings.format = TextureImporterFormat.ASTC_4x4;
                importer.SetPlatformTextureSettings(androidSettings);

                TextureImporterPlatformSettings iosSettings = importer.GetPlatformTextureSettings("iPhone");
                iosSettings.overridden = true;
                iosSettings.format = TextureImporterFormat.ASTC_4x4;
                importer.SetPlatformTextureSettings(iosSettings);

                importer.SaveAndReimport();
            }
        }

        private void SaveSettings()
        {
            if (!string.IsNullOrEmpty(_saveFolderPath))
            {
                string guid = AssetDatabase.AssetPathToGUID(_saveFolderPath);
                EditorPrefs.SetString(PREFS_KEY_SAVE_FOLDER, guid);
            }
            else
            {
                EditorPrefs.DeleteKey(PREFS_KEY_SAVE_FOLDER);
            }

            EditorPrefs.SetInt(PREFS_KEY_SHAPE_TYPE, (int)_shapeType);
            EditorPrefs.SetInt(PREFS_KEY_FILL_TYPE, (int)_fillType);
        }

        private void LoadSettings()
        {
            string guid = EditorPrefs.GetString(PREFS_KEY_SAVE_FOLDER, "");
            if (!string.IsNullOrEmpty(guid))
            {
                _saveFolderPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(_saveFolderPath) && AssetDatabase.IsValidFolder(_saveFolderPath))
                {
                    _saveFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_saveFolderPath);
                }
                else
                {
                    _saveFolderPath = null;
                    _saveFolderAsset = null;
                }
            }

            _shapeType = (ShapeType)Mathf.Clamp(EditorPrefs.GetInt(PREFS_KEY_SHAPE_TYPE, 0), 0, 4);
            _fillType = (FillType)Mathf.Clamp(EditorPrefs.GetInt(PREFS_KEY_FILL_TYPE, 0), 0, 2);
        }
    }
}

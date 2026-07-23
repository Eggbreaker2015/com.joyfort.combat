#if UNITY_EDITOR
using System;
using Combat.Unity.Display;
using UnityEditor;
using UnityEngine;

namespace Combat.Unity.Editor
{
    public sealed class SpriteSheetAnimationWindow : EditorWindow
    {
        private const string DefaultOutputFolder = "Assets/CombatSamples/Standalone/Config/Animation";

        private Texture2D _texture;
        private int _columns = 4;
        private int _rows = 4;
        private float _framesPerSecond = 8f;
        private bool _loop = true;
        private SpriteAnimationKey _animationKey = SpriteAnimationKey.Idle;
        private string _outputFolder = DefaultOutputFolder;
        private string _assetPrefix = string.Empty;
        private SpriteAnimationSetAsset _targetAnimationSet;

        [MenuItem("Combat/Demo Art/Create Sprite Animation From Sheet")]
        public static void Open()
        {
            var window = GetWindow<SpriteSheetAnimationWindow>("Sprite Sheet Animation");
            window.minSize = new Vector2(380f, 280f);
            window.TryUseSelectedAssets();
            window.Show();
        }

        private void OnEnable()
        {
            TryUseSelectedAssets();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Sprite Sheet", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _texture = (Texture2D)EditorGUILayout.ObjectField("Texture", _texture, typeof(Texture2D), allowSceneObjects: false);
            if (EditorGUI.EndChangeCheck() && _texture != null && string.IsNullOrWhiteSpace(_assetPrefix))
            {
                _assetPrefix = SanitizeAssetPrefix(_texture.name);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
            _columns = Mathf.Max(1, EditorGUILayout.IntField("Columns", _columns));
            _rows = Mathf.Max(1, EditorGUILayout.IntField("Rows", _rows));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
            _framesPerSecond = Mathf.Max(0.001f, EditorGUILayout.FloatField("Frames Per Second", _framesPerSecond));
            _loop = EditorGUILayout.Toggle("Loop", _loop);
            _animationKey = (SpriteAnimationKey)EditorGUILayout.EnumPopup("Animation Key", _animationKey);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            _targetAnimationSet = (SpriteAnimationSetAsset)EditorGUILayout.ObjectField(
                "Target Animation Set",
                _targetAnimationSet,
                typeof(SpriteAnimationSetAsset),
                allowSceneObjects: false);
            EditorGUILayout.BeginHorizontal();
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            if (GUILayout.Button("Select", GUILayout.Width(64f)))
            {
                SelectOutputFolder();
            }
            EditorGUILayout.EndHorizontal();
            _assetPrefix = EditorGUILayout.TextField("Asset Prefix", _assetPrefix);

            EditorGUILayout.Space(12f);
            using (new EditorGUI.DisabledScope(_texture == null || _animationKey == SpriteAnimationKey.None))
            {
                if (GUILayout.Button("Create / Update Animation Assets", GUILayout.Height(32f)))
                {
                    Build();
                }
            }
        }

        private void Build()
        {
            string texturePath = AssetDatabase.GetAssetPath(_texture);
            var request = new SpriteSheetAnimationBuildRequest
            {
                TextureAssetPath = texturePath,
                Columns = _columns,
                Rows = _rows,
                FramesPerSecond = _framesPerSecond,
                Loop = _loop,
                AnimationKey = _animationKey,
                OutputFolder = _outputFolder,
                AssetPrefix = string.IsNullOrWhiteSpace(_assetPrefix) ? _texture.name : _assetPrefix,
                TargetAnimationSetAssetPath = _targetAnimationSet == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(_targetAnimationSet)
            };

            try
            {
                SpriteSheetAnimationBuildResult result = SpriteSheetAnimationBuilder.Build(request);
                Selection.activeObject = result.AnimationSet;
                EditorGUIUtility.PingObject(result.AnimationSet);
                Debug.Log($"Created or updated sprite animation assets: {result.ClipPath}, {result.AnimationSetPath} ({result.SpriteCount} frames).");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void TryUseSelectedAssets()
        {
            UnityEngine.Object[] selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                return;
            }

            for (var i = 0; i < selectedObjects.Length; i++)
            {
                if (_texture == null && selectedObjects[i] is Texture2D selectedTexture)
                {
                    _texture = selectedTexture;
                    if (string.IsNullOrWhiteSpace(_assetPrefix))
                    {
                        _assetPrefix = SanitizeAssetPrefix(selectedTexture.name);
                    }
                }

                if (_targetAnimationSet == null && selectedObjects[i] is SpriteAnimationSetAsset selectedSet)
                {
                    _targetAnimationSet = selectedSet;
                }
            }
        }

        private void SelectOutputFolder()
        {
            string startFolder = AssetDatabase.IsValidFolder(_outputFolder) ? _outputFolder : DefaultOutputFolder;
            string absoluteStartFolder = AssetPathToAbsolutePath(startFolder);
            string selected = EditorUtility.OpenFolderPanel("Select output folder", absoluteStartFolder, string.Empty);
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            string dataPath = Application.dataPath.Replace('\\', '/');
            selected = selected.Replace('\\', '/');
            if (selected == dataPath)
            {
                _outputFolder = "Assets";
                return;
            }

            if (selected.StartsWith(dataPath + "/", StringComparison.Ordinal))
            {
                _outputFolder = "Assets" + selected.Substring(dataPath.Length);
            }
            else
            {
                Debug.LogWarning("Output folder must be inside this project's Assets folder.");
            }
        }

        private static string AssetPathToAbsolutePath(string assetPath)
        {
            string normalizedPath = (assetPath ?? string.Empty).Replace('\\', '/');
            if (normalizedPath == "Assets")
            {
                return Application.dataPath;
            }

            if (normalizedPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return Application.dataPath + normalizedPath.Substring("Assets".Length);
            }

            return Application.dataPath;
        }

        private static string SanitizeAssetPrefix(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? "SpriteAnimation" : name.Trim();
        }
    }
}
#endif

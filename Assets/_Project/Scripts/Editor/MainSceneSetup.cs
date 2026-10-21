using Bomb.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// NOTE: namespace は規約だと Bomb.Editor になるが、その中では識別子 Editor が
// namespace として解決され UnityEditor.Editor を継承できなくなる。ここだけ複数形にしている。
namespace Bomb.Editors
{
    /// <summary>
    /// Sc_Main の階層とプレイヤーを規約どおりに組み立てるエディタ専用ツール。
    /// 手作業だと区切りオブジェクトやコンポーネント設定の抜けが出るため、手順をコードに固定している。
    /// 何度実行しても同じ結果になる（既存のオブジェクトは作り直さず再利用する）。
    /// </summary>
    public static class MainSceneSetup
    {
        // ==========================================
        // Private Fields
        // ==========================================

        private const string MENU_PATH = "Bomb/Setup/Build Sc_Main Player";

        private const string SCENE_PATH = "Assets/_Project/Scenes/Sc_Main.unity";
        private const string PREFAB_PATH = "Assets/_Project/Prefabs/P_Player.prefab";
        private const string MATERIAL_PLAYER_PATH = "Assets/_Project/Materials/M_Player.mat";
        private const string MATERIAL_GROUND_PATH = "Assets/_Project/Materials/M_Ground.mat";

        private const string NAME_CAMERA = "MainCamera";
        private const string NAME_LIGHT = "DirectionalLight_Sun";
        private const string NAME_GROUND = "Ground";
        private const string NAME_PLAYER = "Player";

        private const string TAG_MAIN_CAMERA = "MainCamera";

        private const string URP_LIT_SHADER = "Universal Render Pipeline/Lit";
        private const string BUILTIN_LIT_SHADER = "Standard";

        /// <summary>URP Lit のベースカラープロパティ名。</summary>
        private const string PROP_BASE_COLOR = "_BaseColor";

        /// <summary>Built-in Standard のカラープロパティ名。</summary>
        private const string PROP_COLOR = "_Color";

        /// <summary>デフォルトの Cylinder メッシュは半径 0.5 / 高さ 2。CharacterController をこれに合わせる。</summary>
        private const float PLAYER_HEIGHT = 2f;
        private const float PLAYER_RADIUS = 0.5f;

        /// <summary>Plane は 1 スケールで 10m 四方。5 倍して 50m 四方にする。</summary>
        private const float GROUND_SCALE = 5f;

        /// <summary>規約の表と同じ並び順。ヒエラルキー上段から順に並べる。</summary>
        private static readonly string[] GROUP_NAMES =
        {
            "=== SYSTEMS ===",
            "=== CAMERAS ===",
            "=== LIGHTS ===",
            "=== ENVIRONMENT ===",
            "=== ACTORS ===",
            "=== UI ===",
            "=== RUNTIME ===",
        };

        private const int GROUP_INDEX_CAMERAS = 1;
        private const int GROUP_INDEX_LIGHTS = 2;
        private const int GROUP_INDEX_ENVIRONMENT = 3;
        private const int GROUP_INDEX_ACTORS = 4;

        // ==========================================
        // Private Methods
        // ==========================================

        [MenuItem(MENU_PATH)]
        private static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[MainSceneSetup] シーンを開けなかった: {SCENE_PATH}");
                return;
            }

            var groups = new GameObject[GROUP_NAMES.Length];
            for (int i = 0; i < GROUP_NAMES.Length; i++)
            {
                groups[i] = FindOrCreateRoot(GROUP_NAMES[i]);
            }

            Camera mainCamera = SetUpCamera(groups[GROUP_INDEX_CAMERAS]);
            SetUpLight(groups[GROUP_INDEX_LIGHTS]);
            SetUpGround(groups[GROUP_INDEX_ENVIRONMENT]);
            GameObject player = SetUpPlayer(groups[GROUP_INDEX_ACTORS], mainCamera);

            // 先頭から順に末尾へ送ると、結果として配列と同じ並びになる。
            foreach (GameObject group in groups)
            {
                group.transform.SetAsLastSibling();
            }

            SavePlayerPrefab(player);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = player;

            Debug.Log($"[MainSceneSetup] 完了: {SCENE_PATH} に {NAME_PLAYER} を構築し、{PREFAB_PATH} を更新した。");
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
            return created;
        }

        private static Camera SetUpCamera(GameObject parent)
        {
            // テンプレート由来の "Main Camera" をリネームして流用する。
            Camera[] found = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Camera camera = found.Length > 0 ? found[0] : null;

            if (camera == null)
            {
                var created = new GameObject(NAME_CAMERA);
                Undo.RegisterCreatedObjectUndo(created, "Create MainCamera");
                camera = created.AddComponent<Camera>();
                created.AddComponent<AudioListener>();
                created.tag = TAG_MAIN_CAMERA;
            }

            Undo.RecordObject(camera.gameObject, "Set up camera");
            Undo.RecordObject(camera.transform, "Set up camera transform");

            camera.gameObject.name = NAME_CAMERA;
            camera.transform.SetParent(parent.transform, false);

            // プレイヤーを斜め後方から見下ろす定点。追従は後で Cinemachine に任せる想定。
            camera.transform.localPosition = new Vector3(0f, 8f, -9f);
            camera.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            camera.transform.localScale = Vector3.one;

            return camera;
        }

        private static void SetUpLight(GameObject parent)
        {
            Light[] found = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Light directional = null;

            foreach (Light light in found)
            {
                if (light.type == LightType.Directional)
                {
                    directional = light;
                    break;
                }
            }

            if (directional == null)
            {
                var created = new GameObject(NAME_LIGHT);
                Undo.RegisterCreatedObjectUndo(created, "Create DirectionalLight_Sun");
                directional = created.AddComponent<Light>();
                directional.type = LightType.Directional;
            }

            Undo.RecordObject(directional.gameObject, "Set up light");
            Undo.RecordObject(directional.transform, "Set up light transform");

            directional.gameObject.name = NAME_LIGHT;
            directional.transform.SetParent(parent.transform, false);
            directional.transform.localPosition = new Vector3(0f, 3f, 0f);
            directional.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
            directional.transform.localScale = Vector3.one;
        }

        private static void SetUpGround(GameObject parent)
        {
            Transform existing = parent.transform.Find(NAME_GROUND);
            GameObject ground;

            if (existing != null)
            {
                ground = existing.gameObject;
            }
            else
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = NAME_GROUND;
                Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
                ground.transform.SetParent(parent.transform, false);
            }

            Undo.RecordObject(ground.transform, "Set up ground");
            ground.transform.localPosition = Vector3.zero;
            ground.transform.localRotation = Quaternion.identity;
            ground.transform.localScale = new Vector3(GROUND_SCALE, 1f, GROUND_SCALE);

            ApplyMaterial(ground, MATERIAL_GROUND_PATH, new Color(0.22f, 0.24f, 0.27f));
        }

        private static GameObject SetUpPlayer(GameObject parent, Camera mainCamera)
        {
            Transform existing = parent.transform.Find(NAME_PLAYER);
            GameObject player;

            if (existing != null)
            {
                player = existing.gameObject;
            }
            else
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                player.name = NAME_PLAYER;
                Undo.RegisterCreatedObjectUndo(player, "Create Player");
                player.transform.SetParent(parent.transform, false);
            }

            Undo.RecordObject(player.transform, "Set up player");
            player.transform.localPosition = new Vector3(0f, PLAYER_HEIGHT * 0.5f, 0f);
            player.transform.localRotation = Quaternion.identity;
            player.transform.localScale = Vector3.one;

            // Cylinder には CapsuleCollider が付いてくる。CharacterController と二重に当たるので外す。
            Collider primitiveCollider = player.GetComponent<Collider>();
            if (primitiveCollider != null && primitiveCollider is not CharacterController)
            {
                Undo.DestroyObjectImmediate(primitiveCollider);
            }

            CharacterController characterController = player.GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = Undo.AddComponent<CharacterController>(player);
            }

            Undo.RecordObject(characterController, "Configure CharacterController");
            characterController.height = PLAYER_HEIGHT;
            characterController.radius = PLAYER_RADIUS;
            characterController.center = Vector3.zero;
            characterController.slopeLimit = 45f;
            characterController.stepOffset = 0.3f;
            characterController.skinWidth = 0.08f;

            // 既定値 0.001 だと微小な入力が切り捨てられて動き出しが鈍る。
            characterController.minMoveDistance = 0f;

            // PlayerInputReader は PlayerController の [RequireComponent] で自動的に付く。
            PlayerController playerController = player.GetComponent<PlayerController>();
            if (playerController == null)
            {
                playerController = Undo.AddComponent<PlayerController>(player);
            }

            var serialized = new SerializedObject(playerController);
            serialized.FindProperty("_cameraTransform").objectReferenceValue =
                mainCamera != null ? mainCamera.transform : null;
            serialized.ApplyModifiedProperties();

            ApplyMaterial(player, MATERIAL_PLAYER_PATH, new Color(0.25f, 0.6f, 0.95f));

            return player;
        }

        private static void ApplyMaterial(GameObject target, string assetPath, Color color)
        {
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                return;
            }

            Material material = GetOrCreateMaterial(assetPath, color);
            if (material == null)
            {
                return;
            }

            Undo.RecordObject(renderer, "Assign material");
            renderer.sharedMaterial = material;
        }

        private static Material GetOrCreateMaterial(string assetPath, Color color)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find(URP_LIT_SHADER);

            // URP が無効なプロジェクトに持ち込まれてもピンクにならないよう保険をかける。
            if (shader == null && GraphicsSettings.currentRenderPipeline != null)
            {
                Material pipelineDefault = GraphicsSettings.currentRenderPipeline.defaultMaterial;
                shader = pipelineDefault != null ? pipelineDefault.shader : null;
            }

            if (shader == null)
            {
                shader = Shader.Find(BUILTIN_LIT_SHADER);
            }

            if (shader == null)
            {
                Debug.LogWarning($"[MainSceneSetup] シェーダーが見つからず {assetPath} を作成できなかった。");
                return null;
            }

            var material = new Material(shader);
            if (material.HasProperty(PROP_BASE_COLOR))
            {
                material.SetColor(PROP_BASE_COLOR, color);
            }

            if (material.HasProperty(PROP_COLOR))
            {
                material.SetColor(PROP_COLOR, color);
            }

            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        private static void SavePlayerPrefab(GameObject player)
        {
            // 既にプレハブインスタンスなら上書きしない。シーン側の変更で asset を壊さないため。
            if (PrefabUtility.IsPartOfPrefabInstance(player))
            {
                return;
            }

            PrefabUtility.SaveAsPrefabAssetAndConnect(player, PREFAB_PATH, InteractionMode.AutomatedAction);
        }
    }
}

using System.IO;
using System.Linq;
using Bomb.Battle;
using Bomb.Hud;
using Bomb.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// NOTE: namespace は規約だと Bomb.Editor になるが、その中では識別子 Editor が
// namespace として解決され UnityEditor.Editor を継承できなくなる。ここだけ複数形にしている。
namespace Bomb.Editors
{
    /// <summary>
    /// 検証用シーンとその中身を規約どおりに組み立てるエディタ専用ツール。
    /// 手作業だと区切りオブジェクトやコンポーネント設定の抜けが出るため、手順をコードに固定している。
    /// 何度実行しても同じ結果になる（既存のアセットとオブジェクトは作り直さず再利用する）。
    /// </summary>
    public static class MainSceneSetup
    {
        // ==========================================
        // Private Fields
        // ==========================================

        private const string MENU_BUILD_ALL = "Bomb/Setup/Build All";
        private const string MENU_BUILD_MAIN = "Bomb/Setup/Build Sc_Main (Fixed Throw)";
        private const string MENU_BUILD_CHARGE = "Bomb/Setup/Build Sc_Bomb_Charge (Charge Throw)";

        private const string SCENE_MAIN_PATH = "Assets/_Project/Scenes/Sc_Main.unity";
        private const string SCENE_CHARGE_PATH = "Assets/_Project/Scenes/Sc_Bomb_Charge.unity";

        private const string PLAYER_PREFAB_PATH = "Assets/_Project/Prefabs/P_Player.prefab";
        private const string GRENADE_PREFAB_PATH = "Assets/_Project/Prefabs/P_Grenade.prefab";

        private const string PLAYER_DATA_PATH = "Assets/_Project/ScriptableObjects/SO_PlayerData.asset";
        private const string GRENADE_DATA_PATH = "Assets/_Project/ScriptableObjects/SO_GrenadeData.asset";

        private const string MATERIAL_PLAYER_PATH = "Assets/_Project/Materials/M_Player.mat";
        private const string MATERIAL_GROUND_PATH = "Assets/_Project/Materials/M_Ground.mat";
        private const string MATERIAL_TARGET_PATH = "Assets/_Project/Materials/M_Target.mat";
        private const string MATERIAL_GRENADE_PATH = "Assets/_Project/Materials/M_Grenade.mat";

        // NOTE: AssetDatabase.CreateAsset は .physicsMaterial 拡張子を受け付けない。
        // 中身は同じ PhysicsMaterial なので .asset で作る。
        private const string PHYSICS_GRENADE_PATH = "Assets/_Project/Settings/Phys_Grenade.asset";

        private const string MATERIAL_TRAJECTORY_PATH = "Assets/_Project/Materials/M_Trajectory.mat";

        private const string MODEL_GRENADE_PATH = "Assets/_Project/Models/SM_Grenade.fbx";
        private const string TEXTURE_GRENADE_PATH = "Assets/_Project/Textures/T_Grenade_Albedo.png";

        private const string SPRITE_GAUGE_BACK_PATH = "Assets/_Project/Textures/UI/Spr_ChargeGauge_Back.png";
        private const string SPRITE_GAUGE_FILL_PATH = "Assets/_Project/Textures/UI/Spr_ChargeGauge_Fill.png";

        private const string NAME_CAMERA = "MainCamera";
        private const string NAME_LIGHT = "DirectionalLight_Sun";
        private const string NAME_GROUND = "Ground";
        private const string NAME_TARGETS = "Targets";
        private const string NAME_PLAYER = "Player";
        private const string NAME_MODEL = "Model";
        private const string NAME_TRAJECTORY = "TrajectoryLine";
        private const string NAME_CANVAS = "Canvas_Hud";
        private const string NAME_GAUGE = "ChargeGauge";
        private const string NAME_GAUGE_BACK = "GaugeBackground";
        private const string NAME_GAUGE_FILL = "GaugeFill";

        private const string SPRITES_DEFAULT_SHADER = "Sprites/Default";
        private const string URP_PARTICLES_UNLIT_SHADER = "Universal Render Pipeline/Particles/Unlit";

        /// <summary>予測線の太さ (m)。</summary>
        private const float TRAJECTORY_WIDTH = 0.07f;

        /// <summary>ゲージの一辺 (px)。参照解像度 1920x1080 でのサイズ。</summary>
        private const float GAUGE_SIZE = 170f;

        private const string TAG_MAIN_CAMERA = "MainCamera";

        private const string URP_LIT_SHADER = "Universal Render Pipeline/Lit";
        private const string BUILTIN_LIT_SHADER = "Standard";

        private const string PROP_BASE_COLOR = "_BaseColor";
        private const string PROP_COLOR = "_Color";
        private const string PROP_BASE_MAP = "_BaseMap";
        private const string PROP_MAIN_TEX = "_MainTex";

        /// <summary>デフォルトの Cylinder メッシュは半径 0.5 / 高さ 2。CharacterController をこれに合わせる。</summary>
        private const float PLAYER_HEIGHT = 2f;
        private const float PLAYER_RADIUS = 0.5f;

        /// <summary>Plane は 1 スケールで 10m 四方。5 倍して 50m 四方にする。</summary>
        private const float GROUND_SCALE = 5f;

        /// <summary>グレネードの最終的な直径 (m)。FBX の元サイズに関わらずこの大きさへ合わせる。</summary>
        private const float GRENADE_DIAMETER = 0.26f;

        private const float GRENADE_MASS = 0.45f;

        /// <summary>爆発確認用の的の並び。</summary>
        private const int TARGET_COLUMNS = 5;
        private const int TARGET_ROWS = 3;
        private const float TARGET_SIZE = 0.8f;
        private const float TARGET_DISTANCE = 7f;

        /// <summary>的同士の隙間 (m)。ぴったり接していると PhysX が押し合って勝手に震える。</summary>
        private const float TARGET_GAP = 0.04f;

        /// <summary>規約の表と同じ並び順。ヒエラルキー上段から順に並べる。</summary>
        private static readonly string[] GroupNames =
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
        private const int GROUP_INDEX_UI = 5;

        // ==========================================
        // Private Methods
        // ==========================================

        [MenuItem(MENU_BUILD_ALL, false, 0)]
        private static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            GameObject playerPrefab = BuildSharedAssets();
            if (playerPrefab == null)
            {
                return;
            }

            BuildScene(SCENE_MAIN_PATH, ThrowMode.Fixed, playerPrefab);
            BuildScene(SCENE_CHARGE_PATH, ThrowMode.Charge, playerPrefab);

            Debug.Log($"[MainSceneSetup] 完了: {SCENE_MAIN_PATH}（固定投擲）と {SCENE_CHARGE_PATH}（チャージ投擲）を構築した。");
        }

        [MenuItem(MENU_BUILD_MAIN, false, 20)]
        private static void BuildMainScene()
        {
            BuildSingleScene(SCENE_MAIN_PATH, ThrowMode.Fixed);
        }

        [MenuItem(MENU_BUILD_CHARGE, false, 21)]
        private static void BuildChargeScene()
        {
            BuildSingleScene(SCENE_CHARGE_PATH, ThrowMode.Charge);
        }

        private static void BuildSingleScene(string scenePath, ThrowMode mode)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            GameObject playerPrefab = BuildSharedAssets();
            if (playerPrefab == null)
            {
                return;
            }

            BuildScene(scenePath, mode, playerPrefab);
            Debug.Log($"[MainSceneSetup] 完了: {scenePath} を構築した。");
        }

        // ------------------------------------------
        // Assets
        // ------------------------------------------

        private static GameObject BuildSharedAssets()
        {
            PlayerData playerData = GetOrCreateAsset<PlayerData>(PLAYER_DATA_PATH);
            GrenadeData grenadeData = GetOrCreateAsset<GrenadeData>(GRENADE_DATA_PATH);

            GameObject grenadePrefab = BuildGrenadePrefab(grenadeData);
            if (grenadePrefab == null)
            {
                return null;
            }

            return BuildPlayerPrefab(playerData, grenadePrefab);
        }

        private static T GetOrCreateAsset<T>(string assetPath) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            // 既にある設定アセットは絶対に作り直さない。チームが詰めた数値を消すことになる。
            T created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, assetPath);
            Debug.Log($"[MainSceneSetup] {assetPath} を新規作成した。");
            return created;
        }

        private static GameObject BuildGrenadePrefab(GrenadeData grenadeData)
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MODEL_GRENADE_PATH);
            if (modelAsset == null)
            {
                Debug.LogError($"[MainSceneSetup] {MODEL_GRENADE_PATH} が見つからない。");
                return null;
            }

            ConfigureModelImporter();
            Material grenadeMaterial = BuildGrenadeMaterial();

            Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(MODEL_GRENADE_PATH).OfType<Mesh>().FirstOrDefault();
            if (mesh == null)
            {
                Debug.LogError($"[MainSceneSetup] {MODEL_GRENADE_PATH} にメッシュが無い。");
                return null;
            }

            // FBX は Maya 由来でセンチ基準。元の寸法に関わらず狙った直径へ合わせる。
            Bounds bounds = mesh.bounds;
            float longestSide = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float fitScale = longestSide > Mathf.Epsilon ? GRENADE_DIAMETER / longestSide : 1f;

            bool prefabExists = File.Exists(GRENADE_PREFAB_PATH);
            GameObject root = prefabExists
                ? PrefabUtility.LoadPrefabContents(GRENADE_PREFAB_PATH)
                : new GameObject("Grenade");

            // モデルを子に逃がすことで、ルートのスケールを 1 のまま保てる。
            // ルートを縮めると Rigidbody と Collider の挙動が見かけの大きさとずれる。
            Transform modelChild = root.transform.Find(NAME_MODEL);
            if (modelChild == null)
            {
                var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                modelInstance.name = NAME_MODEL;
                modelInstance.transform.SetParent(root.transform, false);
                modelChild = modelInstance.transform;
            }

            modelChild.localScale = Vector3.one * fitScale;
            modelChild.localPosition = -bounds.center * fitScale;
            modelChild.localRotation = Quaternion.identity;

            foreach (MeshRenderer renderer in modelChild.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterial = grenadeMaterial;
            }

            var body = GetOrAddComponent<Rigidbody>(root);
            body.mass = GRENADE_MASS;

            // 速い投擲だと離散判定では薄い床をすり抜ける。
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var sphere = GetOrAddComponent<SphereCollider>(root);
            sphere.radius = GRENADE_DIAMETER * 0.5f;
            sphere.center = Vector3.zero;
            sphere.sharedMaterial = BuildGrenadePhysicsMaterial();

            var grenade = GetOrAddComponent<Grenade>(root);
            SetObjectReference(grenade, "_data", grenadeData);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, GRENADE_PREFAB_PATH);

            if (prefabExists)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                Object.DestroyImmediate(root);
            }

            return savedPrefab;
        }

        private static void ConfigureModelImporter()
        {
            if (AssetImporter.GetAtPath(MODEL_GRENADE_PATH) is not ModelImporter importer)
            {
                return;
            }

            bool changed = false;

            if (importer.importAnimation)
            {
                importer.importAnimation = false;
                changed = true;
            }

            if (importer.importCameras || importer.importLights)
            {
                importer.importCameras = false;
                importer.importLights = false;
                changed = true;
            }

            // FBX 同梱のマテリアルは使わず、M_Grenade を自分で当てる。
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static Material BuildGrenadeMaterial()
        {
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_GRENADE_PATH);
            if (albedo == null)
            {
                Debug.LogWarning($"[MainSceneSetup] {TEXTURE_GRENADE_PATH} が見つからないので無地で作る。");
            }

            Material material = GetOrCreateMaterial(MATERIAL_GRENADE_PATH, Color.white);
            if (material == null || albedo == null)
            {
                return material;
            }

            if (material.HasProperty(PROP_BASE_MAP))
            {
                material.SetTexture(PROP_BASE_MAP, albedo);
            }

            if (material.HasProperty(PROP_MAIN_TEX))
            {
                material.SetTexture(PROP_MAIN_TEX, albedo);
            }

            // テクスチャ側で色が決まるので、乗算されるベースカラーは白へ戻す。
            if (material.HasProperty(PROP_BASE_COLOR))
            {
                material.SetColor(PROP_BASE_COLOR, Color.white);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static PhysicsMaterial BuildGrenadePhysicsMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PHYSICS_GRENADE_PATH);
            if (existing != null)
            {
                return existing;
            }

            var created = new PhysicsMaterial("Phys_Grenade")
            {
                bounciness = 0.35f,
                dynamicFriction = 0.4f,
                staticFriction = 0.4f,
                bounceCombine = PhysicsMaterialCombine.Average,
                frictionCombine = PhysicsMaterialCombine.Average,
            };

            AssetDatabase.CreateAsset(created, PHYSICS_GRENADE_PATH);
            return created;
        }

        private static GameObject BuildPlayerPrefab(PlayerData playerData, GameObject grenadePrefab)
        {
            bool prefabExists = File.Exists(PLAYER_PREFAB_PATH);
            GameObject root;

            if (prefabExists)
            {
                // 既存プレハブは中身を開いて編集する。作り直すとシーン上のインスタンスが切れる。
                root = PrefabUtility.LoadPrefabContents(PLAYER_PREFAB_PATH);
            }
            else
            {
                root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                root.name = NAME_PLAYER;
            }

            // Cylinder には CapsuleCollider が付いてくる。CharacterController と二重に当たるので外す。
            Collider primitiveCollider = root.GetComponent<Collider>();
            if (primitiveCollider != null && primitiveCollider is not CharacterController)
            {
                Object.DestroyImmediate(primitiveCollider, true);
            }

            var characterController = GetOrAddComponent<CharacterController>(root);
            characterController.height = PLAYER_HEIGHT;
            characterController.radius = PLAYER_RADIUS;
            characterController.center = Vector3.zero;
            characterController.slopeLimit = 45f;
            characterController.stepOffset = 0.3f;
            characterController.skinWidth = 0.08f;

            // 既定値 0.001 だと微小な入力が切り捨てられて動き出しが鈍る。
            characterController.minMoveDistance = 0f;

            // PlayerInputReader は両スクリプトの [RequireComponent] で自動的に付く。
            var controller = GetOrAddComponent<PlayerController>(root);
            SetObjectReference(controller, "_data", playerData);

            var thrower = GetOrAddComponent<PlayerThrower>(root);
            SetObjectReference(thrower, "_data", playerData);
            SetObjectReference(thrower, "_grenadePrefab", grenadePrefab);
            SetEnum(thrower, "_mode", (int)ThrowMode.Fixed);

            var renderer = root.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetOrCreateMaterial(MATERIAL_PLAYER_PATH, new Color(0.25f, 0.6f, 0.95f));
            }

            BuildTrajectoryLine(root, thrower);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PLAYER_PREFAB_PATH);

            if (prefabExists)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                Object.DestroyImmediate(root);
            }

            return savedPrefab;
        }

        private static void BuildTrajectoryLine(GameObject playerRoot, PlayerThrower thrower)
        {
            Transform existing = playerRoot.transform.Find(NAME_TRAJECTORY);
            GameObject lineObject;

            if (existing != null)
            {
                lineObject = existing.gameObject;
            }
            else
            {
                lineObject = new GameObject(NAME_TRAJECTORY);
                lineObject.transform.SetParent(playerRoot.transform, false);
            }

            // 線はワールド座標で引くので、親の回転を受けないようローカルは原点のままにする。
            lineObject.transform.localPosition = Vector3.zero;
            lineObject.transform.localRotation = Quaternion.identity;
            lineObject.transform.localScale = Vector3.one;

            var line = GetOrAddComponent<LineRenderer>(lineObject);
            line.useWorldSpace = true;
            line.widthMultiplier = TRAJECTORY_WIDTH;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.positionCount = 0;
            line.sharedMaterial = BuildTrajectoryMaterial();

            var view = GetOrAddComponent<ThrowTrajectoryView>(lineObject);
            SetObjectReference(view, "_thrower", thrower);
        }

        private static Material BuildTrajectoryMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_TRAJECTORY_PATH);
            if (existing != null)
            {
                return existing;
            }

            // LineRenderer の startColor / endColor は頂点カラーなので、
            // 頂点カラーを乗せる半透明シェーダーでないと色が出ない。
            Shader shader = Shader.Find(SPRITES_DEFAULT_SHADER) ?? Shader.Find(URP_PARTICLES_UNLIT_SHADER);
            if (shader == null)
            {
                Debug.LogWarning($"[MainSceneSetup] 予測線用のシェーダーが見つからなかった。");
                return null;
            }

            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, MATERIAL_TRAJECTORY_PATH);
            return material;
        }

        private static Sprite ImportGaugeSprite(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                bool changed = false;

                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    changed = true;
                }

                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }

                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    changed = true;
                }

                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    changed = true;
                }

                if (importer.wrapMode != TextureWrapMode.Clamp)
                {
                    importer.wrapMode = TextureWrapMode.Clamp;
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                Debug.LogError($"[MainSceneSetup] {assetPath} をスプライトとして読めなかった。");
            }

            return sprite;
        }

        // ------------------------------------------
        // Scene
        // ------------------------------------------

        private static void BuildScene(string scenePath, ThrowMode mode, GameObject playerPrefab)
        {
            Scene scene = OpenOrCreateScene(scenePath);
            if (!scene.IsValid())
            {
                Debug.LogError($"[MainSceneSetup] シーンを開けなかった: {scenePath}");
                return;
            }

            var groups = new GameObject[GroupNames.Length];
            for (int i = 0; i < GroupNames.Length; i++)
            {
                groups[i] = FindOrCreateRoot(GroupNames[i]);
            }

            Camera mainCamera = SetUpCamera(groups[GROUP_INDEX_CAMERAS]);
            SetUpLight(groups[GROUP_INDEX_LIGHTS]);
            SetUpGround(groups[GROUP_INDEX_ENVIRONMENT]);
            SetUpTargets(groups[GROUP_INDEX_ENVIRONMENT]);
            GameObject player = SetUpPlayer(groups[GROUP_INDEX_ACTORS], mainCamera, playerPrefab, mode);
            SetUpHud(groups[GROUP_INDEX_UI], mainCamera, player);

            // 先頭から順に末尾へ送ると、結果として配列と同じ並びになる。
            foreach (GameObject group in groups)
            {
                group.transform.SetAsLastSibling();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static Scene OpenOrCreateScene(string scenePath)
        {
            if (File.Exists(scenePath))
            {
                return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            Scene created = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(created, scenePath);
            Debug.Log($"[MainSceneSetup] {scenePath} を新規作成した。");
            return created;
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

            return new GameObject(name);
        }

        private static Camera SetUpCamera(GameObject parent)
        {
            Camera[] found = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Camera camera = found.Length > 0 ? found[0] : null;

            if (camera == null)
            {
                var created = new GameObject(NAME_CAMERA);
                camera = created.AddComponent<Camera>();
                created.AddComponent<AudioListener>();
                created.tag = TAG_MAIN_CAMERA;
            }

            camera.gameObject.name = NAME_CAMERA;
            camera.transform.SetParent(parent.transform, false);

            // プレイヤーと的の両方が入る定点。追従は後で Cinemachine に任せる想定。
            camera.transform.localPosition = new Vector3(0f, 7f, -10f);
            camera.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            camera.transform.localScale = Vector3.one;

            return camera;
        }

        private static void SetUpLight(GameObject parent)
        {
            Light[] found = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Light directional = found.FirstOrDefault(light => light.type == LightType.Directional);

            if (directional == null)
            {
                var created = new GameObject(NAME_LIGHT);
                directional = created.AddComponent<Light>();
                directional.type = LightType.Directional;
            }

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
                ground.transform.SetParent(parent.transform, false);
            }

            ground.transform.localPosition = Vector3.zero;
            ground.transform.localRotation = Quaternion.identity;
            ground.transform.localScale = new Vector3(GROUND_SCALE, 1f, GROUND_SCALE);

            ApplyMaterial(ground, MATERIAL_GROUND_PATH, new Color(0.22f, 0.24f, 0.27f));
        }

        private static void SetUpTargets(GameObject parent)
        {
            Transform existing = parent.transform.Find(NAME_TARGETS);
            GameObject targets;

            if (existing != null)
            {
                // 積み直しのたびに増えると困るので、一度中身を空にしてから積む。
                targets = existing.gameObject;
                for (int i = targets.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(targets.transform.GetChild(i).gameObject);
                }
            }
            else
            {
                targets = new GameObject(NAME_TARGETS);
                targets.transform.SetParent(parent.transform, false);
            }

            targets.transform.localPosition = Vector3.zero;

            Material material = GetOrCreateMaterial(MATERIAL_TARGET_PATH, new Color(0.9f, 0.45f, 0.15f));
            float pitch = TARGET_SIZE + TARGET_GAP;
            float startX = -(TARGET_COLUMNS - 1) * pitch * 0.5f;

            for (int row = 0; row < TARGET_ROWS; row++)
            {
                for (int column = 0; column < TARGET_COLUMNS; column++)
                {
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = $"Target_{row:00}_{column:00}";
                    cube.transform.SetParent(targets.transform, false);
                    cube.transform.localScale = Vector3.one * TARGET_SIZE;
                    cube.transform.localPosition = new Vector3(
                        startX + column * pitch,
                        TARGET_SIZE * 0.5f + row * pitch,
                        TARGET_DISTANCE);

                    var body = cube.AddComponent<Rigidbody>();
                    body.mass = 1f;

                    cube.GetComponent<MeshRenderer>().sharedMaterial = material;
                }
            }
        }

        private static GameObject SetUpPlayer(GameObject parent, Camera mainCamera, GameObject playerPrefab, ThrowMode mode)
        {
            Transform existing = parent.transform.Find(NAME_PLAYER);
            GameObject player;

            if (existing != null)
            {
                player = existing.gameObject;
            }
            else
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.name = NAME_PLAYER;
                player.transform.SetParent(parent.transform, false);
            }

            player.transform.localPosition = new Vector3(0f, PLAYER_HEIGHT * 0.5f, 0f);
            player.transform.localRotation = Quaternion.identity;
            player.transform.localScale = Vector3.one;

            // カメラはシーン内の参照なのでプレハブには持たせられない。ここでインスタンス側に差す。
            var controller = player.GetComponent<PlayerController>();
            if (controller != null)
            {
                SetObjectReference(controller, "_cameraTransform", mainCamera != null ? mainCamera.transform : null);
            }

            // 投擲モードはシーンごとの違いなので、プレハブ本体ではなくインスタンス側で上書きする。
            var thrower = player.GetComponent<PlayerThrower>();
            if (thrower != null)
            {
                SetEnum(thrower, "_mode", (int)mode);
            }

            return player;
        }

        private static void SetUpHud(GameObject parent, Camera mainCamera, GameObject player)
        {
            var thrower = player.GetComponent<PlayerThrower>();
            if (thrower == null)
            {
                Debug.LogWarning($"[MainSceneSetup] {NAME_PLAYER} に PlayerThrower が無いので HUD を作らない。");
                return;
            }

            GameObject canvasObject = FindOrCreateUIChild(parent.transform, NAME_CANVAS);

            var canvas = GetOrAddComponent<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = GetOrAddComponent<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GetOrAddComponent<GraphicRaycaster>(canvasObject);

            // ゲージは毎回作り直す。残したままだと、スクリプト側で既定値を直しても
            // シーンに保存済みの古い色やサイズが残り続けて反映されない。
            Transform staleGauge = canvasObject.transform.Find(NAME_GAUGE);
            if (staleGauge != null)
            {
                Object.DestroyImmediate(staleGauge.gameObject);
            }

            GameObject gauge = FindOrCreateUIChild(canvasObject.transform, NAME_GAUGE);
            var gaugeRect = (RectTransform)gauge.transform;
            gaugeRect.anchorMin = new Vector2(0.5f, 0.5f);
            gaugeRect.anchorMax = new Vector2(0.5f, 0.5f);
            gaugeRect.pivot = new Vector2(0.5f, 0.5f);
            gaugeRect.sizeDelta = new Vector2(GAUGE_SIZE, GAUGE_SIZE);
            gaugeRect.anchoredPosition = Vector2.zero;

            Image background = BuildGaugeImage(gaugeRect, NAME_GAUGE_BACK, SPRITE_GAUGE_BACK_PATH);
            Image fill = BuildGaugeImage(gaugeRect, NAME_GAUGE_FILL, SPRITE_GAUGE_FILL_PATH);

            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = 0f;

            var gaugeUI = GetOrAddComponent<ChargeGaugeUI>(gauge);
            SetObjectReference(gaugeUI, "_thrower", thrower);
            SetObjectReference(gaugeUI, "_background", background);
            SetObjectReference(gaugeUI, "_fill", fill);
            SetObjectReference(gaugeUI, "_camera", mainCamera);
        }

        private static Image BuildGaugeImage(RectTransform parent, string name, string spritePath)
        {
            GameObject imageObject = FindOrCreateUIChild(parent, name);
            var rect = (RectTransform)imageObject.transform;

            // 親いっぱいに広げる。ゲージの大きさは親の sizeDelta だけで決まるようにする。
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = GetOrAddComponent<Image>(imageObject);
            image.sprite = ImportGaugeSprite(spritePath);
            image.preserveAspect = true;
            image.raycastTarget = false;

            return image;
        }

        private static GameObject FindOrCreateUIChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return created;
        }

        // ------------------------------------------
        // Helpers
        // ------------------------------------------

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : target.AddComponent<T>();
        }

        private static void SetObjectReference(Component target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);

            if (property == null)
            {
                Debug.LogWarning($"[MainSceneSetup] {target.GetType().Name} に {propertyName} が無い。");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static void SetEnum(Component target, string propertyName, int value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);

            if (property == null)
            {
                Debug.LogWarning($"[MainSceneSetup] {target.GetType().Name} に {propertyName} が無い。");
                return;
            }

            property.enumValueIndex = value;
            serialized.ApplyModifiedProperties();
        }

        private static void ApplyMaterial(GameObject target, string assetPath, Color color)
        {
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                return;
            }

            Material material = GetOrCreateMaterial(assetPath, color);
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
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
    }
}

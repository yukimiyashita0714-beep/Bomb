using System.IO;
using Bomb.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bomb.Editors
{
    /// <summary>
    /// 爆発エフェクト Vfx_Explosion と、その材料（ソフトパーティクル用テクスチャ・マテリアル）を
    /// 規約どおりの名前で組み立て、P_Grenade に配線するエディタ専用ツール。
    /// 既にあるアセットは作り直さない。チームが Inspector で詰めた値を消さないため。
    /// </summary>
    public static class ExplosionVfxSetup
    {
        // ==========================================
        // Private Fields
        // ==========================================

        private const string MENU_BUILD_VFX = "Bomb/Setup/Build Vfx_Explosion";

        private const string TEXTURE_SOFT_PATH = "Assets/_Project/Textures/T_Vfx_Soft.png";
        private const string MATERIAL_FIRE_PATH = "Assets/_Project/Materials/M_Vfx_Explosion_Fire.mat";
        private const string MATERIAL_SMOKE_PATH = "Assets/_Project/Materials/M_Vfx_Explosion_Smoke.mat";
        private const string VFX_PREFAB_PATH = "Assets/_Project/Prefabs/Vfx_Explosion.prefab";
        private const string GRENADE_PREFAB_PATH = "Assets/_Project/Prefabs/P_Grenade.prefab";

        private const string URP_PARTICLES_UNLIT_SHADER = "Universal Render Pipeline/Particles/Unlit";
        private const string SPRITES_DEFAULT_SHADER = "Sprites/Default";

        private const string PROP_BASE_MAP = "_BaseMap";
        private const string PROP_MAIN_TEX = "_MainTex";
        private const string PROP_BASE_COLOR = "_BaseColor";
        private const string PROP_SURFACE = "_Surface";
        private const string PROP_BLEND = "_Blend";
        private const string PROP_ZWRITE = "_ZWrite";
        private const string PROP_SRC_BLEND = "_SrcBlend";
        private const string PROP_DST_BLEND = "_DstBlend";

        private const string KEYWORD_SURFACE_TRANSPARENT = "_SURFACE_TYPE_TRANSPARENT";

        private const float SURFACE_TRANSPARENT = 1f;
        private const float BLEND_ALPHA = 0f;
        private const float BLEND_ADDITIVE = 2f;

        private const string NAME_VFX_ROOT = "Vfx_Explosion";
        private const string NAME_FLASH = "Flash";
        private const string NAME_FIREBALL = "Fireball";
        private const string NAME_SMOKE = "Smoke";
        private const string NAME_SPARKS = "Sparks";

        private const string FIELD_VFX_PREFAB = "_explosionVfxPrefab";
        private const string FIELD_VFX_BASE_RADIUS = "_vfxBaseRadiusMeters";

        /// <summary>生成するエフェクトが等倍で想定する爆発半径 (m)。GrenadeExplosionView にそのまま渡す。</summary>
        private const float VFX_BASE_RADIUS_METERS = 5f;

        private const int SOFT_TEXTURE_SIZE = 128;

        private const short BURST_FIREBALL = 14;
        private const short BURST_SMOKE = 12;
        private const short BURST_SPARKS = 28;

        // ==========================================
        // Private Methods
        // ==========================================

        [MenuItem(MENU_BUILD_VFX, false, 40)]
        private static void BuildExplosionVfx()
        {
            Texture2D softTexture = BuildSoftTexture();
            if (softTexture == null)
            {
                return;
            }

            Material fireMaterial = BuildParticleMaterial(MATERIAL_FIRE_PATH, softTexture, true);
            Material smokeMaterial = BuildParticleMaterial(MATERIAL_SMOKE_PATH, softTexture, false);

            if (fireMaterial == null || smokeMaterial == null)
            {
                return;
            }

            GameObject vfxPrefab = BuildVfxPrefab(fireMaterial, smokeMaterial);
            if (vfxPrefab == null)
            {
                return;
            }

            AttachToGrenadePrefab(vfxPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[{nameof(ExplosionVfxSetup)}] {VFX_PREFAB_PATH} を用意し、P_Grenade に配線した。");
        }

        // ------------------------------------------
        // Assets
        // ------------------------------------------

        /// <summary>中心が白く縁へ向かって透けていく丸テクスチャ。炎にも煙にも使い回す。</summary>
        private static Texture2D BuildSoftTexture()
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_SOFT_PATH);
            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(SOFT_TEXTURE_SIZE, SOFT_TEXTURE_SIZE, TextureFormat.RGBA32, false);
            float half = SOFT_TEXTURE_SIZE * 0.5f;

            for (int y = 0; y < SOFT_TEXTURE_SIZE; y++)
            {
                for (int x = 0; x < SOFT_TEXTURE_SIZE; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy));

                    // 二乗して中心を濃く、縁をなだらかに。線形だと輪郭が出て板に見える。
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha;

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(TEXTURE_SOFT_PATH));
            File.WriteAllBytes(TEXTURE_SOFT_PATH, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(TEXTURE_SOFT_PATH, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(TEXTURE_SOFT_PATH) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_SOFT_PATH);
        }

        private static Material BuildParticleMaterial(string assetPath, Texture2D texture, bool isAdditive)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find(URP_PARTICLES_UNLIT_SHADER) ?? Shader.Find(SPRITES_DEFAULT_SHADER);
            if (shader == null)
            {
                Debug.LogError($"[{nameof(ExplosionVfxSetup)}] パーティクル用シェーダが見つからない。");
                return null;
            }

            var material = new Material(shader);

            if (material.HasProperty(PROP_BASE_MAP))
            {
                material.SetTexture(PROP_BASE_MAP, texture);
            }

            if (material.HasProperty(PROP_MAIN_TEX))
            {
                material.SetTexture(PROP_MAIN_TEX, texture);
            }

            if (material.HasProperty(PROP_BASE_COLOR))
            {
                material.SetColor(PROP_BASE_COLOR, Color.white);
            }

            // URP の Particles/Unlit は Inspector 側で Surface と Blend を切り替える作りなので、
            // コードから作るときはブレンド係数とキーワードも自分で合わせる必要がある。
            if (material.HasProperty(PROP_SURFACE))
            {
                material.SetFloat(PROP_SURFACE, SURFACE_TRANSPARENT);
                material.SetFloat(PROP_BLEND, isAdditive ? BLEND_ADDITIVE : BLEND_ALPHA);
                material.SetFloat(PROP_ZWRITE, 0f);
                material.SetFloat(PROP_SRC_BLEND, (float)BlendMode.SrcAlpha);
                material.SetFloat(
                    PROP_DST_BLEND,
                    isAdditive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword(KEYWORD_SURFACE_TRANSPARENT);
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        // ------------------------------------------
        // Prefab
        // ------------------------------------------

        private static GameObject BuildVfxPrefab(Material fireMaterial, Material smokeMaterial)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(VFX_PREFAB_PATH);
            if (existing != null)
            {
                Debug.Log($"[{nameof(ExplosionVfxSetup)}] {VFX_PREFAB_PATH} は既にあるので作り直さない。");
                return existing;
            }

            var root = new GameObject(NAME_VFX_ROOT);

            BuildFlash(root, fireMaterial);
            BuildFireball(root, fireMaterial);
            BuildSmoke(root, smokeMaterial);
            BuildSparks(root, fireMaterial);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, VFX_PREFAB_PATH);
            Object.DestroyImmediate(root);

            return saved;
        }

        /// <summary>爆心が一瞬だけ白く光る。これが無いと爆発の「立ち上がり」が見えない。</summary>
        private static void BuildFlash(GameObject root, Material material)
        {
            ParticleSystem system = CreateChildSystem(root, NAME_FLASH, material, -5f);

            ParticleSystem.MainModule main = system.main;
            main.duration = 0.3f;
            main.startLifetime = 0.14f;
            main.startSpeed = 0f;
            main.startSize = 4.5f;
            main.startColor = new Color(1f, 0.95f, 0.75f, 1f);
            main.maxParticles = 4;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            SetSizeOverLifetime(system, new AnimationCurve(
                new Keyframe(0f, 0.3f),
                new Keyframe(0.3f, 1f),
                new Keyframe(1f, 1.1f)));

            SetColorOverLifetime(
                system,
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.5f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(0f, 1f),
                });
        }

        /// <summary>火球。外へ広がりながら黄色→橙→暗い赤へ落ちていく。</summary>
        private static void BuildFireball(GameObject root, Material material)
        {
            ParticleSystem system = CreateChildSystem(root, NAME_FIREBALL, material, 0f);

            ParticleSystem.MainModule main = system.main;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.maxParticles = 40;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, BURST_FIREBALL) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            shape.radiusThickness = 1f;

            SetDrag(system, 2.5f);

            SetSizeOverLifetime(system, new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(1f, 1.15f)));

            SetColorOverLifetime(
                system,
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f),
                    new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.35f),
                    new GradientColorKey(new Color(0.35f, 0.08f, 0.02f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.5f),
                    new GradientAlphaKey(0f, 1f),
                });
        }

        /// <summary>煙。火球より長く残り、ゆっくり上へ昇って消える。</summary>
        private static void BuildSmoke(GameObject root, Material material)
        {
            ParticleSystem system = CreateChildSystem(root, NAME_SMOKE, material, 10f);

            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;

            // 負の重力で浮き上がらせる。VelocityOverLifetime より調整する値が少ない。
            main.gravityModifier = -0.05f;
            main.maxParticles = 40;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, BURST_SMOKE) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;
            shape.radiusThickness = 1f;

            SetDrag(system, 1.5f);

            SetSizeOverLifetime(system, new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(1f, 1.5f)));

            SetColorOverLifetime(
                system,
                new[]
                {
                    new GradientColorKey(new Color(0.35f, 0.33f, 0.31f), 0f),
                    new GradientColorKey(new Color(0.18f, 0.17f, 0.16f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.55f, 0.15f),
                    new GradientAlphaKey(0f, 1f),
                });
        }

        /// <summary>火の粉。尾を引く点が散ることで爆発の勢いが出る。</summary>
        private static void BuildSparks(GameObject root, Material material)
        {
            ParticleSystem system = CreateChildSystem(root, NAME_SPARKS, material, -10f);

            ParticleSystem.MainModule main = system.main;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 13f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.18f);
            main.startColor = Color.white;
            main.gravityModifier = 1.2f;
            main.maxParticles = 60;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, BURST_SPARKS) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            shape.radiusThickness = 1f;

            SetDrag(system, 1f);

            SetColorOverLifetime(
                system,
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.9f, 0.55f), 0f),
                    new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f),
                });

            var psRenderer = system.GetComponent<ParticleSystemRenderer>();
            psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            psRenderer.lengthScale = 3f;
            psRenderer.velocityScale = 0.06f;
        }

        // ------------------------------------------
        // Particle Helpers
        // ------------------------------------------

        private static ParticleSystem CreateChildSystem(
            GameObject root,
            string name,
            Material material,
            float sortingFudge)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);

            var system = child.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = true;

            // GrenadeExplosionView がルートのスケールで爆発半径に合わせるため、
            // 親のスケールを拾う Hierarchy にしておく。Local だと親の拡大が効かない。
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var psRenderer = system.GetComponent<ParticleSystemRenderer>();
            psRenderer.sharedMaterial = material;
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;

            // 煙を奥、火花を手前に固定する。同じ位置だとカメラ角度で前後が入れ替わってちらつく。
            psRenderer.sortingFudge = sortingFudge;
            psRenderer.shadowCastingMode = ShadowCastingMode.Off;
            psRenderer.receiveShadows = false;

            return system;
        }

        private static void SetSizeOverLifetime(ParticleSystem system, AnimationCurve curve)
        {
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void SetColorOverLifetime(
            ParticleSystem system,
            GradientColorKey[] colorKeys,
            GradientAlphaKey[] alphaKeys)
        {
            var gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>飛び出した粒を減速させる。等速のままだと破片が飛んでいくように見える。</summary>
        private static void SetDrag(ParticleSystem system, float drag)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0f;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = true;
        }

        // ------------------------------------------
        // Wiring
        // ------------------------------------------

        private static void AttachToGrenadePrefab(GameObject vfxPrefab)
        {
            if (!File.Exists(GRENADE_PREFAB_PATH))
            {
                Debug.LogError($"[{nameof(ExplosionVfxSetup)}] {GRENADE_PREFAB_PATH} が無い。先に Build All を実行する。");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(GRENADE_PREFAB_PATH);

            try
            {
                var view = root.GetComponent<GrenadeExplosionView>();
                if (view == null)
                {
                    view = root.AddComponent<GrenadeExplosionView>();
                }

                var serialized = new SerializedObject(view);
                serialized.FindProperty(FIELD_VFX_PREFAB).objectReferenceValue = vfxPrefab;
                serialized.FindProperty(FIELD_VFX_BASE_RADIUS).floatValue = VFX_BASE_RADIUS_METERS;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, GRENADE_PREFAB_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

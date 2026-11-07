using UnityEditor;
using UnityEngine;

/// <summary>
/// グレネードの爆発エフェクト (Vfx_Explosion) を派手にする設定を、メニュー1回で入れるエディタ拡張。
/// パーティクルの設定はプレハブの YAML だと数千行あって手で直すと壊しやすいので、API から書き換える。
/// 既存の Flash / Fireball / Smoke / Sparks の数値を上げ、衝撃波・破片・砂ぼこりを足し、光を付ける。
/// 値はすべて上書きで入れ、足す物は前のものを消してから作り直すので、何度実行しても同じ結果になる。
/// </summary>
public static class ExplosionVfxSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_PATH = "Bomb/Setup/Explosion Vfx";
    private const string VFX_PREFAB_PATH = "Assets/_Project/Prefabs/Vfx_Explosion.prefab";

    private const string FLASH_NAME = "Flash";
    private const string FIREBALL_NAME = "Fireball";
    private const string SMOKE_NAME = "Smoke";
    private const string SPARKS_NAME = "Sparks";

    private const string SHOCKWAVE_NAME = "Shockwave";
    private const string DEBRIS_NAME = "Debris";
    private const string GROUND_DUST_NAME = "GroundDust";

    /// <summary>砂ぼこりを地面に沿って広げるため、放出の円を寝かせる角度 (度)。</summary>
    private const float GROUND_SHAPE_ANGLE = -90f;

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH)]
    private static void Setup()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(VFX_PREFAB_PATH);

        if (root == null)
        {
            ShowError($"プレハブが見つからない: {VFX_PREFAB_PATH}");
            return;
        }

        try
        {
            ParticleSystem flash = FindSystem(root, FLASH_NAME);
            ParticleSystem fireball = FindSystem(root, FIREBALL_NAME);
            ParticleSystem smoke = FindSystem(root, SMOKE_NAME);
            ParticleSystem sparks = FindSystem(root, SPARKS_NAME);

            if (flash == null || fireball == null || smoke == null || sparks == null)
            {
                ShowError("Vfx_Explosion に Flash / Fireball / Smoke / Sparks のどれかが無い。");
                return;
            }

            RemoveChild(root, SHOCKWAVE_NAME);
            RemoveChild(root, DEBRIS_NAME);
            RemoveChild(root, GROUND_DUST_NAME);

            TuneFlash(flash);
            TuneFireball(fireball);
            TuneSmoke(smoke);
            TuneSparks(sparks);

            // 新しい物は、見た目の近い既存のものを複製して作る。マテリアルやテクスチャの設定を引き継げる。
            SetupShockwave(Duplicate(flash, SHOCKWAVE_NAME));
            SetupDebris(Duplicate(sparks, DEBRIS_NAME), smoke);
            SetupGroundDust(Duplicate(smoke, GROUND_DUST_NAME));

            if (!root.TryGetComponent(out ExplosionLightFlash _))
            {
                root.AddComponent<ExplosionLightFlash>();
            }

            PrefabUtility.SaveAsPrefabAsset(root, VFX_PREFAB_PATH);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        EditorUtility.DisplayDialog("Explosion Vfx Setup", "爆発エフェクトを派手にした。", "OK");
    }

    // ------------------------------------------
    // 既存の調整
    // ------------------------------------------

    /// <summary>一瞬の白い光。大きく、少し長く光らせて爆発の始まりを強くする。</summary>
    private static void TuneFlash(ParticleSystem system)
    {
        ParticleSystem.MainModule main = system.main;
        main.startLifetime = 0.2f;
        main.startSize = 8f;
        main.startColor = new Color(1f, 0.95f, 0.8f, 1f);
    }

    /// <summary>火の玉。数と大きさを増やし、白→黄→橙→赤黒と熱い色から冷めていくようにする。</summary>
    private static void TuneFireball(ParticleSystem system)
    {
        ParticleSystem.MainModule main = system.main;
        main.duration = 0.6f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 12f);
        main.startSize = new ParticleSystem.MinMaxCurve(3f, 5.5f);
        main.startColor = Color.white;
        main.maxParticles = 80;

        SetBurstCount(system, 32);

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = MakeGradient(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.85f), 0f),
                new GradientColorKey(new Color(1f, 0.75f, 0.2f), 0.2f),
                new GradientColorKey(new Color(1f, 0.35f, 0.05f), 0.5f),
                new GradientColorKey(new Color(0.35f, 0.08f, 0.03f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.9f, 0.5f),
                new GradientAlphaKey(0f, 1f),
            });
    }

    /// <summary>煙。厚く、長く残して爆発の後の余韻を作る。</summary>
    private static void TuneSmoke(ParticleSystem system)
    {
        ParticleSystem.MainModule main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(4f, 6.5f);
        main.maxParticles = 60;

        SetBurstCount(system, 22);
    }

    /// <summary>火花。数と勢いを増やし、重力で弧を描いて落ちるようにする。</summary>
    private static void TuneSparks(ParticleSystem system)
    {
        ParticleSystem.MainModule main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(12f, 24f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.gravityModifier = 2f;
        main.maxParticles = 160;

        SetBurstCount(system, 80);
    }

    // ------------------------------------------
    // 追加するもの
    // ------------------------------------------

    /// <summary>地面と平行に一気に広がって消える輪。爆風が届く範囲を見せる。</summary>
    private static void SetupShockwave(ParticleSystem system)
    {
        ParticleSystem.MainModule main = system.main;
        main.duration = 0.4f;
        main.startLifetime = 0.35f;
        main.startSpeed = 0f;
        main.startSize = 14f;
        main.startColor = new Color(1f, 0.85f, 0.6f, 0.8f);
        main.maxParticles = 1;

        SetBurstCount(system, 1);

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(new Keyframe(0f, 0.1f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f)));

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = MakeGradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });

        // 画面に向けず地面に寝かせて描くと、輪が横に広がって見える。
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
    }

    /// <summary>飛び散る黒っぽい破片。火花より大きく重く、ゆっくり落ちる。</summary>
    private static void SetupDebris(ParticleSystem system, ParticleSystem smoke)
    {
        ParticleSystem.MainModule main = system.main;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 14f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.18f, 0.15f, 0.13f, 1f);
        main.gravityModifier = 2.5f;
        main.maxParticles = 30;

        SetBurstCount(system, 22);

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = MakeGradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });

        // 火花は光る (加算) マテリアルなので、暗い破片が見えない。煙のマテリアルを借りる。
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial;
    }

    /// <summary>地面をはうように横へ広がる砂ぼこり。</summary>
    private static void SetupGroundDust(ParticleSystem system)
    {
        ParticleSystem.MainModule main = system.main;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSize = new ParticleSystem.MinMaxCurve(2f, 3.5f);
        main.startColor = new Color(0.62f, 0.55f, 0.45f, 0.8f);
        main.gravityModifier = 0f;
        main.maxParticles = 30;

        SetBurstCount(system, 20);

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.5f;
        shape.rotation = new Vector3(GROUND_SHAPE_ANGLE, 0f, 0f);

        // 横に飛ばしたまま止まらないと、煙が輪のまま遠くへ流れていってしまう。
        ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 1f;
        limit.dampen = 0.15f;
    }

    // ------------------------------------------
    // 共通
    // ------------------------------------------

    private static ParticleSystem FindSystem(GameObject root, string childName)
    {
        Transform child = root.transform.Find(childName);
        return child != null ? child.GetComponent<ParticleSystem>() : null;
    }

    private static void RemoveChild(GameObject root, string childName)
    {
        Transform child = root.transform.Find(childName);

        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static ParticleSystem Duplicate(ParticleSystem source, string newName)
    {
        GameObject copy = Object.Instantiate(source.gameObject, source.transform.parent);
        copy.name = newName;
        return copy.GetComponent<ParticleSystem>();
    }

    /// <summary>最初のバースト (爆発の瞬間にまとめて出す数) を変える。無ければ作る。</summary>
    private static void SetBurstCount(ParticleSystem system, int count)
    {
        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        if (emission.burstCount == 0)
        {
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            return;
        }

        ParticleSystem.Burst burst = emission.GetBurst(0);
        burst.count = count;
        emission.SetBurst(0, burst);
    }

    private static Gradient MakeGradient(GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }

    private static void ShowError(string message)
    {
        Debug.LogError($"[{nameof(ExplosionVfxSetupMenu)}] {message}");
        EditorUtility.DisplayDialog("Explosion Vfx Setup", message, "OK");
    }
}

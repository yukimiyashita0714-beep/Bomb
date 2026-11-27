using System;
using UnityEngine;

/// <summary>
/// 敵にグレネードが当たったことを、プレイヤーに分かるように伝える。
/// ・当たった音を鳴らす (倒れる1発は別の音)
/// ・残りが少なくなった敵から、倒れるまで煙と火花を出し続ける
/// ・<see cref="OnAnyEnemyHit"/> でコントローラーの振動 (EnemyHitRumble) に知らせる
/// <see cref="EnemyHealth"/> のイベントだけを聞くので、どの種類の敵にも付けられる。
/// 倒れたときの見た目は <see cref="EnemyDeathView"/> に任せ、ここでは止めるだけにする。
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
[DisallowMultipleComponent]
public class EnemyHitFeedback : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("音と傷ついた見た目の調整値")]
    private EnemyHitFeedbackData _data;

    // ==========================================
    // Private Fields
    // ==========================================

    private EnemyHealth _health;

    /// <summary>今出している傷ついた見た目。出していなければ null。</summary>
    private GameObject _damagedEffectInstance;

    // ==========================================
    // Events
    // ==========================================

    /// <summary>
    /// どれかの敵に当たった瞬間に発火する。コントローラーの振動はこれを聞く。
    /// 引数はその敵の調整値と、倒れる1発なら true。
    /// 敵はシーンにいくつも出入りするので、UI 側が1体ずつ登録しなくて済むよう static にしている。
    /// </summary>
    public static event Action<EnemyHitFeedbackData, bool> OnAnyEnemyHit;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        _health = GetComponent<EnemyHealth>();

        if (_data == null)
        {
            Debug.LogError($"[{nameof(EnemyHitFeedback)}] EnemyHitFeedbackData が未設定のため動作できない。", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        _health.OnDamaged += HandleDamaged;
        _health.OnDied += HandleDied;
        _health.OnRevived += HandleRevived;
    }

    private void OnDisable()
    {
        _health.OnDamaged -= HandleDamaged;
        _health.OnDied -= HandleDied;
        _health.OnRevived -= HandleRevived;

        // 親ごと消える途中や非表示になる途中は親子関係を変えられないので、ここではすぐ消す。
        if (_damagedEffectInstance != null)
        {
            Destroy(_damagedEffectInstance);
            _damagedEffectInstance = null;
        }
    }

    // ==========================================
    // Private Methods
    // ==========================================

    /// <summary>
    /// OnDamaged は倒れる1発でも OnDied より先に来る。
    /// そのため倒れたかどうかは IsDead ではなく残りの回数で見分ける。
    /// </summary>
    private void HandleDamaged()
    {
        bool isKillingHit = _health.RemainingHitCount <= 0;

        SEEvents.PlayOneShot(isKillingHit ? _data.KillSe : _data.HitSe, transform, _data.SeVolume);
        OnAnyEnemyHit?.Invoke(_data, isKillingHit);

        if (!isKillingHit && _health.RemainingHitCount <= _data.DamagedEffectRemainingHitCount)
        {
            StartDamagedEffect();
        }
    }

    private void HandleDied()
    {
        StopDamagedEffect();
    }

    private void HandleRevived()
    {
        StopDamagedEffect();
    }

    private void StartDamagedEffect()
    {
        if (_damagedEffectInstance != null || _data.DamagedEffectPrefab == null)
        {
            return;
        }

        // 敵の子にして、砲台が向きを変えても同じ場所から出るようにする。
        // 置いた敵ごとに Scale が違うので、位置はモデルの見た目の大きさから決める。
        _damagedEffectInstance = Instantiate(_data.DamagedEffectPrefab, GetDamagedEffectPosition(), Quaternion.identity, transform);

        // パーティクルは自分の Scale だけを見る (Scaling Mode: Local) ので、親の Scale に関係なくこの倍率になる。
        _damagedEffectInstance.transform.localScale = Vector3.one * _data.DamagedEffectScale;
    }

    /// <summary>モデルの足元からてっぺんまでのうち、指定の割合の高さ。モデルが見つからなければ敵の位置。</summary>
    private Vector3 GetDamagedEffectPosition()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        Bounds bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            // 予告線・弾の軌跡・煙は対象外。モデルだけで測る。
            if (!(renderers[i] is MeshRenderer) && !(renderers[i] is SkinnedMeshRenderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        if (!hasBounds)
        {
            return transform.position;
        }

        return new Vector3(
            bounds.center.x,
            bounds.min.y + bounds.size.y * _data.DamagedEffectHeightRatio,
            bounds.center.z);
    }

    /// <summary>新しく出すのを止め、出ている煙が消えきってから片付ける。</summary>
    private void StopDamagedEffect()
    {
        if (_damagedEffectInstance == null)
        {
            return;
        }

        ParticleSystem[] particles = _damagedEffectInstance.GetComponentsInChildren<ParticleSystem>();

        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        // 消えるまで敵の動きにつられないよう、その場に残す。
        _damagedEffectInstance.transform.SetParent(null, true);
        Destroy(_damagedEffectInstance, _data.DamagedEffectFadeOutSeconds);
        _damagedEffectInstance = null;
    }
}

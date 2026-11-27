using UnityEngine;

/// <summary>
/// 敵にグレネードが当たったときの手ごたえ (音・傷ついた見た目) の調整値。
/// 敵の種類が違っても同じアセットを使い回せる。見せ方を変えたい敵だけ複製して数値を変える。
/// </summary>
[CreateAssetMenu(fileName = "SO_EnemyHitFeedbackData", menuName = "Bomb/Enemy Hit Feedback Data")]
public class EnemyHitFeedbackData : ScriptableObject
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("Sound")]
    [SerializeField, Tooltip("倒れない1発を受けたときに鳴らす音。SO_SEData に登録が無ければ鳴らない")]
    private SEType _hitSe = SEType.EnemyHit;

    [SerializeField, Tooltip("倒れる1発を受けたときに鳴らす音。SO_SEData に登録が無ければ鳴らない")]
    private SEType _killSe = SEType.EnemyKill;

    [SerializeField, Tooltip("被弾音・撃破音の音量の倍率")]
    [Range(0f, 1f)]
    private float _seVolume = 1f;

    [Header("Damaged Effect")]
    [SerializeField, Tooltip("傷ついた敵から出し続ける煙と火花。空なら出さない")]
    private GameObject _damagedEffectPrefab;

    [SerializeField, Tooltip("残りの回数がこれ以下になったら傷ついた見た目にする。2発で倒れる敵なら 1 で「あと1発」から出る")]
    [Min(1)]
    private int _damagedEffectRemainingHitCount = 1;

    [SerializeField, Tooltip("傷ついた見た目を出す高さ。モデルの足元を 0、てっぺんを 1 とした割合。敵の大きさが違っても同じ所から出る")]
    [Range(0f, 1.5f)]
    private float _damagedEffectHeightRatio = 0.8f;

    [SerializeField, Tooltip("傷ついた見た目の大きさの倍率")]
    [Min(0.01f)]
    private float _damagedEffectScale = 1f;

    [SerializeField, Tooltip("止めてから消すまでの時間 (秒)。出ている煙が自然に消えきるのを待つ")]
    [Min(0f)]
    private float _damagedEffectFadeOutSeconds = 2f;

    [Header("Rumble")]
    [SerializeField, Tooltip("倒れない1発のときの振動の強さ (低音側の重いモーター 0〜1)")]
    [Range(0f, 1f)]
    private float _hitRumbleLow = 0.15f;

    [SerializeField, Tooltip("倒れない1発のときの振動の強さ (高音側の軽いモーター 0〜1)")]
    [Range(0f, 1f)]
    private float _hitRumbleHigh = 0.35f;

    [SerializeField, Tooltip("倒れない1発のときに振動させる時間 (秒)")]
    [Range(0f, 1f)]
    private float _hitRumbleSeconds = 0.08f;

    [SerializeField, Tooltip("倒れる1発のときの振動の強さ (低音側の重いモーター 0〜1)")]
    [Range(0f, 1f)]
    private float _killRumbleLow = 0.45f;

    [SerializeField, Tooltip("倒れる1発のときの振動の強さ (高音側の軽いモーター 0〜1)")]
    [Range(0f, 1f)]
    private float _killRumbleHigh = 0.7f;

    [SerializeField, Tooltip("倒れる1発のときに振動させる時間 (秒)")]
    [Range(0f, 1f)]
    private float _killRumbleSeconds = 0.18f;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>倒れない1発を受けたときの音。</summary>
    public SEType HitSe => _hitSe;

    /// <summary>倒れる1発を受けたときの音。</summary>
    public SEType KillSe => _killSe;

    /// <summary>被弾音・撃破音の音量の倍率 (0〜1)。</summary>
    public float SeVolume => _seVolume;

    /// <summary>傷ついた敵から出し続けるエフェクト。null なら出さない。</summary>
    public GameObject DamagedEffectPrefab => _damagedEffectPrefab;

    /// <summary>残りの回数がこれ以下になったら傷ついた見た目にする。</summary>
    public int DamagedEffectRemainingHitCount => _damagedEffectRemainingHitCount;

    /// <summary>傷ついた見た目を出す高さ (モデルの足元 0〜てっぺん 1 の割合)。</summary>
    public float DamagedEffectHeightRatio => _damagedEffectHeightRatio;

    /// <summary>傷ついた見た目の大きさの倍率。</summary>
    public float DamagedEffectScale => _damagedEffectScale;

    /// <summary>止めてから消すまでの時間 (秒)。</summary>
    public float DamagedEffectFadeOutSeconds => _damagedEffectFadeOutSeconds;

    /// <summary>低音側モーターの強さ (0〜1)。</summary>
    public float GetRumbleLow(bool isKill) => isKill ? _killRumbleLow : _hitRumbleLow;

    /// <summary>高音側モーターの強さ (0〜1)。</summary>
    public float GetRumbleHigh(bool isKill) => isKill ? _killRumbleHigh : _hitRumbleHigh;

    /// <summary>振動させる時間 (秒)。</summary>
    public float GetRumbleSeconds(bool isKill) => isKill ? _killRumbleSeconds : _hitRumbleSeconds;
}

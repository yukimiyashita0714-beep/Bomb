using UnityEngine;

/// <summary>
/// 爆風で破片になって地面に残り、しばらくすると元の場所に戻って復活するブロックの調整値。
/// マップのブロックは数千個あるので、1個ずつではなくこのアセット1つで全部まとめて調整する。
/// </summary>
[CreateAssetMenu(fileName = "SO_DestructibleBlockData", menuName = "Bomb/Destructible Block Data")]
public class DestructibleBlockData : ScriptableObject
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("Break")]
    [SerializeField, Tooltip("爆風が当たってから縮み始めるまでの時間 (秒)")]
    [Range(0f, 5f)]
    private float _fadeDelaySeconds = 0f;

    [SerializeField, Tooltip("縮み始めてから破片の大きさになるまでの時間 (秒)")]
    [Range(0.05f, 10f)]
    private float _fadeDurationSeconds = 1f;

    // NOTE: 初めはゆっくり、終わり際に一気に縮む形 (EaseIn) にしておくと、壊れた形が少しの間見えて分かりやすい。
    [Header("Shrink")]
    [SerializeField, Tooltip("縮み方。横軸が縮み始めてからの経過割合 (0〜1)、縦軸が 1 で元の大きさ、0 で破片の大きさ")]
    private AnimationCurve _shrinkCurve = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -2f, 0f));

    [SerializeField, Tooltip("破片の大きさの下限。最初の大きさに対する倍率。ブロックごとにこの範囲からランダムに決める")]
    [Range(0.05f, 1f)]
    private float _minDebrisScaleRatio = 0.2f;

    [SerializeField, Tooltip("破片の大きさの上限。最初の大きさに対する倍率")]
    [Range(0.05f, 1f)]
    private float _maxDebrisScaleRatio = 0.5f;

    [Header("Spin")]
    [SerializeField, Tooltip("回転の速さの下限 (度/秒)。ブロックごとにこの範囲からランダムに決める")]
    [Range(0f, 3600f)]
    private float _minSpinDegreesPerSecond = 360f;

    [SerializeField, Tooltip("回転の速さの上限 (度/秒)")]
    [Range(0f, 3600f)]
    private float _maxSpinDegreesPerSecond = 900f;

    [Header("Respawn")]
    [SerializeField, Tooltip("爆風が当たってから (爆風以外でずれたときは、ずれてから) 元の場所に復活するまでの時間 (秒)")]
    [Range(1f, 120f)]
    private float _respawnDelaySeconds = 15f;

    [SerializeField, Tooltip("復活する場所にプレイヤーなどがいたとき、もう一度確かめるまでの間隔 (秒)")]
    [Range(0.1f, 5f)]
    private float _respawnRetrySeconds = 0.5f;

    [SerializeField, Tooltip("破片が元の場所へ戻っていくのにかける時間 (秒)")]
    [Range(0.1f, 5f)]
    private float _returnDurationSeconds = 0.6f;

    [Header("Displacement")]
    [SerializeField, Tooltip("壊れていないブロックが、元の位置からこの距離 (m) 以上ずれたら「ずれた」とみなし、復活と同じ時間がたったら元の場所へ戻す")]
    [Range(0.01f, 5f)]
    private float _displacedDistanceMeters = 0.2f;

    [SerializeField, Tooltip("壊れていないブロックが、元の向きからこの角度 (度) 以上傾いたら「ずれた」とみなす")]
    [Range(1f, 180f)]
    private float _displacedAngleDegrees = 15f;

    [SerializeField, Tooltip("何かに当たって動いているブロックが、ずれたかどうかを調べる間隔 (秒)。短いほど正確だが重くなる")]
    [Range(0.1f, 5f)]
    private float _displacementCheckIntervalSeconds = 0.5f;

    [Header("Motion")]
    [SerializeField, Tooltip("ON なら爆風で飛ばされながら縮む。OFF なら縮み終わるまでその場で止まる")]
    private bool _isMovingWhileFading = true;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>爆風が当たってから縮み始めるまでの時間 (秒)。</summary>
    public float FadeDelaySeconds => _fadeDelaySeconds;

    /// <summary>縮み始めてから破片の大きさになるまでの時間 (秒)。</summary>
    public float FadeDurationSeconds => _fadeDurationSeconds;

    /// <summary>破片の大きさの下限。最初の大きさに対する倍率。</summary>
    public float MinDebrisScaleRatio => _minDebrisScaleRatio;

    /// <summary>破片の大きさの上限。最初の大きさに対する倍率。</summary>
    public float MaxDebrisScaleRatio => _maxDebrisScaleRatio;

    /// <summary>回転の速さの下限 (度/秒)。</summary>
    public float MinSpinDegreesPerSecond => _minSpinDegreesPerSecond;

    /// <summary>回転の速さの上限 (度/秒)。</summary>
    public float MaxSpinDegreesPerSecond => _maxSpinDegreesPerSecond;

    /// <summary>爆風が当たってから復活するまでの時間 (秒)。</summary>
    public float RespawnDelaySeconds => _respawnDelaySeconds;

    /// <summary>復活の場所がふさがっていたとき、もう一度確かめるまでの間隔 (秒)。</summary>
    public float RespawnRetrySeconds => _respawnRetrySeconds;

    /// <summary>破片が元の場所へ戻っていくのにかける時間 (秒)。</summary>
    public float ReturnDurationSeconds => _returnDurationSeconds;

    /// <summary>元の位置からこの距離 (m) 以上ずれたら「ずれた」とみなす。</summary>
    public float DisplacedDistanceMeters => _displacedDistanceMeters;

    /// <summary>元の向きからこの角度 (度) 以上傾いたら「ずれた」とみなす。</summary>
    public float DisplacedAngleDegrees => _displacedAngleDegrees;

    /// <summary>動いているブロックがずれたかどうかを調べる間隔 (秒)。</summary>
    public float DisplacementCheckIntervalSeconds => _displacementCheckIntervalSeconds;

    /// <summary>true なら爆風で飛ばされながら縮む。</summary>
    public bool IsMovingWhileFading => _isMovingWhileFading;

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>破片の大きさ (最初の大きさに対する倍率) を、下限と上限の間からランダムに決めて返す。</summary>
    public float GetRandomDebrisScaleRatio()
    {
        return Random.Range(_minDebrisScaleRatio, _maxDebrisScaleRatio);
    }

    /// <summary>
    /// 縮み始めてからの経過割合 (0〜1) に対する、元の大きさの倍率を返す。
    /// カーブが 1 なら元の大きさ、0 なら破片の大きさ (<paramref name="debrisScaleRatio"/>) になる。
    /// </summary>
    public float GetShrinkScale(float progressRatio, float debrisScaleRatio)
    {
        float t = Mathf.Clamp01(progressRatio);

        // カーブが未設定や壊れているときは、まっすぐ縮める。
        float curveValue = _shrinkCurve == null || _shrinkCurve.length == 0
            ? 1f - t
            : _shrinkCurve.Evaluate(t);

        return Mathf.Lerp(debrisScaleRatio, 1f, Mathf.Clamp01(curveValue));
    }

    // ==========================================
    // Unity Messages
    // ==========================================

    private void OnValidate()
    {
        // 上限が下限を下回ると Random.Range の範囲が逆になる。
        if (_maxSpinDegreesPerSecond < _minSpinDegreesPerSecond)
        {
            _maxSpinDegreesPerSecond = _minSpinDegreesPerSecond;
        }

        if (_maxDebrisScaleRatio < _minDebrisScaleRatio)
        {
            _maxDebrisScaleRatio = _minDebrisScaleRatio;
        }
    }
}

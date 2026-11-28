using UnityEngine;

/// <summary>
/// アセットの壁が壊れたときに飛び散る破片の調整値。全部の壁で同じアセットを共有する。
/// 破片の形 (メッシュ) は Bomb/Setup/Wall Debris で作ったものを並べておき、1個ずつランダムに選ぶ。
/// 飛ばし方は、分割済みのブロックがグレネードで飛ぶとき (Rigidbody.AddExplosionForce) に近づけてある。
/// </summary>
[CreateAssetMenu(fileName = "SO_WallDebrisData", menuName = "Bomb/Wall Debris Data")]
public class WallDebrisData : ScriptableObject
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("Style")]
    [SerializeField, Tooltip("Crumble: 壁は消えて、大きな板と小さなかけらに割れて飛ぶ。\nFlyAway: 今までどおり壁ごと回りながら飛び、そこからかけらも飛び散る")]
    private WallBreakStyle _breakStyle = WallBreakStyle.Crumble;

    [Header("Shape")]
    [SerializeField, Tooltip("破片の形。1個ずつこの中からランダムに選ぶ。大きさ 1m 前後で作ってある")]
    private Mesh[] _meshes;

    [SerializeField, Tooltip("小さなかけらの基本の大きさ (m)")]
    [Min(0.05f)]
    private float _pieceSize = 0.6f;

    [SerializeField, Tooltip("小さなかけらの大きさのばらつきの下限 (基本の大きさに対する倍率)")]
    [Min(0.05f)]
    private float _minSizeRatio = 0.4f;

    [SerializeField, Tooltip("小さなかけらの大きさのばらつきの上限 (基本の大きさに対する倍率)")]
    [Min(0.05f)]
    private float _maxSizeRatio = 1.4f;

    [Header("Big Pieces (Crumble)")]
    [SerializeField, Tooltip("壁から割れる大きな板の最小数。壁の厚みを保ったまま、重く回りながら飛ぶ")]
    [Min(0)]
    private int _minBigPieceCount = 2;

    [SerializeField, Tooltip("壁から割れる大きな板の最大数")]
    [Min(0)]
    private int _maxBigPieceCount = 4;

    [SerializeField, Tooltip("大きな板の幅と高さの下限 (壁の幅・高さに対する割合)")]
    [Range(0.05f, 1f)]
    private float _minBigPieceRatio = 0.3f;

    [SerializeField, Tooltip("大きな板の幅と高さの上限 (壁の幅・高さに対する割合)")]
    [Range(0.05f, 1f)]
    private float _maxBigPieceRatio = 0.55f;

    [SerializeField, Tooltip("大きな板の飛ぶ速さの倍率。重い塊らしく、小さなかけらより遅く飛ばす")]
    [Range(0.1f, 2f)]
    private float _bigPieceSpeedRatio = 0.6f;

    [Header("Count")]
    [SerializeField, Tooltip("壁の体積 1m³ あたりに出す小さなかけらの数。大きい壁ほど多く出る")]
    [Min(0f)]
    private float _piecesPerCubicMeter = 5f;

    [SerializeField, Tooltip("1枚の壁から出す小さなかけらの最小数")]
    [Min(0)]
    private int _minPieceCount = 8;

    [SerializeField, Tooltip("1枚の壁から出す小さなかけらの最大数")]
    [Min(0)]
    private int _maxPieceCount = 22;

    [SerializeField, Tooltip("同時に出ていられる破片の最大数。超えたら古い破片から消して使い回す (重さ対策)")]
    [Min(1)]
    private int _maxActivePieceCount = 250;

    [Header("Motion")]
    [SerializeField, Tooltip("爆心のすぐそばで飛ぶ速さ (m/s)。分割済みブロックはグレネードの爆発力 18 で飛ぶので、それに合わせてある")]
    [Min(0f)]
    private float _maxSpeed = 18f;

    [SerializeField, Tooltip("爆風の端で飛ぶ速さ (m/s)。遠くても少しは飛ぶようにする")]
    [Min(0f)]
    private float _minSpeed = 5f;

    [SerializeField, Tooltip("この距離 (m) で速さが一番遅くなる。グレネードの爆発の半径と同じにしてある")]
    [Min(0.1f)]
    private float _blastRadius = 5f;

    [SerializeField, Tooltip("爆心をこれだけ下 (m) にあることにして、上へ跳ね上げる。グレネードの Upwards Modifier と同じ考え方")]
    [Min(0f)]
    private float _upwardsModifier = 2f;

    [SerializeField, Tooltip("飛ぶ向きのばらつき。0 ならきれいに放射状、大きいほどバラバラに散る")]
    [Range(0f, 1f)]
    private float _directionSpread = 0.3f;

    [SerializeField, Tooltip("速さのばらつき (±の割合)")]
    [Range(0f, 1f)]
    private float _speedJitter = 0.25f;

    [SerializeField, Tooltip("回転の速さの下限 (度/秒)。分割済みブロックと同じ")]
    [Min(0f)]
    private float _minSpinDegreesPerSecond = 360f;

    [SerializeField, Tooltip("回転の速さの上限 (度/秒)。分割済みブロックと同じ")]
    [Min(0f)]
    private float _maxSpinDegreesPerSecond = 900f;

    [SerializeField, Tooltip("1m 角の破片の重さ (kg)。大きさに合わせて増減する")]
    [Min(0.01f)]
    private float _massPerCubicMeter = 3f;

    [Header("Lifetime")]
    [SerializeField, Tooltip("縮み始めるまでの時間 (秒)。地面で転がる時間を残す。壁が復活する時間より短くする")]
    [Min(0f)]
    private float _lifeSeconds = 4f;

    [SerializeField, Tooltip("縮んで消えるまでの時間 (秒)")]
    [Min(0.01f)]
    private float _shrinkSeconds = 1f;

    [SerializeField, Tooltip("消える時間のばらつき (秒)。全部の破片が同時に消えないようにする")]
    [Min(0f)]
    private float _lifeJitterSeconds = 1.5f;

    [Header("Effects")]
    [SerializeField, Tooltip("壊れた瞬間に壁の位置から出すほこりと火花。壁が消える瞬間を隠す")]
    private GameObject _breakEffectPrefab;

    [SerializeField, Tooltip("壁が戻るときに足元から出す小さなほこり")]
    private GameObject _restoreEffectPrefab;

    [Header("Restore (Crumble)")]
    [SerializeField, Tooltip("壁が戻るのにかける時間 (秒)。縮んだ状態から元の大きさまで伸びる")]
    [Min(0.01f)]
    private float _restoreSeconds = 0.7f;

    [SerializeField, Tooltip("戻り始めの大きさ (元の大きさに対する割合)")]
    [Range(0f, 1f)]
    private float _restoreStartScale = 0.15f;

    [SerializeField, Tooltip("戻るときに残っている破片を、何秒で縮めて片付けるか")]
    [Min(0.01f)]
    private float _cleanupShrinkSeconds = 0.4f;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>壁の壊れ方。</summary>
    public WallBreakStyle BreakStyle => _breakStyle;

    /// <summary>破片の形の候補。</summary>
    public Mesh[] Meshes => _meshes;

    /// <summary>同時に出ていられる破片の最大数。</summary>
    public int MaxActivePieceCount => _maxActivePieceCount;

    /// <summary>大きな板の飛ぶ速さの倍率。</summary>
    public float BigPieceSpeedRatio => _bigPieceSpeedRatio;

    /// <summary>飛ぶ向きのばらつき (0〜1)。</summary>
    public float DirectionSpread => _directionSpread;

    /// <summary>上へ跳ね上げる量 (m)。</summary>
    public float UpwardsModifier => _upwardsModifier;

    /// <summary>縮んで消えるまでの時間 (秒)。</summary>
    public float ShrinkSeconds => _shrinkSeconds;

    /// <summary>壊れた瞬間のほこりと火花。</summary>
    public GameObject BreakEffectPrefab => _breakEffectPrefab;

    /// <summary>戻るときのほこり。</summary>
    public GameObject RestoreEffectPrefab => _restoreEffectPrefab;

    /// <summary>壁が戻るのにかける時間 (秒)。</summary>
    public float RestoreSeconds => _restoreSeconds;

    /// <summary>戻り始めの大きさの割合。</summary>
    public float RestoreStartScale => _restoreStartScale;

    /// <summary>戻るときに残った破片を片付ける時間 (秒)。</summary>
    public float CleanupShrinkSeconds => _cleanupShrinkSeconds;

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>形の候補からランダムに1つ返す。登録が無ければ null。</summary>
    public Mesh GetRandomMesh()
    {
        if (_meshes == null || _meshes.Length == 0)
        {
            return null;
        }

        return _meshes[Random.Range(0, _meshes.Length)];
    }

    /// <summary>小さなかけら1個の大きさ (m) をランダムに決めて返す。</summary>
    public float GetRandomPieceSize()
    {
        return _pieceSize * Random.Range(_minSizeRatio, Mathf.Max(_minSizeRatio, _maxSizeRatio));
    }

    /// <summary>大きな板の数をランダムに決めて返す。</summary>
    public int GetRandomBigPieceCount()
    {
        return Random.Range(_minBigPieceCount, Mathf.Max(_minBigPieceCount, _maxBigPieceCount) + 1);
    }

    /// <summary>大きな板の幅・高さの割合をランダムに決めて返す。</summary>
    public float GetRandomBigPieceRatio()
    {
        return Random.Range(_minBigPieceRatio, Mathf.Max(_minBigPieceRatio, _maxBigPieceRatio));
    }

    /// <summary>大きさ (各辺 m) に合わせた重さ (kg)。小さな破片が壁を押しのけないよう、体積に比例させる。</summary>
    public float GetMass(Vector3 scale)
    {
        return Mathf.Max(0.05f, _massPerCubicMeter * Mathf.Abs(scale.x * scale.y * scale.z));
    }

    /// <summary>壁の体積 (m³) から出す小さなかけらの数を決める。</summary>
    public int GetPieceCount(float volume)
    {
        int count = Mathf.RoundToInt(volume * _piecesPerCubicMeter);
        return Mathf.Clamp(count, _minPieceCount, Mathf.Max(_minPieceCount, _maxPieceCount));
    }

    /// <summary>爆心からの距離 (m) に応じた速さ (m/s)。近いほど速い。ばらつき込み。</summary>
    public float GetSpeedAtDistance(float distance)
    {
        float t = Mathf.Clamp01(distance / _blastRadius);
        float speed = Mathf.Lerp(_maxSpeed, _minSpeed, t);
        return speed * (1f + Random.Range(-_speedJitter, _speedJitter));
    }

    /// <summary>回転の速さ (ラジアン/秒) をランダムに決めて返す。</summary>
    public Vector3 GetRandomAngularVelocity()
    {
        float degrees = Random.Range(_minSpinDegreesPerSecond, Mathf.Max(_minSpinDegreesPerSecond, _maxSpinDegreesPerSecond));
        return Random.onUnitSphere * (degrees * Mathf.Deg2Rad);
    }

    /// <summary>縮み始めるまでの時間 (秒) をばらつきつきで返す。</summary>
    public float GetRandomLifeSeconds()
    {
        return _lifeSeconds + Random.Range(0f, _lifeJitterSeconds);
    }
}

/// <summary>アセットの壁の壊れ方。</summary>
public enum WallBreakStyle
{
    /// <summary>壁は消えて、大きな板と小さなかけらに割れて飛ぶ。戻るときは縮んだ状態から伸びて戻る。</summary>
    Crumble,

    /// <summary>今までどおり壁ごと回りながら飛んで縮み、元の場所へ飛んで戻る。そこにかけらの飛び散りを足す。</summary>
    FlyAway,
}

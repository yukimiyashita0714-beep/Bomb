using System.Collections;
using UnityEngine;

/// <summary>
/// 爆風が当たると回りながら縮んで破片になり、地面に残る。一定時間たつと破片が元の場所へ戻って復活するブロック。
/// グレネードは相手が何者かを知らずに <see cref="IDamageable"/> だけを見て当てるので、
/// ここもそれを実装して「ダメージを受けた = 爆風が当たった」として扱う。
/// 破片の間はレイヤーを Debris に変え、地面や壁には乗るが、プレイヤーやグレネードとは当たらないようにする
/// (どのレイヤー同士が当たるかは Project Settings > Physics の Layer Collision Matrix で決めている)。
/// </summary>
[DisallowMultipleComponent]
public class DestructibleBlock : MonoBehaviour, IDamageable
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("壊れ方と復活の設定。全ブロックで同じアセットを共有する")]
    private DestructibleBlockData _data;

    // ==========================================
    // Private Fields
    // ==========================================

    /// <summary>破片の間に使うレイヤーの名前。Project Settings > Tags and Layers に登録してある。</summary>
    private const string DEBRIS_LAYER_NAME = "Debris";

    /// <summary>レイヤーの番号をまだ調べていないことを表す値。</summary>
    private const int UNRESOLVED_LAYER = -2;

    /// <summary>復活の場所に誰かいるかを調べるとき、一度に拾う上限。</summary>
    private const int MAX_RESPAWN_OVERLAP_COUNT = 16;

    /// <summary>
    /// 復活の場所を調べる箱を少し小さくする割合。
    /// 原寸だと、隣に並んでいるブロックにも触れてしまい、いつまでも復活できない。
    /// </summary>
    private const float RESPAWN_CHECK_SHRINK_RATIO = 0.9f;

    /// <summary>破片の重さの下限。0 に近いと物理が不安定になる。</summary>
    private const float MIN_DEBRIS_MASS = 0.01f;

    private static readonly Collider[] RespawnOverlapBuffer = new Collider[MAX_RESPAWN_OVERLAP_COUNT];

    private static int _debrisLayer = UNRESOLVED_LAYER;

    private Rigidbody _rigidbody;
    private Collider[] _colliders;
    private Transform[] _layerTargets;

    // 復活したときに元へ戻すため、最初の状態を覚えておく。
    private Vector3 _initialLocalPosition;
    private Quaternion _initialLocalRotation;
    private Vector3 _initialLocalScale;
    private bool _wasKinematic;
    private float _initialMass;
    private int[] _originalLayers;

    private float _brokenTime;
    private bool _isBreaking;

    /// <summary>メインターゲットの爆発で飛ばされたら true。もう壊れも復活もしない。</summary>
    private bool _isLaunched;

    /// <summary>今回の破片の大きさ。壊れるたびにランダムに決め直す。</summary>
    private float _debrisScaleRatio;

    /// <summary>Rigidbody に回転を任せられないときに、自分で回す軸と速さ。</summary>
    private Vector3 _spinAxis;
    private float _spinDegreesPerSecond;
    private bool _isSpinningByTransform;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>爆風が当たってから、元の場所に戻って復活するまでの間は true。</summary>
    public bool IsBreaking => _isBreaking;

    /// <summary>メインターゲットの爆発で飛ばされた後は true。元の場所には戻らない。</summary>
    public bool IsLaunched => _isLaunched;

    /// <summary>破片のレイヤー番号。登録されていなければ -1。</summary>
    private static int DebrisLayer
    {
        get
        {
            if (_debrisLayer == UNRESOLVED_LAYER)
            {
                _debrisLayer = LayerMask.NameToLayer(DEBRIS_LAYER_NAME);

                if (_debrisLayer < 0)
                {
                    Debug.LogWarning(
                        $"[{nameof(DestructibleBlock)}] レイヤー \"{DEBRIS_LAYER_NAME}\" が無いので、破片がプレイヤーにも当たってしまう。");
                }
            }

            return _debrisLayer;
        }
    }

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>(true);
        _layerTargets = GetComponentsInChildren<Transform>(true);

        if (_data == null)
        {
            Debug.LogError($"[{nameof(DestructibleBlock)}] DestructibleBlockData が未設定のため動作できない。", this);
            enabled = false;
            return;
        }

        SaveInitialState();
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>爆風が当たったときに呼ばれる。量に関係なく、一度当たれば壊れる。破片の間は何もしない。</summary>
    public void TakeDamage(float amount)
    {
        if (!enabled || _isBreaking || _isLaunched)
        {
            return;
        }

        Break();
    }

    /// <summary>
    /// メインターゲットの爆発で飛ばす。グレネードの爆風とは別の処理で、縮みも復活もしない。
    /// 破片と同じ Debris レイヤーに移すので、飛んだブロック同士や、まだ立っているブロック、
    /// プレイヤーやグレネードとは当たらず、地面にだけ落ちる。
    /// </summary>
    /// <param name="velocity">飛ばす速度 (m/s)。重さに関係なくこの速さになる。</param>
    /// <param name="angularVelocity">付ける回転の速さ (ラジアン/秒)。</param>
    public void Launch(Vector3 velocity, Vector3 angularVelocity)
    {
        if (!enabled || _isLaunched)
        {
            return;
        }

        _isLaunched = true;

        // グレネードで壊れている途中なら、縮む・元に戻る処理をここで止める。
        StopAllCoroutines();
        _isBreaking = false;
        _isSpinningByTransform = false;

        int debrisLayer = DebrisLayer;

        if (debrisLayer >= 0)
        {
            SetLayer(debrisLayer);
        }

        // 元の場所へ戻る途中で当たり判定を切っていた場合に備えて、入れ直す。
        SetCollidersEnabled(true);

        if (_rigidbody == null)
        {
            return;
        }

        _rigidbody.isKinematic = false;
        _rigidbody.WakeUp();
        _rigidbody.AddForce(velocity, ForceMode.VelocityChange);
        _rigidbody.AddTorque(angularVelocity, ForceMode.VelocityChange);
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void SaveInitialState()
    {
        _initialLocalPosition = transform.localPosition;
        _initialLocalRotation = transform.localRotation;
        _initialLocalScale = transform.localScale;
        _wasKinematic = _rigidbody != null && _rigidbody.isKinematic;
        _initialMass = _rigidbody != null ? _rigidbody.mass : 0f;

        _originalLayers = new int[_layerTargets.Length];

        for (int i = 0; i < _layerTargets.Length; i++)
        {
            _originalLayers[i] = _layerTargets[i].gameObject.layer;
        }
    }

    private void Break()
    {
        _isBreaking = true;
        _brokenTime = Time.time;

        //破片ごとに大きさを変える。
        _debrisScaleRatio = _data.GetRandomDebrisScaleRatio();

        // 当たり判定は残したまま、プレイヤーやグレネードとは当たらないレイヤーに移す。
        // こうすると、壊れた瞬間から通り抜けられるのに、破片は地面に落ちて残る。
        int debrisLayer = DebrisLayer;

        if (debrisLayer >= 0)
        {
            SetLayer(debrisLayer);
        }

        if (_rigidbody != null && !_data.IsMovingWhileFading)
        {
            _rigidbody.isKinematic = true;
        }

        StartSpin();
        StartCoroutine(BreakAndRespawnRoutine());
    }

    /// <summary>
    /// ブロックごとに軸と速さを変えて回す。
    /// </summary>
    private void StartSpin()
    {
        _spinAxis = Random.onUnitSphere;
        _spinDegreesPerSecond = Random.Range(_data.MinSpinDegreesPerSecond, _data.MaxSpinDegreesPerSecond);

        bool isMovedByPhysics = _rigidbody != null && !_rigidbody.isKinematic;

        if (!isMovedByPhysics)
        {
            _isSpinningByTransform = true;
            return;
        }

        // 物理で動いている間に transform を回すと物理とずれるので、回転も Rigidbody に任せる。
        // 既定の上限 (7 rad/s) だと速い回転が頭打ちになるので、出したい速さまで上げておく。
        float spinRadiansPerSecond = _spinDegreesPerSecond * Mathf.Deg2Rad;
        _rigidbody.maxAngularVelocity = Mathf.Max(_rigidbody.maxAngularVelocity, spinRadiansPerSecond);
        _rigidbody.angularVelocity = _spinAxis * spinRadiansPerSecond;
    }

    private IEnumerator BreakAndRespawnRoutine()
    {
        yield return ShrinkRoutine();

        BecomeDebris();

        // 爆風が当たった瞬間から数える。縮むのにかかった時間もこの中に含まれる。
        float respawnTime = _brokenTime + _data.RespawnDelaySeconds;

        if (Time.time < respawnTime)
        {
            yield return new WaitForSeconds(respawnTime - Time.time);
        }

        // プレイヤーがいる場所に戻すと、ブロックの中に閉じ込めたり弾き飛ばしたりしてしまう。
        // 場所が空くまで待つので、このループはプレイヤーがどけば必ず抜ける。
        WaitForSeconds retryWait = new WaitForSeconds(_data.RespawnRetrySeconds);

        while (IsRespawnBlocked())
        {
            yield return retryWait;
        }

        yield return ReturnRoutine();

        Respawn();
    }

    private IEnumerator ShrinkRoutine()
    {
        // 待っている間も回し続け、壊れた瞬間から回って見えるようにする。
        float delayElapsedSeconds = 0f;

        while (delayElapsedSeconds < _data.FadeDelaySeconds)
        {
            delayElapsedSeconds += Time.deltaTime;
            TickSpin();
            yield return null;
        }

        Vector3 startScale = transform.localScale;
        float durationSeconds = _data.FadeDurationSeconds;
        float elapsedSeconds = 0f;

        while (elapsedSeconds < durationSeconds)
        {
            elapsedSeconds += Time.deltaTime;
            float progressRatio = Mathf.Clamp01(elapsedSeconds / durationSeconds);

            TickSpin();
            transform.localScale = startScale * _data.GetShrinkScale(progressRatio, _debrisScaleRatio);

            yield return null;
        }
    }

    private void TickSpin()
    {
        if (!_isSpinningByTransform)
        {
            return;
        }

        transform.Rotate(_spinAxis, _spinDegreesPerSecond * Time.deltaTime, Space.World);
    }

    /// <summary>
    /// 縮み終わった破片を物理に任せ、地面に落ちて転がって止まるようにする。
    /// 自前の回転はここで止める。物理で回っていた分は、そのまま転がりとして残る。
    /// </summary>
    private void BecomeDebris()
    {
        _isSpinningByTransform = false;

        if (_rigidbody == null)
        {
            return;
        }

        // 元の重さのままだと、小さな破片がまだ壊れていないブロックを押しのけてしまう。
        float ratio = _debrisScaleRatio;
        _rigidbody.mass = Mathf.Max(MIN_DEBRIS_MASS, _initialMass * ratio * ratio * ratio);

        if (!_wasKinematic)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.WakeUp();
        }
    }

    /// <summary>破片を今の場所から元の位置・回転・大きさへ、滑らかに戻していく。</summary>
    private IEnumerator ReturnRoutine()
    {
        // 戻る途中で何かにぶつかって止まらないよう、物理から外して当たり判定も切る。
        if (_rigidbody != null)
        {
            if (!_rigidbody.isKinematic)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            _rigidbody.isKinematic = true;
        }

        SetCollidersEnabled(false);

        Vector3 startPosition = transform.localPosition;
        Quaternion startRotation = transform.localRotation;
        Vector3 startScale = transform.localScale;
        float durationSeconds = _data.ReturnDurationSeconds;
        float elapsedSeconds = 0f;

        while (elapsedSeconds < durationSeconds)
        {
            elapsedSeconds += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsedSeconds / durationSeconds));

            transform.localPosition = Vector3.Lerp(startPosition, _initialLocalPosition, t);
            transform.localRotation = Quaternion.Slerp(startRotation, _initialLocalRotation, t);
            transform.localScale = Vector3.Lerp(startScale, _initialLocalScale, t);

            yield return null;
        }
    }

    private void Respawn()
    {
        transform.localPosition = _initialLocalPosition;
        transform.localRotation = _initialLocalRotation;
        transform.localScale = _initialLocalScale;

        RestoreLayers();
        SetCollidersEnabled(true);

        if (_rigidbody != null)
        {
            _rigidbody.position = transform.position;
            _rigidbody.rotation = transform.rotation;
            _rigidbody.mass = _initialMass;
            _rigidbody.isKinematic = _wasKinematic;

            if (!_wasKinematic)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;

                // 積み上がった壁は下のブロックが先に戻るとは限らない。
                // 眠らせておけば、何かが触れるまで元の場所で止まっている。
                _rigidbody.Sleep();
            }
        }

        _isBreaking = false;
    }

    /// <summary>
    /// 元の場所に、プレイヤーやグレネードなど動く物が入っていれば true。
    /// 地形と、ほかのブロックと、破片は邪魔扱いしない。
    /// 破片は動かさずに、元の位置に置いたときの箱を計算して調べる。
    /// </summary>
    private bool IsRespawnBlocked()
    {
        Matrix4x4 parentMatrix = transform.parent != null
            ? transform.parent.localToWorldMatrix
            : Matrix4x4.identity;
        Matrix4x4 initialMatrix = parentMatrix * Matrix4x4.TRS(_initialLocalPosition, _initialLocalRotation, _initialLocalScale);

        int debrisLayer = DebrisLayer;
        int layerMask = debrisLayer >= 0 ? ~(1 << debrisLayer) : Physics.AllLayers;

        for (int i = 0; i < _colliders.Length; i++)
        {
            if (!(_colliders[i] is BoxCollider box))
            {
                continue;
            }

            // 子に付いた箱は、自分から見た位置関係を保ったまま元の場所へ移して考える。
            Matrix4x4 boxMatrix = initialMatrix * (transform.worldToLocalMatrix * box.transform.localToWorldMatrix);
            Vector3 lossyScale = boxMatrix.lossyScale;
            Vector3 absScale = new Vector3(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
            Vector3 halfExtents = Vector3.Scale(box.size, absScale) * (0.5f * RESPAWN_CHECK_SHRINK_RATIO);

            int hitCount = Physics.OverlapBoxNonAlloc(
                boxMatrix.MultiplyPoint3x4(box.center),
                halfExtents,
                RespawnOverlapBuffer,
                boxMatrix.rotation,
                layerMask,
                QueryTriggerInteraction.Ignore);

            for (int j = 0; j < hitCount; j++)
            {
                if (IsBlockingRespawn(RespawnOverlapBuffer[j]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsBlockingRespawn(Collider other)
    {
        if (other is CharacterController)
        {
            return true;
        }

        Rigidbody body = other.attachedRigidbody;

        if (body == null)
        {
            return false;
        }

        return !body.TryGetComponent(out DestructibleBlock _);
    }

    private void SetCollidersEnabled(bool isEnabled)
    {
        for (int i = 0; i < _colliders.Length; i++)
        {
            _colliders[i].enabled = isEnabled;
        }
    }

    private void SetLayer(int layer)
    {
        for (int i = 0; i < _layerTargets.Length; i++)
        {
            _layerTargets[i].gameObject.layer = layer;
        }
    }

    private void RestoreLayers()
    {
        for (int i = 0; i < _layerTargets.Length; i++)
        {
            _layerTargets[i].gameObject.layer = _originalLayers[i];
        }
    }
}

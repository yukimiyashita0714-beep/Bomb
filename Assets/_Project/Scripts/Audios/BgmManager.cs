using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// BgmEvents を購読して BGM を流す。シーンの === SYSTEMS === の下に1つだけ置く。
///
/// 【AudioSource の使い方】
///   子に AudioSource を2つ持ち、交互に使ってクロスフェードする。BGM は同時に1曲だけ。
///   BGM はいつも 2D (どこにいても同じ音量)。
///
/// 【フェードと Duck】
///   Time.timeScale の影響を受けない時間で進めるので、ポーズ中でも切り替えは止まらない。
///
/// 【音量】
///   作る AudioSource はすべて Bgm グループに流す。BGM スライダーは <see cref="SetBgmVolume"/> を呼ぶ。
///   値は PlayerPrefs に保存し、次に起動したときも同じ音量にする。
///   Mixer に Exposed Parameter が無いときは、AudioSource の音量に掛けて代わりにする。
///
/// 【購読のタイミング】
///   SEManager と同じく Awake で購読する。他のオブジェクトが OnEnable / Start で Play を呼んでも取りこぼさない。
/// </summary>
[DisallowMultipleComponent]
public class BgmManager : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("Data")]
    [SerializeField, Tooltip("BGM の登録表。SO_BgmData を入れる")]
    private BgmData _data;

    [Header("Start")]
    [SerializeField, Tooltip("シーンが始まったときに流す曲。None なら何も流さない")]
    private BgmType _startType = BgmType.None;

    [SerializeField, Tooltip("シーンが始まったときのフェードイン時間 (秒)")]
    [Min(0f)]
    private float _startFadeSeconds = 1f;

    [Header("Mixer")]
    [SerializeField, Tooltip("BGM を流す Mixer グループ。AM_Main の Bgm を入れる")]
    private AudioMixerGroup _bgmGroup;

    [SerializeField, Tooltip("Bgm の音量として公開した Exposed Parameter の名前")]
    private string _bgmVolumeParameter = "BgmVolume";

    [SerializeField, Tooltip("保存された音量が無いときの音量 (0〜1)")]
    [Range(0f, 1f)]
    private float _defaultBgmVolume = 1f;

    [Header("Duck")]
    [SerializeField, Tooltip("下げたときの音量の倍率")]
    [Range(0f, 1f)]
    private float _duckVolume = 0.35f;

    [SerializeField, Tooltip("下げる・戻すときにかける時間 (秒)")]
    [Min(0f)]
    private float _duckFadeSeconds = 0.3f;

    // ==========================================
    // Private Fields
    // ==========================================

    private const string BGM_VOLUME_PREF_KEY = "Bomb.BgmVolume";

    /// <summary>音量 0 のときに Mixer へ渡す値 (dB)。Mixer の下限。</summary>
    private const float MIN_DECIBEL = -80f;

    /// <summary>これより小さい音量は無音として扱う。Log10(0) が -∞ になるのを避ける。</summary>
    private const float MIN_LINEAR_VOLUME = 0.0001f;

    /// <summary>クロスフェードに使う AudioSource の数。</summary>
    private const int SOURCE_COUNT = 2;

    private readonly AudioSource[] _sources = new AudioSource[SOURCE_COUNT];

    /// <summary>各 AudioSource の登録表の音量。</summary>
    private readonly float[] _entryVolumes = new float[SOURCE_COUNT];

    /// <summary>各 AudioSource のフェードの今の値 (0〜1)。</summary>
    private readonly float[] _fadeLevels = new float[SOURCE_COUNT];

    /// <summary>各 AudioSource のフェードの行き先 (0 か 1)。</summary>
    private readonly float[] _fadeTargets = new float[SOURCE_COUNT];

    /// <summary>各 AudioSource のフェードにかける時間 (秒)。</summary>
    private readonly float[] _fadeSeconds = new float[SOURCE_COUNT];

    /// <summary>今の曲を流している AudioSource の番号。</summary>
    private int _currentIndex;

    private float _bgmVolume;

    /// <summary>Mixer で音量を変えられないとき true。AudioSource の音量に掛けて代わりにする。</summary>
    private bool _isSourceVolumeFallback;

    private bool _isDucked;

    /// <summary>Duck の今の倍率。1 で元の音量。</summary>
    private float _duckLevel = 1f;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>シーンにある BgmManager。BGM スライダーから音量を変えるときに使う。</summary>
    public static BgmManager Instance { get; private set; }

    /// <summary>今の BGM の音量 (0〜1)。</summary>
    public float BgmVolume => _bgmVolume;

    /// <summary>今流れている曲。何も流れていなければ null。</summary>
    public BgmType? CurrentType { get; private set; }

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(BgmManager)}] シーンに2つ以上ある。後から来た方を消す。", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_data == null)
        {
            Debug.LogError($"[{nameof(BgmManager)}] BgmData が未設定のため曲を流せない。", this);
        }

        if (_bgmGroup == null)
        {
            Debug.LogWarning($"[{nameof(BgmManager)}] Bgm グループが未設定。AudioSource の音量で代わりにする。", this);
        }

        _bgmVolume = PlayerPrefs.GetFloat(BGM_VOLUME_PREF_KEY, _defaultBgmVolume);

        for (int i = 0; i < SOURCE_COUNT; i++)
        {
            _sources[i] = CreateSource($"BGM_Source_{i}");
        }

        BgmEvents.OnPlayRequested += HandlePlay;
        BgmEvents.OnStopRequested += HandleStop;
        BgmEvents.OnDuckRequested += HandleDuck;
    }

    private void Start()
    {
        // AudioMixer.SetFloat は Awake で呼ぶと反映されないことがあるので Start で入れる。
        ApplyBgmVolume();

        if (_startType != BgmType.None)
        {
            HandlePlay(_startType, _startFadeSeconds);
        }
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        UpdateDuck(deltaTime);

        for (int i = 0; i < SOURCE_COUNT; i++)
        {
            UpdateFade(i, deltaTime);
        }
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        Instance = null;

        BgmEvents.OnPlayRequested -= HandlePlay;
        BgmEvents.OnStopRequested -= HandleStop;
        BgmEvents.OnDuckRequested -= HandleDuck;
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>BGM の音量を変えて保存する。BGM スライダーの OnValueChanged から呼ぶ。</summary>
    /// <param name="volume">0 で無音、1 で最大。</param>
    public void SetBgmVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(BGM_VOLUME_PREF_KEY, _bgmVolume);
        ApplyBgmVolume();
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void HandlePlay(BgmType type, float fadeSeconds)
    {
        // 同じ曲を頼まれても頭に戻さない。ウェーブごとに呼ばれても途切れないように。
        if (CurrentType == type && _sources[_currentIndex].isPlaying && _fadeTargets[_currentIndex] > 0f)
        {
            return;
        }

        if (_data == null || !_data.TryGetEntry(type, out BgmEntry entry))
        {
            return;
        }

        // 今の曲はフェードアウトさせ、空いている方で新しい曲を始める。
        StartFade(_currentIndex, 0f, fadeSeconds);

        int nextIndex = (_currentIndex + 1) % SOURCE_COUNT;
        AudioSource source = _sources[nextIndex];

        source.Stop();
        source.clip = entry.Clip;

        // clip を入れた後に設定する。入れる前に設定しても clip の差し替えで 0 に戻る。
        source.time = entry.StartSeconds;
        source.loop = entry.Loop;

        _entryVolumes[nextIndex] = entry.Volume;
        _fadeLevels[nextIndex] = 0f;
        StartFade(nextIndex, 1f, fadeSeconds);
        ApplySourceVolume(nextIndex);

        source.Play();

        _currentIndex = nextIndex;
        CurrentType = type;
    }

    private void HandleStop(float fadeSeconds)
    {
        StartFade(_currentIndex, 0f, fadeSeconds);
        CurrentType = null;
    }

    private void HandleDuck(bool isDucked)
    {
        _isDucked = isDucked;
    }

    private void StartFade(int index, float target, float seconds)
    {
        _fadeTargets[index] = target;
        _fadeSeconds[index] = seconds;

        // 0 秒ならその場で切り替える。
        if (seconds <= 0f)
        {
            _fadeLevels[index] = target;
            ApplySourceVolume(index);
            StopIfSilent(index);
        }
    }

    private void UpdateFade(int index, float deltaTime)
    {
        if (!Mathf.Approximately(_fadeLevels[index], _fadeTargets[index]))
        {
            float step = _fadeSeconds[index] > 0f ? deltaTime / _fadeSeconds[index] : 1f;
            _fadeLevels[index] = Mathf.MoveTowards(_fadeLevels[index], _fadeTargets[index], step);
        }

        ApplySourceVolume(index);
        StopIfSilent(index);
    }

    private void UpdateDuck(float deltaTime)
    {
        float target = _isDucked ? _duckVolume : 1f;

        if (Mathf.Approximately(_duckLevel, target))
        {
            return;
        }

        // 下げきるまで (1 → _duckVolume) を _duckFadeSeconds で進める。
        float range = Mathf.Max(1f - _duckVolume, MIN_LINEAR_VOLUME);
        float step = _duckFadeSeconds > 0f ? deltaTime * range / _duckFadeSeconds : 1f;
        _duckLevel = Mathf.MoveTowards(_duckLevel, target, step);
    }

    /// <summary>フェードアウトし終わった AudioSource を止める。止めないと音源の読み込みが続く。</summary>
    private void StopIfSilent(int index)
    {
        if (_fadeTargets[index] <= 0f && _fadeLevels[index] <= 0f && _sources[index].isPlaying)
        {
            _sources[index].Stop();
        }
    }

    private void ApplySourceVolume(int index)
    {
        float userVolume = _isSourceVolumeFallback ? _bgmVolume : 1f;
        _sources[index].volume = _entryVolumes[index] * _fadeLevels[index] * _duckLevel * userVolume;
    }

    private AudioSource CreateSource(string objectName)
    {
        GameObject sourceObject = new GameObject(objectName);
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.outputAudioMixerGroup = _bgmGroup;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;

        // ほかの音で BGM が途切れないよう、優先度を一番高くする。
        source.priority = 0;
        return source;
    }

    private void ApplyBgmVolume()
    {
        _isSourceVolumeFallback = !TrySetMixerVolume();

        for (int i = 0; i < SOURCE_COUNT; i++)
        {
            ApplySourceVolume(i);
        }
    }

    private bool TrySetMixerVolume()
    {
        if (_bgmGroup == null)
        {
            return false;
        }

        // Mixer は dB で受け取る。0〜1 をそのまま渡すと、スライダーの右半分で音量がほとんど変わらない。
        float decibel = _bgmVolume <= MIN_LINEAR_VOLUME
            ? MIN_DECIBEL
            : Mathf.Log10(_bgmVolume) * 20f;

        return _bgmGroup.audioMixer.SetFloat(_bgmVolumeParameter, decibel);
    }
}

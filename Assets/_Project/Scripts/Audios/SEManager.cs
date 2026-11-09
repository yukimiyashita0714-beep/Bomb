using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// SEEvents を購読して効果音を鳴らす。シーンの === SYSTEMS === の下に1つだけ置く。
///
/// 【AudioSource の使い方】
///   OneShot : 子に AudioSource をためておき、空いているものを使い回す。毎回作って消すとゴミが出るため。
///             上限まで使い切ったら、古いものから順に止めて使う。
///   ループ  : 「SEType ＋ 発生源」ごとに1つ作る。発生源があればその子にして位置を追いかける。
///
/// 【音量】
///   作る AudioSource はすべて Sfx グループに流す。SE スライダーは <see cref="SetSfxVolume"/> を呼ぶ。
///   値は PlayerPrefs に保存し、次に起動したときも同じ音量にする。
///
/// 【購読のタイミング】
///   OnEnable ではなく Awake で購読する。Unity は全オブジェクトの Awake を終えてから OnEnable を呼ぶので、
///   他のオブジェクトが OnEnable で StartLoop を呼んでも取りこぼさない。
/// </summary>
[DisallowMultipleComponent]
public class SEManager : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [Header("Data")]
    [SerializeField, Tooltip("効果音の登録表。SO_SEData を入れる")]
    private SEData _data;

    [Header("Mixer")]
    [SerializeField, Tooltip("効果音を流す Mixer グループ。AM_Main の Sfx を入れる")]
    private AudioMixerGroup _sfxGroup;

    [SerializeField, Tooltip("Sfx の音量として公開した Exposed Parameter の名前")]
    private string _sfxVolumeParameter = "SfxVolume";

    [SerializeField, Tooltip("保存された音量が無いときの音量 (0〜1)")]
    [Range(0f, 1f)]
    private float _defaultSfxVolume = 1f;

    [Header("3D Sound")]
    [SerializeField, Tooltip("この距離 (m) までは音量が下がらない")]
    [Range(0f, 50f)]
    private float _minDistance = 3f;

    [SerializeField, Tooltip("この距離 (m) を超えると聞こえなくなる")]
    [Range(1f, 200f)]
    private float _maxDistance = 20f;

    [SerializeField, Tooltip("距離による音量の下がり方")]
    private AudioRolloffMode _rolloffMode = AudioRolloffMode.Linear;

    [Header("Pool")]
    [SerializeField, Tooltip("OneShot を同時に鳴らせる数の上限。超えたら古い音から止める")]
    [Range(1, 64)]
    private int _maxOneShotSources = 16;

    // ==========================================
    // Private Fields
    // ==========================================

    private const string SFX_VOLUME_PREF_KEY = "Bomb.SfxVolume";

    /// <summary>音量 0 のときに Mixer へ渡す値 (dB)。Mixer の下限。</summary>
    private const float MIN_DECIBEL = -80f;

    /// <summary>これより小さい音量は無音として扱う。Log10(0) が -∞ になるのを避ける。</summary>
    private const float MIN_LINEAR_VOLUME = 0.0001f;

    private readonly List<AudioSource> _oneShotSources = new List<AudioSource>();

    /// <summary>_oneShotSources の同じ番号で、今鳴らしている SEType。同時再生数の上限を数えるのに使う。</summary>
    private readonly List<SEType> _oneShotTypes = new List<SEType>();

    private readonly Dictionary<LoopKey, AudioSource> _loopSources = new Dictionary<LoopKey, AudioSource>();

    /// <summary>空きが無いときに次に止める OneShot の番号。古い順に回す。</summary>
    private int _nextStealIndex;

    private float _sfxVolume;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>シーンにある SEManager。SE スライダーから音量を変えるときに使う。</summary>
    public static SEManager Instance { get; private set; }

    /// <summary>今の効果音の音量 (0〜1)。</summary>
    public float SfxVolume => _sfxVolume;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(SEManager)}] シーンに2つ以上ある。後から来た方を消す。", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_data == null)
        {
            Debug.LogError($"[{nameof(SEManager)}] SEData が未設定のため音を鳴らせない。", this);
        }

        if (_sfxGroup == null)
        {
            Debug.LogWarning($"[{nameof(SEManager)}] Sfx グループが未設定。音は鳴るが SE スライダーが効かない。", this);
        }

        _sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_PREF_KEY, _defaultSfxVolume);

        SEEvents.OnOneShotRequested += HandleOneShot;
        SEEvents.OnLoopStartRequested += HandleStartLoop;
        SEEvents.OnLoopStopRequested += HandleStopLoop;
    }

    private void Start()
    {
        // AudioMixer.SetFloat は Awake で呼ぶと反映されないことがあるので Start で入れる。
        ApplySfxVolume();
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        Instance = null;

        SEEvents.OnOneShotRequested -= HandleOneShot;
        SEEvents.OnLoopStartRequested -= HandleStartLoop;
        SEEvents.OnLoopStopRequested -= HandleStopLoop;

        // 発生源の子にしたループは自分と一緒に消えないので、ここで消す。
        StopAllLoops();
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>効果音の音量を変えて保存する。SE スライダーの OnValueChanged から呼ぶ。</summary>
    /// <param name="volume">0 で無音、1 で最大。</param>
    public void SetSfxVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SFX_VOLUME_PREF_KEY, _sfxVolume);
        ApplySfxVolume();
    }

    /// <summary>鳴っているループをすべて止める。</summary>
    public void StopAllLoops()
    {
        foreach (AudioSource source in _loopSources.Values)
        {
            if (source != null)
            {
                Destroy(source.gameObject);
            }
        }

        _loopSources.Clear();
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void HandleOneShot(SEType type, bool is3D, Vector3 position, float volume)
    {
        if (_data == null || !_data.TryGetEntry(type, out SEEntry entry))
        {
            return;
        }

        // Layered は1回で複数の AudioSource を使うので、上限も重ねる数の分だけ広げて数える。
        int sourcesPerPlay = entry.PlayMode == SEPlayMode.Layered ? CountValidClips(entry) : 1;

        if (entry.MaxSimultaneous > 0
            && CountPlayingOneShots(type) >= entry.MaxSimultaneous * sourcesPerPlay)
        {
            return;
        }

        // 重ねた音どうしで高さがばらばらだと1つの音に聞こえないので、ピッチは1回分まとめて決める。
        float pitch = entry.PickPitch();

        if (entry.PlayMode == SEPlayMode.Random)
        {
            PlayOneShotClip(type, entry, entry.PickClip(), is3D, position, volume, pitch);
            return;
        }

        IReadOnlyList<SEClip> clips = entry.Clips;

        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i] != null && clips[i].IsValid)
            {
                PlayOneShotClip(type, entry, clips[i], is3D, position, volume, pitch);
            }
        }
    }

    private void PlayOneShotClip(
        SEType type, SEEntry entry, SEClip clip, bool is3D, Vector3 position, float volume, float pitch)
    {
        int index = GetOneShotSourceIndex();
        AudioSource source = _oneShotSources[index];

        // 2D は位置に関係なく聞こえるので、自分の位置に置いておけば十分。
        source.transform.position = is3D ? position : transform.position;

        Play(source, entry, clip, is3D, volume, pitch, isLoop: false);
        _oneShotTypes[index] = type;
    }

    private void HandleStartLoop(SEType type, Transform owner, float volume)
    {
        if (_data == null)
        {
            return;
        }

        LoopKey key = new LoopKey(type, owner);

        if (_loopSources.TryGetValue(key, out AudioSource current))
        {
            if (current != null)
            {
                return;
            }

            // 発生源が Destroy されると子の AudioSource も消える。消え残った行を外して作り直す。
            _loopSources.Remove(key);
        }

        if (!_data.TryGetEntry(type, out SEEntry entry))
        {
            return;
        }

        bool is3D = owner != null;
        AudioSource source = CreateSource($"SE_Loop_{type}", is3D ? owner : transform);
        source.transform.localPosition = Vector3.zero;

        // ループは1つの AudioSource で回すので、Layered でも1つだけ選ぶ。
        Play(source, entry, entry.PickClip(), is3D, volume, entry.PickPitch(), isLoop: true);
        _loopSources.Add(key, source);
    }

    private void HandleStopLoop(SEType type, Transform owner)
    {
        LoopKey key = new LoopKey(type, owner);

        if (!_loopSources.TryGetValue(key, out AudioSource source))
        {
            return;
        }

        _loopSources.Remove(key);

        if (source != null)
        {
            Destroy(source.gameObject);
        }
    }

    private void Play(
        AudioSource source, SEEntry entry, SEClip clip, bool is3D, float volume, float pitch, bool isLoop)
    {
        source.outputAudioMixerGroup = entry.OutputOverride != null ? entry.OutputOverride : _sfxGroup;
        source.clip = clip.Clip;

        // clip を入れた後に設定する。入れる前に設定しても clip の差し替えで 0 に戻る。
        source.time = clip.StartSeconds;

        source.volume = entry.Volume * Mathf.Clamp01(volume);
        source.pitch = pitch;
        source.loop = isLoop;

        ApplySpatialSettings(source, is3D);

        // 待っている間も isPlaying は true になるので、使用中の AudioSource として扱われ横取りされない。
        if (clip.DelaySeconds > 0f)
        {
            source.PlayDelayed(clip.DelaySeconds);
            return;
        }

        source.Play();
    }

    private void ApplySpatialSettings(AudioSource source, bool is3D)
    {
        source.spatialBlend = is3D ? 1f : 0f;

        // 動く発生源でピッチが上下すると、足音などが不自然に聞こえるので切る。
        source.dopplerLevel = 0f;

        if (!is3D)
        {
            return;
        }

        source.rolloffMode = _rolloffMode;
        source.minDistance = _minDistance;
        source.maxDistance = _maxDistance;
    }

    private int GetOneShotSourceIndex()
    {
        for (int i = 0; i < _oneShotSources.Count; i++)
        {
            if (!_oneShotSources[i].isPlaying)
            {
                return i;
            }
        }

        if (_oneShotSources.Count < _maxOneShotSources)
        {
            _oneShotSources.Add(CreateSource($"SE_OneShot_{_oneShotSources.Count}", transform));
            _oneShotTypes.Add(SEType.None);
            return _oneShotSources.Count - 1;
        }

        // 全部鳴っているときは古い順に止めて使う。新しい音が鳴らない方が目立つため。
        int stolen = _nextStealIndex % _oneShotSources.Count;
        _nextStealIndex = stolen + 1;
        _oneShotSources[stolen].Stop();
        return stolen;
    }

    private static int CountValidClips(SEEntry entry)
    {
        int count = 0;
        IReadOnlyList<SEClip> clips = entry.Clips;

        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i] != null && clips[i].IsValid)
            {
                count++;
            }
        }

        return count;
    }

    private int CountPlayingOneShots(SEType type)
    {
        int count = 0;

        for (int i = 0; i < _oneShotSources.Count; i++)
        {
            if (_oneShotTypes[i] == type && _oneShotSources[i].isPlaying)
            {
                count++;
            }
        }

        return count;
    }

    private static AudioSource CreateSource(string objectName, Transform parent)
    {
        GameObject sourceObject = new GameObject(objectName);
        sourceObject.transform.SetParent(parent, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return source;
    }

    private void ApplySfxVolume()
    {
        if (_sfxGroup == null)
        {
            return;
        }

        // Mixer は dB で受け取る。0〜1 をそのまま渡すと、スライダーの右半分で音量がほとんど変わらない。
        float decibel = _sfxVolume <= MIN_LINEAR_VOLUME
            ? MIN_DECIBEL
            : Mathf.Log10(_sfxVolume) * 20f;

        if (!_sfxGroup.audioMixer.SetFloat(_sfxVolumeParameter, decibel))
        {
            Debug.LogWarning(
                $"[{nameof(SEManager)}] Exposed Parameter「{_sfxVolumeParameter}」が {_sfxGroup.audioMixer.name} に無い。",
                this);
        }
    }

    // ==========================================
    // Nested Types
    // ==========================================

    /// <summary>ループを見分ける鍵。同じ音でも発生源が違えば別のループにする。</summary>
    private readonly struct LoopKey : IEquatable<LoopKey>
    {
        private readonly SEType _type;

        /// <summary>発生源の InstanceID。2D (発生源なし) は 0。Destroy された後も同じ値なので止められる。</summary>
        private readonly int _ownerId;

        public LoopKey(SEType type, Transform owner)
        {
            _type = type;
            _ownerId = owner != null ? owner.GetInstanceID() : 0;
        }

        public bool Equals(LoopKey other) => _type == other._type && _ownerId == other._ownerId;

        public override bool Equals(object obj) => obj is LoopKey other && Equals(other);

        public override int GetHashCode() => ((int)_type * 397) ^ _ownerId;
    }
}

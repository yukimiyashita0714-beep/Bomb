using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

/// <summary>
/// 効果音の登録表。SEType ごとに鳴らす音と鳴らし方を持つ。
/// 音を足すときは SEType に1行足し、ここに Entry を1つ追加する。
/// SEManager の Inspector から渡す。Resources には置かない (AudioClip ごと常に読み込まれるため)。
/// </summary>
[CreateAssetMenu(fileName = "SO_SEData", menuName = "Bomb/SE Data")]
public class SEData : ScriptableObject
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("効果音の一覧。同じ SEType を2つ登録しない")]
    private List<SEEntry> _entries = new List<SEEntry>();

    // ==========================================
    // Private Fields
    // ==========================================

    /// <summary>毎回リストを先頭から探さないための引き当て表。初回に作る。</summary>
    private Dictionary<SEType, SEEntry> _lookup;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void OnEnable()
    {
        _lookup = null;
    }

    private void OnValidate()
    {
        // Inspector で書き換えたら作り直す。重複はここで気付けるように警告する。
        _lookup = null;
        WarnDuplicates();
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>登録内容を返す。登録が無い・Clip が空なら false。</summary>
    public bool TryGetEntry(SEType type, out SEEntry entry)
    {
        if (_lookup == null)
        {
            BuildLookup();
        }

        if (_lookup.TryGetValue(type, out entry) && entry.HasClip)
        {
            return true;
        }

        Debug.LogWarning($"[{nameof(SEData)}] {type} の音が登録されていない。", this);
        return false;
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void BuildLookup()
    {
        _lookup = new Dictionary<SEType, SEEntry>();

        for (int i = 0; i < _entries.Count; i++)
        {
            SEEntry entry = _entries[i];

            // 重複したら先に書いた方を使う。後ろの行を足しても挙動が変わらないように。
            if (entry != null && !_lookup.ContainsKey(entry.Type))
            {
                _lookup.Add(entry.Type, entry);
            }
        }
    }

    private void WarnDuplicates()
    {
        HashSet<SEType> seen = new HashSet<SEType>();

        for (int i = 0; i < _entries.Count; i++)
        {
            SEEntry entry = _entries[i];

            if (entry != null && !seen.Add(entry.Type))
            {
                Debug.LogWarning($"[{nameof(SEData)}] {entry.Type} が2回登録されている。上の行だけが使われる。", this);
            }
        }
    }
}

/// <summary>登録した Clip の鳴らし方。</summary>
public enum SEPlayMode
{
    /// <summary>毎回1つだけランダムに選んで鳴らす。</summary>
    Random = 0,

    /// <summary>全部を同時に重ねて鳴らす。</summary>
    Layered = 1,
}

/// <summary>1種類の効果音の登録内容。</summary>
[Serializable]
public class SEEntry
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("どの音として登録するか")]
    private SEType _type = SEType.None;

    [SerializeField, Tooltip("Random: 毎回1つだけ選んで鳴らす (同じ音の繰り返し感を減らす)\nLayered: 全部を同時に重ねて1つの音にする")]
    private SEPlayMode _playMode = SEPlayMode.Random;

    [SerializeField, Tooltip("鳴らす音。Play Mode が Random なら1つ選び、Layered なら全部鳴らす")]
    private SEClip[] _clips = Array.Empty<SEClip>();

    [SerializeField, Tooltip("音量 (0〜1)。全体の音量は AudioMixer の Sfx で変える")]
    [Range(0f, 1f)]
    private float _volume = 1f;

    [SerializeField, Tooltip("ピッチの最小値。最大値と同じならピッチは変わらない")]
    [Range(0.1f, 3f)]
    private float _pitchMin = 1f;

    [SerializeField, Tooltip("ピッチの最大値。少しばらつかせると連発しても機械的に聞こえない")]
    [Range(0.1f, 3f)]
    private float _pitchMax = 1f;

    [SerializeField, Tooltip("同時に鳴らせる数の上限。0 なら上限なし。グレネード連発で音が割れるときに絞る")]
    [Range(0, 32)]
    private int _maxSimultaneous = 0;

    [SerializeField, Tooltip("出力先の Mixer グループ。空なら SEManager の Sfx グループに流す")]
    private AudioMixerGroup _outputOverride;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>どの音として登録しているか。</summary>
    public SEType Type => _type;

    /// <summary>Clip を1つ選ぶか、全部重ねるか。</summary>
    public SEPlayMode PlayMode => _playMode;

    /// <summary>登録した Clip。空の欄も含む。</summary>
    public IReadOnlyList<SEClip> Clips => _clips;

    /// <summary>音量 (0〜1)。</summary>
    public float Volume => _volume;

    /// <summary>同時に鳴らせる数の上限。0 なら上限なし。</summary>
    public int MaxSimultaneous => _maxSimultaneous;

    /// <summary>出力先の Mixer グループ。null なら既定の Sfx。</summary>
    public AudioMixerGroup OutputOverride => _outputOverride;

    /// <summary>鳴らせる Clip が1つ以上あるなら true。</summary>
    public bool HasClip
    {
        get
        {
            if (_clips == null)
            {
                return false;
            }

            for (int i = 0; i < _clips.Length; i++)
            {
                if (_clips[i] != null && _clips[i].Clip != null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>登録した Clip からランダムに1つ返す。空の欄は飛ばす。</summary>
    public SEClip PickClip()
    {
        if (!HasClip)
        {
            return null;
        }

        // 空の欄が混ざっていても止まらないよう、当たるまで引き直す。HasClip で1つはあると分かっている。
        while (true)
        {
            SEClip candidate = _clips[Random.Range(0, _clips.Length)];

            if (candidate != null && candidate.Clip != null)
            {
                return candidate;
            }
        }
    }

    /// <summary>ピッチの範囲からランダムに1つ返す。</summary>
    public float PickPitch()
    {
        float min = Mathf.Min(_pitchMin, _pitchMax);
        float max = Mathf.Max(_pitchMin, _pitchMax);
        return Random.Range(min, max);
    }
}

/// <summary>1つの音源と、その音源をいつ・どこから鳴らすか。</summary>
[Serializable]
public class SEClip
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("鳴らす音源")]
    private AudioClip _clip;

    [SerializeField, Tooltip("音源の頭を何秒飛ばして鳴らすか。音源の最初に無音があって実際の動きより遅れて聞こえるときに増やす")]
    [Min(0f)]
    private float _startSeconds = 0f;

    [SerializeField, Tooltip("何秒遅らせて鳴らすか。Layered で「ドーン」の後に破片の音を続けるときなどに使う")]
    [Min(0f)]
    private float _delaySeconds = 0f;

    // ==========================================
    // Private Fields
    // ==========================================

    /// <summary>開始位置を後ろへずらしすぎても、最低これだけは鳴らす (秒)。</summary>
    private const float MIN_REMAINING_SECONDS = 0.01f;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>鳴らす音源。</summary>
    public AudioClip Clip => _clip;

    /// <summary>鳴らすまでに待つ時間 (秒)。</summary>
    public float DelaySeconds => _delaySeconds;

    /// <summary>
    /// 再生を始める位置 (秒)。音源の長さを超えないように詰めて返す。
    /// ループでは最初の1回だけ効き、2周目からは音源の頭から鳴る (AudioSource の仕様)。
    /// </summary>
    public float StartSeconds
    {
        get
        {
            if (_clip == null)
            {
                return 0f;
            }

            // 長さちょうどを渡すと AudioSource がエラーを出すので、少し手前で止める。
            return Mathf.Clamp(_startSeconds, 0f, Mathf.Max(0f, _clip.length - MIN_REMAINING_SECONDS));
        }
    }

    /// <summary>この欄に鳴らせる音源が入っているなら true。</summary>
    public bool IsValid => _clip != null;
}

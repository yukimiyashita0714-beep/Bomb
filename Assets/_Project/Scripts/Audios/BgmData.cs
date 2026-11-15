using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BGM の登録表。BgmType ごとに流す曲と流し方を持つ。1行＝1曲。
/// 曲を足すときは BgmType に1行足し、ここに Entry を1つ追加する。
/// BgmManager の Inspector から渡す。Resources には置かない (AudioClip ごと常に読み込まれるため)。
/// </summary>
[CreateAssetMenu(fileName = "SO_BgmData", menuName = "Bomb/BGM Data")]
public class BgmData : ScriptableObject
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("BGM の一覧。同じ BgmType を2つ登録しない")]
    private List<BgmEntry> _entries = new List<BgmEntry>();

    // ==========================================
    // Private Fields
    // ==========================================

    /// <summary>毎回リストを先頭から探さないための引き当て表。初回に作る。</summary>
    private Dictionary<BgmType, BgmEntry> _lookup;

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
    public bool TryGetEntry(BgmType type, out BgmEntry entry)
    {
        if (_lookup == null)
        {
            BuildLookup();
        }

        if (_lookup.TryGetValue(type, out entry) && entry.Clip != null)
        {
            return true;
        }

        Debug.LogWarning($"[{nameof(BgmData)}] {type} の曲が登録されていない。", this);
        return false;
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void BuildLookup()
    {
        _lookup = new Dictionary<BgmType, BgmEntry>();

        for (int i = 0; i < _entries.Count; i++)
        {
            BgmEntry entry = _entries[i];

            // 重複したら先に書いた方を使う。後ろの行を足しても挙動が変わらないように。
            if (entry != null && !_lookup.ContainsKey(entry.Type))
            {
                _lookup.Add(entry.Type, entry);
            }
        }
    }

    private void WarnDuplicates()
    {
        HashSet<BgmType> seen = new HashSet<BgmType>();

        for (int i = 0; i < _entries.Count; i++)
        {
            BgmEntry entry = _entries[i];

            if (entry != null && !seen.Add(entry.Type))
            {
                Debug.LogWarning($"[{nameof(BgmData)}] {entry.Type} が2回登録されている。上の行だけが使われる。", this);
            }
        }
    }
}

/// <summary>1曲の登録内容。</summary>
[Serializable]
public class BgmEntry
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("どの BGM として登録するか")]
    private BgmType _type = BgmType.None;

    [SerializeField, Tooltip("流す音源")]
    private AudioClip _clip;

    [SerializeField, Tooltip("音量 (0〜1)。曲ごとの大きさのバラツキをそろえるのに使う。全体の音量は BGM スライダーで変える")]
    [Range(0f, 1f)]
    private float _volume = 0.6f;

    [SerializeField, Tooltip("オンなら最後まで流れたら頭に戻る。オフなら1回流して止まる")]
    private bool _loop = true;

    [SerializeField, Tooltip("音源の頭を何秒飛ばして流すか。最初の1周だけ効く")]
    [Min(0f)]
    private float _startSeconds = 0f;

    // ==========================================
    // Private Fields
    // ==========================================

    /// <summary>開始位置を後ろへずらしすぎても、最低これだけは流す (秒)。</summary>
    private const float MIN_REMAINING_SECONDS = 0.01f;

    // ==========================================
    // Properties
    // ==========================================

    /// <summary>どの BGM として登録しているか。</summary>
    public BgmType Type => _type;

    /// <summary>流す音源。</summary>
    public AudioClip Clip => _clip;

    /// <summary>音量 (0〜1)。</summary>
    public float Volume => _volume;

    /// <summary>最後まで流れたら頭に戻るなら true。</summary>
    public bool Loop => _loop;

    /// <summary>再生を始める位置 (秒)。音源の長さを超えないように詰めて返す。</summary>
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
}

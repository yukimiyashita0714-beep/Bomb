using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 壁の破片 (<see cref="WallDebrisPiece"/>) を使い回す置き場。
/// 壁が壊れるたびに破片を作って消すと重くなるので、消えた破片はここにしまって次に出すときに使う。
/// 同時に出ている数が上限を超えたら、いちばん古い破片を消して使い回す。
/// シーンに置く必要はない。初めて破片を出すときに自動で置き場を作り、シーンが変われば一緒に消える。
/// </summary>
public static class WallDebrisPool
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string ROOT_NAME = "WallDebrisPool";

    /// <summary>重なりを押し戻すときの速さの上限 (m/s)。</summary>
    private const float MAX_DEPENETRATION_VELOCITY = 3f;

    private static Transform _root;

    private static readonly Stack<WallDebrisPiece> InactivePieces = new Stack<WallDebrisPiece>();

    /// <summary>出ている破片。古い順に並ぶので、上限を超えたら先頭から使い回す。</summary>
    private static readonly List<WallDebrisPiece> ActivePieces = new List<WallDebrisPiece>();

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>
    /// 使える破片を1個返す (表示した状態)。置き場に無ければ作る。
    /// 出ている数が上限に達していたら、いちばん古いものを引き上げて返す。
    /// </summary>
    public static WallDebrisPiece Get(int maxActiveCount)
    {
        EnsureRoot();

        WallDebrisPiece piece;

        if (ActivePieces.Count >= maxActiveCount && ActivePieces.Count > 0)
        {
            piece = ActivePieces[0];
            ActivePieces.RemoveAt(0);
        }
        else if (InactivePieces.Count > 0)
        {
            piece = InactivePieces.Pop();
        }
        else
        {
            piece = CreatePiece();
        }

        piece.gameObject.SetActive(true);
        ActivePieces.Add(piece);
        return piece;
    }

    /// <summary>消えた破片をしまう。</summary>
    public static void Release(WallDebrisPiece piece)
    {
        if (piece == null)
        {
            return;
        }

        ActivePieces.Remove(piece);
        piece.gameObject.SetActive(false);
        InactivePieces.Push(piece);
    }

    /// <summary>指定の壁が出した破片のうち、まだ出ているものを縮めて片付ける。</summary>
    public static void ShrinkPiecesOf(Object owner, float seconds)
    {
        for (int i = 0; i < ActivePieces.Count; i++)
        {
            if (ActivePieces[i] != null && ActivePieces[i].Owner == owner)
            {
                ActivePieces[i].ShrinkNow(seconds);
            }
        }
    }

    // ==========================================
    // Private Methods
    // ==========================================

    /// <summary>ドメインリロード無しで再生したときに、前回の置き場が残らないようにする。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _root = null;
        InactivePieces.Clear();
        ActivePieces.Clear();
    }

    private static void EnsureRoot()
    {
        if (_root != null)
        {
            return;
        }

        // シーンが変わると置き場ごと破片も消えているので、覚えていた破片を捨てて作り直す。
        InactivePieces.Clear();
        ActivePieces.Clear();
        _root = new GameObject(ROOT_NAME).transform;
    }

    private static WallDebrisPiece CreatePiece()
    {
        GameObject pieceObject = new GameObject(
            "WallDebrisPiece",
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(MeshCollider),
            typeof(Rigidbody));

        pieceObject.transform.SetParent(_root, false);

        Rigidbody body = pieceObject.GetComponent<Rigidbody>();
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;

        // 出た瞬間に破片どうしや床と重なっていても、ものすごい速さで弾き出されないようにする。
        body.maxDepenetrationVelocity = MAX_DEPENETRATION_VELOCITY;

        return pieceObject.AddComponent<WallDebrisPiece>();
    }
}

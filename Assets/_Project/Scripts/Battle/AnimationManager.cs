using Bomb.Core;
using UnityEngine;

public class AnimationManager : MonoBehaviour
{
    public enum AnimationKind
    {
        None,
        Escape,
    }

    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("アニメーションを起動させる用")]
    private Animator _animator;

    [SerializeField, Tooltip("アニメーションに使われる敵のアニメーションたち")]
    private Animator[] _animatorEnemies;

    [SerializeField, Tooltip("爆弾の設置・爆発を知らせるイベント。空ならシーンから探す")]
    private BombEvent _bombEvent;

    // ==========================================
    // Public Fields
    // ==========================================

    public bool Animation { get; private set; }

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Awake()
    {
        if (_bombEvent == null)
        {
            _bombEvent = FindFirstObjectByType<BombEvent>();
        }
    }

    private void OnEnable()
    {
        _bombEvent.OnPlant += Escape;
    }

    private void OnDisable()
    {
        _bombEvent.OnPlant -= Escape;
    }

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary> アニメーション起動 </summary>
    public void Escape()
    {
        _animator.SetTrigger("Escape");
    }

    // ==========================================
    // Animator
    // ==========================================

    /// <summary> アニメーションの状態を変える </summary>
    private void ChangeAnimationState(AnimationKind animation)
    {
        Animation = animation != AnimationKind.None;
    }

    /// <summary> 敵のアニメーターを何もしないにさせる </summary>
    private void EnemySetIdle(int number)
    {
        _animatorEnemies[number].SetFloat("Move", 0);
    }

    /// <summary> 敵のアニメーターを移動にさせる </summary>
    private void EnemySetMove(int number)
    {
        _animatorEnemies[number].SetFloat("Move", 1);
    }

    /// <summary> 敵のアニメーターを何もしないにさせる </summary>
    private void EnemySetDie(int number)
    {
        _animatorEnemies[number].SetTrigger("Died");
    }
}

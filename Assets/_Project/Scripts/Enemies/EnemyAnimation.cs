using UnityEngine;

public class EnemyAnimation : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField] private EnemyData _data;

    [SerializeField] private Animator _animator;

    [SerializeField] private EnemyHealth _health;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void OnEnable()
    {
        _health.OnDied += OnDied;
        SetAnimationSpeed();
    }

    private void Start()
    {
        Move(0);
    }

    private void OnDisable()
    {
        _health.OnDied -= OnDied;
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void SetAnimationSpeed()
    {
        _animator.SetFloat("AnimationSpeed", _data.AnimationSpeed);
    }

    private void Move(float move)
    {
        _animator.SetFloat("Move", move);
    }

    private void OnDied()
    {
        _animator.SetTrigger("Died");
    }
}

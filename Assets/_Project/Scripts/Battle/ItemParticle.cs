using System.Collections;
using UnityEngine;

public class ItemParticle : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("ê∂ë∂éûä‘")]
    private float _lifeTime = 1f;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Start()
    {
        StartCoroutine(LifeTime());
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private IEnumerator LifeTime()
    {
        yield return new WaitForSeconds(_lifeTime);

        Destroy(gameObject);
    }
}

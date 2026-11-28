using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyItemDrop : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("倒れた瞬間を検知する")]
    private EnemyHealth _enemyHealth;

    [SerializeField, Tooltip("落とすものリスト")]
    private List<EnemyItemDropDetails> _itemDrops;

    [SerializeField, Tooltip("ドロップアイテムの飛び出す方向")]
    private Vector3 _dropVel;

    [SerializeField, Tooltip("dropVelの追加ランダム方向")]
    private Vector3 _dropRandomVel;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void OnEnable()
    {
        if (_enemyHealth == null)
        {
            return;
        }

        _enemyHealth.OnDied += ItemDrop;
    }

    private void OnDisable()
    {
        if (_enemyHealth == null)
        {
            return;
        }

        _enemyHealth.OnDied -= ItemDrop;
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void ItemDrop()
    {
        var randomNumber = UnityEngine.Random.Range(0, 101);

        foreach (var item in _itemDrops)
        {
            if (item._minParcent <= randomNumber &&
                randomNumber < item._maxParcent)
            {
                var dropItem = Instantiate(item._item);
                dropItem.transform.position = transform.position;
                
                if (dropItem.TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    var randomVel = new Vector3(
                        UnityEngine.Random.Range(-_dropRandomVel.x, _dropRandomVel.x),
                        UnityEngine.Random.Range(-_dropRandomVel.y, _dropRandomVel.y),
                        UnityEngine.Random.Range(-_dropRandomVel.z, _dropRandomVel.z));

                    var vel = transform.TransformDirection(_dropVel + _dropRandomVel);

                    rb.AddForce(vel, ForceMode.Impulse);
                }
            }
        }
    }

    // ==========================================
    // Animator
    // ==========================================
}

[Serializable]
public class EnemyItemDropDetails
{
    [Header("アイテム")]
    [Tooltip("アイテムドロップするプレファブ")] public Transform _item;
    [Header("ドロップ確率 (0 ～ 100)")]
    [Tooltip("ドロップ確率の下の値")] public float _minParcent = 0;
    [Tooltip("ドロップ確率の上の値")] public float _maxParcent = 50;
}
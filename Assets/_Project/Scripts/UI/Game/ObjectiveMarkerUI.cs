using Bomb.Battle;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class ObjectiveMarkerUI : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField, Tooltip("印となるオブジェクト分生成されるオブジェクト")]
    private Transform _marker;

    [SerializeField, Tooltip("マーカーをリストで管理する")]
    private List<Transform> _markerList;

    // ==========================================
    // Private Fields
    // ==========================================

    // ==========================================
    // Properties
    // ==========================================

    // ==========================================
    // Unity Messages
    // ==========================================

    private void Start()
    {
        SetMarkerList();
    }

    // ==========================================
    // Public Methods
    // ==========================================

    // ==========================================
    // Private Methods
    // ==========================================

    /// <summary>
    /// リストをリセットする
    /// 
    /// 見つける対象を増やす場合、
    /// 
    /// SetMarkerObjects(
    /// FindMarkerObjects(見つけたいComponent),
    /// MarkerObjectUI.MarkerKind);
    /// 
    /// と入力するだけで増やせます
    /// </summary>
    private void SetMarkerList()
    {
        foreach (var marker in _markerList)
        {
            Destroy(marker);
            _markerList.Remove(marker);
        }

        var targets = FindMarkerObjects<MainTarget>();
        List<Transform> mainTarget = new List<Transform>();
        List<Transform> subTarget = new List<Transform>();

        foreach (var target in targets)
        {
            if (target.GetComponent<MainTarget>().MainBomb)
            {
                mainTarget.Add(target);
            }
            else
            {
                subTarget.Add(target);
            }
        }

        SetMarkerObjects(
            mainTarget,
            MarkerObjectUI.MarkerKind.MainTarget);

        SetMarkerObjects(
            subTarget,
            MarkerObjectUI.MarkerKind.SubTarget);
    }

    /// <summary>
    /// あるComponentをついているオブジェクトを探しマーカーを付ける対象にする
    /// </summary>
    private List<Transform> FindMarkerObjects<T>() where T : Component
    {
        //ターゲットのオブジェクトを探す
        T[] markerList = GameObject.FindObjectsByType<T>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);

        //ターゲットのトランスフォームを取得
        List<Transform> markerTransformList = new List<Transform>();

        //メインターゲットのオブジェクト
        foreach (var marker in markerList)
        {
            markerTransformList.Add(marker.transform);
        }

        return markerTransformList;
    }

    /// <summary>
    /// マーカーを作る
    /// </summary>
    /// <param name="markerObjects"></param>
    /// <param name="kind"></param>
    private void SetMarkerObjects(List<Transform> markerObjects, MarkerObjectUI.MarkerKind kind)
    {
        for (int i = 0; i < markerObjects.Count; i++)
        {
            var marker = Instantiate(_marker, transform);
            marker.GetComponent<MarkerObjectUI>().GetTargetObject(markerObjects[i], kind);
            _markerList.Add(marker);
        }
    }
}

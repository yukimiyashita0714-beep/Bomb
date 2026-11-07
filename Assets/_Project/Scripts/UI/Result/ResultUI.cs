using Bomb.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class ResultUI : MonoBehaviour
{
    // ==========================================
    // Serialize Fields
    // ==========================================

    [SerializeField] private Animator _animator;

    [SerializeField, Tooltip("リザルト出た場合に最初に選択されるボタン")] 
    private GameObject _setButton;

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>
    /// 表示非表示
    /// </summary>
    /// <param name="enabled"></param>
    public void SetDisplay(bool enabled)
    {
        _animator.SetBool("Display", enabled);

        SetButton(enabled);
    }

    public void OnRestart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void SetButton(bool enabled)
    {
        if (enabled)
        {
            EventSystem.current.SetSelectedGameObject(_setButton);
        }
        else
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    // ==========================================
    // Debug
    // ==========================================

    [ContextMenu("Result")]
    private void ResultDebug()
    {
        SetDisplay(true);
    }
}

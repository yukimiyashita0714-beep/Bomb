using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Bomb.UI
{
    public class ResultUI : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [SerializeField] private Animator _animator;

        [SerializeField, Tooltip("リザルトを表示されたら選択されるボタン")]
        private GameObject _selectButton;

        // ==========================================
        // Public Methods
        // ==========================================

        /// <summary>
        /// リザルトを表示非表示
        /// </summary>
        /// <param name="enabled"></param>
        public void SetDisplay(bool enabled)
        {
            _animator.SetBool("Display", enabled);

            SetButton(enabled);
        }

        public void OnRestart()
        {
            Debug.Log("Restart");

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // ==========================================
        // Private Methods
        // ==========================================

        /// <summary>
        /// ボタンを設定する
        /// </summary>
        /// <param name="enabled"></param>
        private void SetButton(bool enabled)
        {
            if (_selectButton == null)
            {
                return;
            }

            switch (enabled)
            {
                case true:

                    EventSystem.current.SetSelectedGameObject(_selectButton);

                    break;

                case false:

                    EventSystem.current.SetSelectedGameObject(null);

                    break;
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
}

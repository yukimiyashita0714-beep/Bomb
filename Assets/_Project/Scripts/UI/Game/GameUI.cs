using Bomb.Core;
using UnityEngine;

namespace Bomb.UI
{
    public class GameUI : MonoBehaviour
    {
        private enum State
        {
            Enable,
            Disable,
        }

        // ==========================================
        // Serialize Fields
        // ==========================================

        [SerializeField] private Animator _animator;

        [SerializeField] private BombEvent _bombEvent;

        // ==========================================
        // Private Fields
        // ==========================================

        // ==========================================
        // Properties
        // ==========================================

        // ==========================================
        // Events
        // ==========================================

        // ==========================================
        // Unity Messages
        // ==========================================

        private void OnEnable()
        {
            if (_bombEvent == null)
            {
                Debug.LogWarning("BombEventをアタッチして～");
                return;
            }

            _bombEvent.OnExplosion += GameUIDisplayDisable;
        }

        private void OnDisable()
        {
            if (_bombEvent == null)
            {
                Debug.LogWarning("BombEventをアタッチして～");
                return;
            }

            _bombEvent.OnExplosion -= GameUIDisplayDisable;
        }

        // ==========================================
        // Public Methods
        // ==========================================

        // ==========================================
        // Private Methods
        // ==========================================

        private void GameUIDisplayDisable()
        {
            GameUIDisplay(State.Disable);
        }

        private void GameUIDisplay(State state)
        {
            switch (state)
            {
                case State.Enable:

                    _animator.SetBool("Display", true);

                    break;

                case State.Disable:

                    _animator.SetBool("Display", false);

                    break;
            }
        }
    }
}
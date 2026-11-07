using System;
using UnityEngine;
using Bomb.Core;

namespace Bomb.Player
{
    public class PlayerInteractController : MonoBehaviour
    {
        //Playerがインタラクトを見つける/実行させるためのクラス

        // ==========================================
        // Private Fields
        // ==========================================

        /// <summary> 受け取ったインターフェイスを管理する </summary>
        private IInteractable _currentInteractable;

        [SerializeField] private PlayerData _data;
        [SerializeField] private PlayerInputReceiver _inputReceiver;

        /// <summary> 受け取ったコライダーを格納する </summary>
        private readonly Collider[] _results = new Collider[10];

        // ==========================================
        // Events
        // ==========================================

        /// <summary> インターフェイスを表示する </summary>
        public event Action<InteractKind> OnInteractEnable;

        /// <summary> インターフェイスを非表示にする </summary>
        public event Action OnInteractDisable;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Update()
        {
            SearchInteractable();

            if (!_inputReceiver.ConsumeInteractRequest())
            {
                return;
            }

            Interact();
        }

        // ==========================================
        // Public Methods
        // ==========================================

        public void Interact()
        {
            _currentInteractable?.Interact();
        }

        // ==========================================
        // Private Methods
        // ==========================================

        /// <summary>
        /// インターフェイスを探す
        /// </summary>
        private void SearchInteractable()
        {
            //プレイヤーの周りのコライダーを検知
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                _data.InteractRadius,
                _results
            );

            IInteractable found = null;

            //先にResultに入ったインターフェイスを表示する
            foreach (var result in _results)
            {
                for (int i = 0; i < count; i++)
                {
                    if (_results[i].TryGetComponent<IInteractable>(out var interactable) &&
                        interactable.IsInteractable)
                    {
                        found = interactable;
                        break;
                    }
                }

                //更新しない場合
                if (_currentInteractable == found)
                {
                    return;
                }

                //インターフェイス更新

                //インターフェイスが前になかった場合
                if (_currentInteractable != null)
                {
                    OnInteractDisable?.Invoke();
                }

                _currentInteractable = found;

                //インターフェイスが見つからなかった場合
                if (_currentInteractable != null)
                {
                    OnInteractEnable?.Invoke(_currentInteractable.InteractKind);
                }
            }
        }
    }
}
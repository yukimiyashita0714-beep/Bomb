using Bomb.Core;
using Bomb.Player;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Bomb.UI
{
    public class InteractUI : MonoBehaviour
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [SerializeField] private PlayerInteractController _interactPlayerController;

        [SerializeField] private TextMeshProUGUI _interactText;

        // ==========================================
        // Private Fields
        // ==========================================

        private List<InteractKind> _interactKindList;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Awake()
        {
            _interactKindList = new List<InteractKind>();
        }

        private void OnEnable()
        {
            _interactPlayerController.OnInteractEnable += AddInteract;
            _interactPlayerController.OnInteractDisable += RemoveInteract;
        }

        private void OnDisable()
        {
            _interactPlayerController.OnInteractEnable -= AddInteract;
            _interactPlayerController.OnInteractDisable -= RemoveInteract;
        }

        // ==========================================
        // Public Methods
        // ==========================================

        /// <summary>
        /// インタラクトを増やす
        /// </summary>
        /// <param name="interactKind"></param>
        public void AddInteract(InteractKind interactKind)
        {
            _interactKindList.Add(interactKind);
            InteractListUpdate();
        }

        /// <summary>
        /// インタラクトを減らす
        /// </summary>
        public void RemoveInteract()
        {
            _interactKindList.Clear();
            InteractListUpdate();
        }

        // ==========================================
        // Private Methods
        // ==========================================

        /// <summary>
        /// インタラクトの表示更新
        /// </summary>
        private void InteractListUpdate()
        {
            string text = string.Empty;
            int count = 0;

            if (_interactKindList.Count <= 0)
            {
                _interactText.text = text;
                return;
            }

            //複数選択の場合を想定して作ろうとしたが、
            //一旦保留
            //この案がこのまま通り、複数使用しなければ行けない場合作り直す
            foreach (var interact in _interactKindList)
            {
                switch (interact)
                {
                    case InteractKind.BombPlant:

                        text += "Bomb Plant";

                        break;

                    case InteractKind.BombExplosion:

                        text += "Bomb Explosion";

                        break;
                }

                switch (count)
                {
                    case 0:

                        text += "\nE button";

                        break;

                    case 1:

                        text += "\nQ button";

                        break;

                    case 2:

                        text += "\nX button";

                        break;
                }

                text += "\n\n";

                count++;
            }

            //更新
            _interactText.text = text;
        }
    }
}
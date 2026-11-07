using UnityEngine;

namespace Bomb.Core
{
    /// <summary>
    /// インタラクトの呼び出し
    /// Bomb.BattleからBomb.UIに受け渡すためのインターフェイス
    /// 
    /// IsInteractable : インターフェイスを表示非表示
    /// InteractKind : インターフェイスの種類
    /// Interact() : インタラクトを呼ばれたときの処理
    /// </summary>
    public interface IInteractable
    {
        bool IsInteractable { get; }

        InteractKind InteractKind { get; }

        void Interact();
    }
}

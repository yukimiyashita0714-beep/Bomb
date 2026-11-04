namespace Bomb.Core
{
    /// <summary>
    /// ダメージを受けられるもの。爆風などの攻撃側は、相手が何者かを知らずにこれだけ見て当てる。
    /// Battle と Player が互いを参照せずに済むよう、両方が参照できる Core に置いている。
    /// </summary>
    public interface IDamageable
    {
        /// <summary>ダメージを与える。</summary>
        /// <param name="amount">ダメージ量。0 以下は呼ぶ側で弾く。</param>
        void TakeDamage(float amount);
    }
}

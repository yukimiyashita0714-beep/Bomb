using UnityEngine;

namespace Bomb.Player
{
    /// <summary>
    /// 投げたものが描く放物線を計算する。MonoBehaviour に依存しない純粋な計算。
    /// 衝突も空気抵抗も見ないので、壁に当たるまでの見込み線としてだけ使う。
    /// </summary>
    public static class ProjectilePath
    {
        // ==========================================
        // Public Methods
        // ==========================================

        /// <summary>
        /// 放物線上の点を <paramref name="buffer"/> に詰め、書き込んだ個数を返す。
        /// 地面 (<paramref name="groundY"/>) を下回った時点で打ち切る。
        /// </summary>
        /// <param name="origin">投げ出す位置</param>
        /// <param name="velocity">初速度 (m/s)</param>
        /// <param name="gravity">重力加速度 (m/s^2)</param>
        /// <param name="groundY">これより下に落ちたら打ち切る高さ</param>
        /// <param name="timeStep">点と点の時間間隔 (秒)</param>
        /// <param name="buffer">書き込み先。長さが上限になる</param>
        public static int Sample(
            Vector3 origin,
            Vector3 velocity,
            Vector3 gravity,
            float groundY,
            float timeStep,
            Vector3[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return 0;
            }

            buffer[0] = origin;

            for (int i = 1; i < buffer.Length; i++)
            {
                float time = timeStep * i;
                Vector3 point = origin + velocity * time + 0.5f * gravity * (time * time);

                if (point.y <= groundY)
                {
                    // 地面をまたいだ区間だけ線形に戻して、着地点をきれいに合わせる。
                    Vector3 previous = buffer[i - 1];
                    float span = previous.y - point.y;
                    float ratio = span > Mathf.Epsilon ? (previous.y - groundY) / span : 0f;

                    buffer[i] = Vector3.Lerp(previous, point, ratio);
                    return i + 1;
                }

                buffer[i] = point;
            }

            return buffer.Length;
        }
    }
}

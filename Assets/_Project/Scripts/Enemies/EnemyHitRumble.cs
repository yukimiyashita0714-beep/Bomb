using Bomb.Core;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 敵にグレネードが当たった瞬間、コントローラーを短く振動させる。倒した1発は少し強く長く。
/// コントローラーで遊んでいるとき (<see cref="InputDeviceWatcher.IsGamepad"/>) だけ振動させる。
/// 振動は自分で止めないと鳴りっぱなしになるので、時間が来たとき・ポーズしたとき・
/// アプリが裏に回ったとき・このコンポーネントが無効になったときに必ず止める。
/// <see cref="EnemyHitFeedback.OnAnyEnemyHit"/> を聞くので、シーンに1つ置けばよい。
/// </summary>
[DisallowMultipleComponent]
public class EnemyHitRumble : MonoBehaviour
{
    // ==========================================
    // Private Fields
    // ==========================================

    /// <summary>振動させているコントローラー。途中で別のコントローラーに持ち替えても、これを止める。</summary>
    private Gamepad _rumblingGamepad;

    /// <summary>振動を止める時刻 (Time.unscaledTime)。ポーズで時間が止まっても必ず止まるよう unscaled で持つ。</summary>
    private float _stopAtUnscaledTime;

    // ==========================================
    // Unity Messages
    // ==========================================

    private void OnEnable()
    {
        EnemyHitFeedback.OnAnyEnemyHit += HandleEnemyHit;
    }

    private void OnDisable()
    {
        EnemyHitFeedback.OnAnyEnemyHit -= HandleEnemyHit;
        StopRumble();
    }

    private void Update()
    {
        if (_rumblingGamepad == null)
        {
            return;
        }

        // ポーズ中は Time.timeScale が 0 になる。メニューを開いたまま震え続けないようにする。
        if (Time.unscaledTime >= _stopAtUnscaledTime || Time.timeScale <= 0f)
        {
            StopRumble();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            StopRumble();
        }
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
        {
            StopRumble();
        }
    }

    // ==========================================
    // Private Methods
    // ==========================================

    private void HandleEnemyHit(EnemyHitFeedbackData data, bool isKill)
    {
        if (data == null || !InputDeviceWatcher.IsGamepad || Time.timeScale <= 0f)
        {
            return;
        }

        Gamepad gamepad = Gamepad.current;
        float seconds = data.GetRumbleSeconds(isKill);

        if (gamepad == null || seconds <= 0f)
        {
            return;
        }

        float low = data.GetRumbleLow(isKill);
        float high = data.GetRumbleHigh(isKill);

        // 振動中にもう1発当たったときは、残り時間が長いほうを残す。
        // 1回の爆発で倒した1発と倒れない1発が同時に来ても、倒した1発の強い振動を弱めない。
        if (_rumblingGamepad == gamepad)
        {
            float remainingSeconds = _stopAtUnscaledTime - Time.unscaledTime;

            if (remainingSeconds > seconds)
            {
                return;
            }
        }
        else
        {
            StopRumble();
        }

        gamepad.SetMotorSpeeds(low, high);
        _rumblingGamepad = gamepad;
        _stopAtUnscaledTime = Time.unscaledTime + seconds;
    }

    private void StopRumble()
    {
        if (_rumblingGamepad == null)
        {
            return;
        }

        // 抜かれたコントローラーは added が false になる。止める相手がいないので何もしない。
        if (_rumblingGamepad.added)
        {
            _rumblingGamepad.SetMotorSpeeds(0f, 0f);
        }

        _rumblingGamepad = null;
    }
}

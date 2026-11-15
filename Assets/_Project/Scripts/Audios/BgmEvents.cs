using System;

/// <summary>
/// BGM の再生リクエストを BgmManager へ取り次ぐ static イベント。
/// 流したい側は BgmManager を知らずに済む。
///
/// 【呼び出し方】
///   流す (クロスフェード) : BgmEvents.Play(BgmType.Normal);
///   秒数を指定して流す     : BgmEvents.Play(BgmType.Escape, 2f);
///   すぐ切り替える         : BgmEvents.Play(BgmType.Escape, 0f);
///   フェードアウトして止める : BgmEvents.Stop();
///   一時的に小さくする/戻す : BgmEvents.SetDucked(true); / BgmEvents.SetDucked(false);
///
/// 同じ曲をもう一度頼んでも頭からには戻らず、そのまま流れ続ける。
/// </summary>
public static class BgmEvents
{
    // ==========================================
    // Constants
    // ==========================================

    /// <summary>秒数を省いたときのフェード時間 (秒)。</summary>
    public const float DEFAULT_FADE_SECONDS = 1f;

    // ==========================================
    // Events
    // ==========================================

    /// <summary>再生の要求。引数は種類・フェード秒数。</summary>
    public static event Action<BgmType, float> OnPlayRequested;

    /// <summary>停止の要求。引数はフェード秒数。</summary>
    public static event Action<float> OnStopRequested;

    /// <summary>音量を下げる/戻す要求。true で下げる。</summary>
    public static event Action<bool> OnDuckRequested;

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>曲を流す。今の曲からクロスフェードで切り替わる。</summary>
    public static void Play(BgmType type, float fadeSeconds = DEFAULT_FADE_SECONDS)
        => OnPlayRequested?.Invoke(type, fadeSeconds);

    /// <summary>今の曲をフェードアウトして止める。</summary>
    public static void Stop(float fadeSeconds = DEFAULT_FADE_SECONDS)
        => OnStopRequested?.Invoke(fadeSeconds);

    /// <summary>ポーズ中などに BGM を一時的に小さくする。false で元に戻す。</summary>
    public static void SetDucked(bool isDucked)
        => OnDuckRequested?.Invoke(isDucked);
}

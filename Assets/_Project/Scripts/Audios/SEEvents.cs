using System;
using UnityEngine;

/// <summary>
/// 効果音の再生リクエストを SEManager へ取り次ぐ static イベント。
/// 鳴らしたい側は SEManager を知らずに済む。
///
/// 【呼び出し方】
///   2D OneShot           : SEEvents.PlayOneShot(SEType.Jump);
///   3D OneShot (位置)     : SEEvents.PlayOneShot(SEType.Explosion, center);
///   3D OneShot (Transform): SEEvents.PlayOneShot(SEType.Explosion, transform);
///   ループ開始            : SEEvents.StartLoop(SEType.FootStep, transform);
///   ループ停止            : SEEvents.StopLoop(SEType.FootStep, transform);
///
/// 【3D と 2D】
///   位置か Transform を渡すと 3D (遠いほど小さい)、渡さなければ 2D (どこでも同じ音量)。
///   OneShot は鳴らした瞬間の位置で鳴り、その後は発生源を追いかけない。
///   ループは発生源の子として鳴るので追いかける。
///
/// 【ループの単位】
///   「SEType ＋ 発生源」で1つ。2人のプレイヤーが同じ足音を同時に鳴らせる。
///   止めるときは、開始したときと同じ Transform を渡す。
/// </summary>
public static class SEEvents
{
    // ==========================================
    // Events
    // ==========================================

    /// <summary>OneShot の再生要求。引数は種類・3Dで鳴らすか・位置・音量倍率。</summary>
    public static event Action<SEType, bool, Vector3, float> OnOneShotRequested;

    /// <summary>ループ開始の要求。引数は種類・発生源 (null なら 2D)・音量倍率。</summary>
    public static event Action<SEType, Transform, float> OnLoopStartRequested;

    /// <summary>ループ停止の要求。引数は種類・発生源 (開始時と同じもの)。</summary>
    public static event Action<SEType, Transform> OnLoopStopRequested;

    // ==========================================
    // Public Methods
    // ==========================================

    /// <summary>2D で1回鳴らす。UI の音など、場所に関係なく同じ音量で聞かせたいとき。</summary>
    public static void PlayOneShot(SEType type, float volume = 1f)
        => OnOneShotRequested?.Invoke(type, false, Vector3.zero, volume);

    /// <summary>指定した位置で 3D で1回鳴らす。</summary>
    public static void PlayOneShot(SEType type, Vector3 position, float volume = 1f)
        => OnOneShotRequested?.Invoke(type, true, position, volume);

    /// <summary>
    /// source の今の位置で 3D で1回鳴らす。null なら 2D。
    /// 鳴らした直後に source が消えても音は最後まで鳴る。
    /// </summary>
    public static void PlayOneShot(SEType type, Transform source, float volume = 1f)
    {
        if (source == null)
        {
            PlayOneShot(type, volume);
            return;
        }

        PlayOneShot(type, source.position, volume);
    }

    /// <summary>ループを始める。source を渡すと 3D でその位置について回る。</summary>
    public static void StartLoop(SEType type, Transform source = null, float volume = 1f)
        => OnLoopStartRequested?.Invoke(type, source, volume);

    /// <summary>ループを止める。開始したときと同じ source を渡す。</summary>
    public static void StopLoop(SEType type, Transform source = null)
        => OnLoopStopRequested?.Invoke(type, source);
}

/// <summary>
/// BGM の種類。SO_BgmData にこの値ごとの曲を登録し、BgmEvents で流すときの名前にする。
/// </summary>
/// <remarks>
/// Inspector には数値で保存されるため、途中に差し込んだり並べ替えたりすると登録済みの曲がずれる。
/// 新しい曲は必ず一番下に、次の番号で足す。
/// </remarks>
public enum BgmType
{
    None = 0,

    /// <summary>タイトル画面。</summary>
    Title = 1,

    /// <summary>ゲーム中の通常状態。</summary>
    Normal = 2,

    /// <summary>メインターゲットに爆弾を付けてから脱出するまで。</summary>
    Escape = 3,
}

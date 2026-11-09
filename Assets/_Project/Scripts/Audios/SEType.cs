/// <summary>
/// 効果音の種類。SO_SEData にこの値ごとの音を登録し、SEEvents で鳴らすときの名前にする。
/// </summary>
/// <remarks>
/// Inspector には数値で保存されるため、途中に差し込んだり並べ替えたりすると登録済みの音がずれる。
/// 新しい音は必ず一番下に、次の番号で足す。
/// </remarks>
public enum SEType
{
    None = 0,
    Explosion = 1,
}

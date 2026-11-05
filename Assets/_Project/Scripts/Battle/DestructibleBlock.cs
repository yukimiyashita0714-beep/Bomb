using System.Collections;
using Bomb.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bomb.Battle
{
    /// <summary>
    /// 爆風が当たると当たり判定がなくなり、徐々に消えていくブロック。
    /// グレネードは相手が何者かを知らずに <see cref="IDamageable"/> だけを見て当てるので、
    /// ここもそれを実装して「ダメージを受けた = 爆風が当たった」として扱う。
    /// </summary>
    [DisallowMultipleComponent]
    public class DestructibleBlock : MonoBehaviour, IDamageable
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [SerializeField, Tooltip("消え方の設定。全ブロックで同じアセットを共有する")]
        private DestructibleBlockData _data;

        // ==========================================
        // Private Fields
        // ==========================================

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// 色の上書きに使い回す入れ物。SetPropertyBlock は中身をコピーするので全ブロックで共有してよい。
        /// マテリアルを直接書き換えると、ブロックごとにマテリアルの複製ができてしまう。
        /// </summary>
        private static MaterialPropertyBlock _propertyBlock;

        private Rigidbody _rigidbody;
        private Collider[] _colliders;
        private Renderer[] _renderers;
        private Color[] _baseColors;
        private Vector3 _initialScale;
        private bool _isBreaking;

        // ==========================================
        // Properties
        // ==========================================

        /// <summary>爆風が当たって消えている途中なら true。</summary>
        public bool IsBreaking => _isBreaking;

        // ==========================================
        // Unity Messages
        // ==========================================

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);

            if (_data == null)
            {
                Debug.LogError($"[{nameof(DestructibleBlock)}] DestructibleBlockData が未設定のため動作できない。", this);
                enabled = false;
            }
        }

        // ==========================================
        // Public Methods
        // ==========================================

        /// <summary>爆風が当たったときに呼ばれる。量に関係なく、一度当たれば消える。</summary>
        public void TakeDamage(float amount)
        {
            if (!enabled || _isBreaking)
            {
                return;
            }

            Break();
        }

        // ==========================================
        // Private Methods
        // ==========================================

        private void Break()
        {
            _isBreaking = true;

            // 消えかけのブロックに当たって止まったり、次の爆風をさえぎったりしないよう、すぐ判定を消す。
            for (int i = 0; i < _colliders.Length; i++)
            {
                _colliders[i].enabled = false;
            }

            if (_rigidbody != null && !_data.IsMovingWhileFading)
            {
                _rigidbody.isKinematic = true;
            }

            // 半透明にしても影は濃いまま残るので、消え始めた時点で影を切る。
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            }

            StartCoroutine(FadeOutRoutine());
        }

        private IEnumerator FadeOutRoutine()
        {
            if (_data.FadeDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(_data.FadeDelaySeconds);
            }

            bool isTransparentFade = _data.FadeMaterial != null;

            if (isTransparentFade)
            {
                SwapToFadeMaterial();
            }
            else
            {
                _initialScale = transform.localScale;
            }

            float durationSeconds = _data.FadeDurationSeconds;
            float elapsedSeconds = 0f;

            while (elapsedSeconds < durationSeconds)
            {
                elapsedSeconds += Time.deltaTime;
                float remainRatio = 1f - Mathf.Clamp01(elapsedSeconds / durationSeconds);

                if (isTransparentFade)
                {
                    ApplyAlpha(remainRatio);
                }
                else
                {
                    transform.localScale = _initialScale * remainRatio;
                }

                yield return null;
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// 不透明のマテリアルは透明にできないので、消える直前に半透明のものへ差し替える。
        /// 色は元のマテリアルから引き継ぐので、色違いのブロックも1つの半透明マテリアルで済む。
        /// </summary>
        private void SwapToFadeMaterial()
        {
            _baseColors = new Color[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++)
            {
                Material original = _renderers[i].sharedMaterial;

                _baseColors[i] = original != null && original.HasProperty(BaseColorId)
                    ? original.GetColor(BaseColorId)
                    : Color.white;

                _renderers[i].sharedMaterial = _data.FadeMaterial;
            }
        }

        private void ApplyAlpha(float alpha)
        {
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                Color color = _baseColors[i];
                color.a *= alpha;

                _renderers[i].GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorId, color);
                _renderers[i].SetPropertyBlock(_propertyBlock);
            }
        }
    }
}

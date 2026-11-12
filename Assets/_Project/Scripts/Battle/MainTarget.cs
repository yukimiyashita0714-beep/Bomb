using Bomb.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bomb.Battle
{
    public class MainTarget : MonoBehaviour, IInteractable
    {
        // ==========================================
        // Serialize Fields
        // ==========================================

        [SerializeField] private Animator _animator;

        [SerializeField] private BombEvent _bombEvent;
        [SerializeField] private ExplosionArea _explosionArea;

        [SerializeField, Tooltip("目的の爆弾")]
        private bool _mainBomb;

        [SerializeField, Tooltip("爆発させる範囲")]
        [Range(0, 300f)]
        private float _explosionRadius = 40f;

        [SerializeField, Tooltip("爆発の力。中心で物が上へ飛び出す速さ (m/s)")]
        [Range(0, 300f)]
        private float _explosionPower = 40f;

        [SerializeField, Tooltip("爆発の力の減衰。0 で範囲の端まで同じ力、1 で端では力が 0 になる")]
        [Range(0, 1f)]
        private float _explosionAttenuationPower = 0.5f;

        [SerializeField, Tooltip("外側へ飛ばす速さ (m/s)。0 なら真上にだけ飛ぶ")]
        [Range(0, 100f)]
        private float _explosionOutwardPower = 8f;

        [SerializeField, Tooltip("飛ばすときに付ける回転の速さの上限 (ラジアン/秒)")]
        [Range(0, 50f)]
        private float _explosionMaxSpin = 10f;

        [SerializeField, Tooltip("爆発が中心から範囲の端まで広がるのにかかる時間 (秒)。近い物から順に飛んでいく")]
        [Range(0.05f, 5f)]
        private float _explosionWaveSeconds = 1f;

        [SerializeField, Tooltip("爆発に巻き込ませるLayer")]
        private LayerMask _explosionLayerMask;

        // ==========================================
        // Private Fields
        // ==========================================

        /// <summary>爆発で飛ばす相手 1 つ分。</summary>
        private struct LaunchTarget
        {
            public Rigidbody Body;
            public DestructibleBlock Block;
            public float Distance;
        }

        /// <summary>飛ばす相手を、中心に近い順に並べたもの。</summary>
        private readonly List<LaunchTarget> _launchTargets = new List<LaunchTarget>();

        /// <summary>コライダーを複数持つ相手を二重に数えないための記録。</summary>
        private readonly HashSet<Rigidbody> _collectedBodies = new HashSet<Rigidbody>();

        private bool _isBombPlanted = false;

        // ==========================================
        // Properties
        // ==========================================

        public bool IsInteractable => !_isBombPlanted;

        public InteractKind InteractKind => InteractKind.BombPlant;

        // ==========================================
        // Unity Events
        // ==========================================

        private void OnEnable()
        {
            if (_bombEvent != null)
            {
                Debug.LogWarning("BombEventをアタッチして～");
                return;
            }

            _bombEvent.OnExplosion += Explosion;
        }

        private void OnDisable()
        {
            if (_bombEvent != null)
            {
                Debug.LogWarning("BombEventをアタッチして～");
                return;
            }

            _bombEvent.OnExplosion -= Explosion;
        }


        // ==========================================
        // Private Methods
        // ==========================================

        /// <summary>
        /// 範囲内の物を、中心に近い順に上へ飛ばしていく。
        /// グレネードの爆風 (縮んで復活する) とは別の処理で、飛んだブロックは元に戻らない。
        /// </summary>
        private void AddExplosionForce()
        {
            CollectLaunchTargets();
            StartCoroutine(LaunchWaveRoutine());
        }

        /// <summary>
        /// 範囲内の Rigidbody を集めて、中心に近い順に並べる。
        /// 範囲を調べるのは起爆の瞬間の 1 回だけ。
        /// </summary>
        private void CollectLaunchTargets()
        {
            _launchTargets.Clear();
            _collectedBodies.Clear();

            Vector3 center = transform.position;

            Collider[] colliders = Physics.OverlapSphere(
                center,
                _explosionRadius,
                _explosionLayerMask,
                QueryTriggerInteraction.Ignore);

            foreach (var collider in colliders)
            {
                Rigidbody rb = collider.attachedRigidbody;

                // Rigidbody が無い物は飛ばせないので次へ進む。同じ物を二度入れない。
                if (rb == null || !_collectedBodies.Add(rb))
                {
                    continue;
                }

                rb.TryGetComponent(out DestructibleBlock block);

                // ほかのメインターゲットの爆発で、もう飛んでいるブロックは飛ばさない。
                if (block != null && block.IsLaunched)
                {
                    continue;
                }

                _launchTargets.Add(new LaunchTarget
                {
                    Body = rb,
                    Block = block,
                    Distance = Vector3.Distance(rb.position, center),
                });
            }

            _launchTargets.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            _collectedBodies.Clear();
        }

        /// <summary>
        /// 爆発を中心から外へ広げ、届いた物から順に飛ばす。
        /// 1 フレームで飛ばすのはそのフレームで届いた分だけなので、負荷も数フレームに散らばる。
        /// </summary>
        private IEnumerator LaunchWaveRoutine()
        {
            Vector3 center = transform.position;
            int nextIndex = 0;
            float elapsedSeconds = 0f;

            while (nextIndex < _launchTargets.Count)
            {
                elapsedSeconds += Time.deltaTime;
                float waveRadius = _explosionRadius * Mathf.Clamp01(elapsedSeconds / _explosionWaveSeconds);

                while (nextIndex < _launchTargets.Count && _launchTargets[nextIndex].Distance <= waveRadius)
                {
                    LaunchTarget target = _launchTargets[nextIndex];
                    nextIndex++;

                    // 待っている間に消された物は飛ばさない。
                    if (target.Body == null)
                    {
                        continue;
                    }

                    Vector3 velocity = CalculateLaunchVelocity(center, target.Body.position, target.Distance);
                    Vector3 spin = Random.insideUnitSphere * _explosionMaxSpin;

                    if (target.Block != null)
                    {
                        target.Block.Launch(velocity, spin);
                        continue;
                    }

                    target.Body.isKinematic = false;
                    target.Body.AddForce(velocity, ForceMode.VelocityChange);
                    target.Body.AddTorque(spin, ForceMode.VelocityChange);
                }

                yield return null;
            }

            _launchTargets.Clear();
        }

        /// <summary>
        /// 中心からの距離に応じて、飛ばす速度を決める。
        /// 主に上へ飛ばし、少しだけ中心から外向きの速さを足す。中心に近いほど強い。
        /// </summary>
        private Vector3 CalculateLaunchVelocity(Vector3 center, Vector3 position, float distance)
        {
            Vector3 outward = position - center;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.zero;

            float distanceRatio = _explosionRadius > 0f ? Mathf.Clamp01(distance / _explosionRadius) : 0f;
            float powerRatio = 1f - _explosionAttenuationPower * distanceRatio;

            return (Vector3.up * _explosionPower + outward * _explosionOutwardPower) * powerRatio;
        }

        // ==========================================
        // Public Methods
        // ==========================================

        public void Interact()
        {
            Plant();
        }

        /// <summary>
        /// 爆弾を置く処理
        /// </summary>
        public void Plant()
        {
            if (_isBombPlanted)
            {
                return;
            }

            _isBombPlanted = true;

            _animator.SetTrigger("Plant");

            _explosionArea.AddBombPlantCount();

            SEEvents.PlayOneShot(SEType.MainTargetBombPlant, transform.position);

            //もしメイン爆弾なら設置した後のフェーズに入る
            if (_mainBomb)
            {
                if (_bombEvent != null)
                {
                    Debug.LogWarning("BombEventをアタッチして～");
                    return;
                }

                _bombEvent.Play(BombEvent.EventKind.Plant);

                SEEvents.PlayOneShot(SEType.Alarm);
            }
        }

        /// <summary>
        /// 爆弾を起爆させた時の処理
        /// </summary>
        public void Explosion()
        {
            if (!_isBombPlanted)
            {
                return;
            }

            _animator.SetTrigger("Explosion");
        }

        public void AnimatorExplosion()
        {
            SEEvents.PlayOneShot(SEType.MainTargetBombExplosion);

            AddExplosionForce();
        }
    }
}

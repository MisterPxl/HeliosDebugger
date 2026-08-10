#if DOTWEEN
using DG.Tweening;
using UnityEngine;

namespace HeliosDebugger.DOTween.Samples
{
    public sealed class HeliosDOTweenSample : MonoBehaviour
    {
        [SerializeField] private float _distance = 3f;
        [SerializeField] private float _duration = 2f;

        private Tween _moveTween;
        private Tween _pausedRotationTween;
        private Tween _valueTween;
        private float _sampleValue;

        private void Start()
        {
            _moveTween = transform
                .DOMoveX(transform.position.x + _distance, _duration)
                .SetId("sample.move")
                .SetTarget(gameObject)
                .SetLoops(-1, LoopType.Yoyo);

            _pausedRotationTween = transform
                .DORotate(new Vector3(0f, 180f, 0f), _duration)
                .SetId("sample.rotation.paused")
                .SetTarget(transform)
                .SetLoops(-1, LoopType.Yoyo)
                .Pause();

            _valueTween = DG.Tweening.DOTween
                .To(() => _sampleValue, value => _sampleValue = value, 100f, _duration * 2f)
                .SetId("sample.value")
                .SetTarget(this)
                .SetLoops(-1, LoopType.Restart);
        }

        private void OnDestroy()
        {
            _moveTween?.Kill();
            _pausedRotationTween?.Kill();
            _valueTween?.Kill();
        }
    }
}
#endif

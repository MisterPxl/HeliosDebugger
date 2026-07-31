#if DOTWEEN
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace HeliosDebugger.Samples
{
    public sealed class HeliosDOTweenStressDemo : MonoBehaviour
    {
        [SerializeField] private int _standaloneObjectCount = 10;
        [SerializeField] private int _sequenceObjectCount = 6;
        [SerializeField] private float _spacing = 1.5f;

        private readonly List<Tween> _tweens = new List<Tween>();
        private readonly List<GameObject> _objects = new List<GameObject>();

        private void Start()
        {
            CreateDemo();
        }

        [ContextMenu("Restart DOTween Stress Demo")]
        private void RestartDemo()
        {
            if (!Application.isPlaying)
                return;

            ClearDemo();
            CreateDemo();
        }

        [ContextMenu("Stop DOTween Stress Demo")]
        private void StopDemo()
        {
            ClearDemo();
        }

        private void OnDestroy()
        {
            ClearDemo();
        }

        private void CreateDemo()
        {
            CreateStandaloneTweens();
            CreateSequences();
            Debug.Log(
                $"Helios DOTween stress demo started: {_tweens.Count} active tweens/sequences. " +
                "Open Helios > Tweens.");
        }

        private void CreateStandaloneTweens()
        {
            int count = Mathf.Max(1, _standaloneObjectCount);
            float startX = -((count - 1) * _spacing) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                GameObject target = CreateCube(
                    $"StandaloneTween_{i:00}",
                    new Vector3(startX + i * _spacing, 0f, 0f));

                Tween movement = target.transform
                    .DOMoveY(2f + i * 0.15f, 1f + i * 0.08f)
                    .SetId($"stress.move.{i:00}")
                    .SetTarget(target)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);

                Tween rotation = target.transform
                    .DORotate(
                        new Vector3(0f, 360f, 0f),
                        1.5f + i * 0.05f,
                        RotateMode.FastBeyond360)
                    .SetId($"stress.rotate.{i:00}")
                    .SetTarget(target)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Restart);

                if (i % 4 == 0)
                    movement.Pause();
                if (i % 5 == 0)
                    rotation.Pause();

                _tweens.Add(movement);
                _tweens.Add(rotation);
            }
        }

        private void CreateSequences()
        {
            int count = Mathf.Max(1, _sequenceObjectCount);
            float startX = -((count - 1) * _spacing) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                GameObject target = CreateCube(
                    $"Sequence_{i:00}",
                    new Vector3(startX + i * _spacing, 0f, 4f));

                Sequence sequence = DG.Tweening.DOTween.Sequence()
                    .SetId($"stress.sequence.{i:00}")
                    .SetTarget(target);

                sequence
                    .Append(target.transform.DOMoveY(2.5f, 0.8f).SetEase(Ease.OutQuad))
                    .Join(target.transform.DOScale(1.6f, 0.8f).SetEase(Ease.OutBack))
                    .Append(target.transform.DOMoveX(target.transform.position.x + 1f, 0.5f))
                    .Join(target.transform.DORotate(
                        new Vector3(180f, 180f, 0f),
                        0.5f,
                        RotateMode.FastBeyond360))
                    .AppendInterval(0.25f)
                    .Append(target.transform.DOMoveX(target.transform.position.x, 0.5f))
                    .Join(target.transform.DOScale(1f, 0.5f))
                    .Append(target.transform.DOMoveY(0f, 0.8f).SetEase(Ease.InQuad))
                    .SetLoops(-1, LoopType.Restart);

                if (i % 3 == 0)
                    sequence.Pause();

                _tweens.Add(sequence);
            }
        }

        private GameObject CreateCube(string objectName, Vector3 localPosition)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = objectName;
            cube.transform.SetParent(transform, false);
            cube.transform.localPosition = localPosition;
            _objects.Add(cube);
            return cube;
        }

        private void ClearDemo()
        {
            for (int i = 0; i < _tweens.Count; i++)
            {
                Tween tween = _tweens[i];
                if (tween != null && tween.IsActive())
                    tween.Kill();
            }

            _tweens.Clear();
            for (int i = 0; i < _objects.Count; i++)
            {
                GameObject target = _objects[i];
                if (target != null)
                    Destroy(target);
            }

            _objects.Clear();
        }
    }
}
#endif

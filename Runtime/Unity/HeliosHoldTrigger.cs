using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HeliosDebugger
{
    public sealed class HeliosHoldTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private bool _pressed;
        private bool _invoked;
        private float _pressedAt;

        public float Duration { get; set; } = 0.75f;
        public Action Invoked { get; set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            _invoked = false;
            _pressedAt = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pressed = false;
        }

        private void Update()
        {
            if (!_pressed || _invoked || Time.unscaledTime - _pressedAt < Duration)
                return;

            _invoked = true;
            _pressed = false;
            Invoked?.Invoke();
        }
    }
}

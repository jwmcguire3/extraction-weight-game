#nullable enable
using System;
using UnityEngine;

namespace ExtractionWeight.Core
{
    public sealed class PlayerContextActionTarget : MonoBehaviour
    {
        [SerializeField]
        private ContextActionKind _actionKind = ContextActionKind.Extract;

        [SerializeField]
        private string _labelOverride = string.Empty;

        [Min(0f)]
        [SerializeField]
        private float _interactionRadius = 2.5f;

        [SerializeField]
        private bool _isEnabled = true;

        [Min(0)]
        [SerializeField]
        private int _priority;

        public event Action<PlayerController>? Activated;

        public ContextActionKind ActionKind => _actionKind;

        public float InteractionRadius => _interactionRadius;

        public int Priority => _priority;

        public bool IsEnabled => _isEnabled && isActiveAndEnabled;

        public string Label => string.IsNullOrWhiteSpace(_labelOverride) ? _actionKind.ToString() : _labelOverride;

        public void Execute(PlayerController playerController)
        {
            Activated?.Invoke(playerController);
        }

        public void Configure(ContextActionKind actionKind, string label, float interactionRadius, int priority)
        {
            _actionKind = actionKind;
            _labelOverride = label;
            _interactionRadius = interactionRadius;
            _priority = priority;
            _isEnabled = true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(ContextActionKind actionKind, string label, float interactionRadius, int priority)
        {
            Configure(actionKind, label, interactionRadius, priority);
        }
#endif
    }
}

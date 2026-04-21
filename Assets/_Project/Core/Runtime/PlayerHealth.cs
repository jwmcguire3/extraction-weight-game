#nullable enable
using System;
using ExtractionWeight.Weight;
using UnityEngine;

namespace ExtractionWeight.Core
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        private const float DefaultMaxHealth = 100f;
        private const float RegenDelaySeconds = 5f;
        private const float RegenPerSecond = 10f;
        private const float RegenCapFraction = 0.8f;

        [Min(1f)]
        [SerializeField]
        private float _maxHealth = DefaultMaxHealth;

        private float _secondsSinceLastDamage = RegenDelaySeconds;
        private bool _hasDied;

        public float MaxHealth => _maxHealth;

        public float CurrentHealth { get; private set; } = DefaultMaxHealth;

        public bool IsDead => _hasDied;

        public event Action<PlayerHealth>? OnPlayerDeath;

        private void Awake()
        {
            CurrentHealth = Mathf.Clamp(CurrentHealth <= 0f ? _maxHealth : CurrentHealth, 0f, _maxHealth);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void TakeDamage(float damage, IThreat source)
        {
            if (_hasDied || damage <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            _secondsSinceLastDamage = 0f;

            if (CurrentHealth > 0f)
            {
                return;
            }

            _hasDied = true;
            OnPlayerDeath?.Invoke(this);
        }

        public void Tick(float deltaTime)
        {
            if (_hasDied || deltaTime <= 0f)
            {
                return;
            }

            _secondsSinceLastDamage += deltaTime;
            if (_secondsSinceLastDamage < RegenDelaySeconds)
            {
                return;
            }

            var regenCap = _maxHealth * RegenCapFraction;
            if (CurrentHealth >= regenCap)
            {
                return;
            }

            CurrentHealth = Mathf.Min(regenCap, CurrentHealth + (RegenPerSecond * deltaTime));
        }

#if UNITY_EDITOR
        public void EditorSetCurrentHealth(float currentHealth)
        {
            CurrentHealth = Mathf.Clamp(currentHealth, 0f, _maxHealth);
            _hasDied = CurrentHealth <= 0f;
        }
#endif
    }
}

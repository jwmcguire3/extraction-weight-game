#nullable enable
using System;
using System.Collections.Generic;
using ExtractionWeight.Core;
using UnityEngine;

namespace ExtractionWeight.Weight
{
    public sealed class CarryState
    {
        public const float LoadedThreshold = 0.4f;
        public const float OverburdenedThreshold = 0.8f;
        public const float SoftCeilingThreshold = 1.0f;
        public const float MaxCapacityFraction = 1.2f;

        private readonly List<ILoadoutItem> _items;

        public CarryState(float carryCapacity)
        {
            if (carryCapacity <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(carryCapacity), "Carry capacity must be greater than zero.");
            }

            CarryCapacity = carryCapacity;
            _items = new List<ILoadoutItem>();
            TotalCost = new CostSignature();
        }

        public event Action? OnCarryChanged;

        public float CarryCapacity { get; }

        public IReadOnlyList<ILoadoutItem> Items => _items;

        public CostSignature TotalCost { get; private set; }

        public float CapacityFraction => TotalCost.Magnitude / CarryCapacity;

        public CarryBreakpoint CurrentBreakpoint
        {
            get
            {
                var fraction = CapacityFraction;
                if (fraction < LoadedThreshold)
                {
                    return CarryBreakpoint.Light;
                }

                if (fraction < OverburdenedThreshold)
                {
                    return CarryBreakpoint.Loaded;
                }

                if (fraction <= SoftCeilingThreshold)
                {
                    return CarryBreakpoint.Overburdened;
                }

                return CarryBreakpoint.SoftCeiling;
            }
        }

        public bool TryAdd(ILoadoutItem item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            var projectedTotal = TotalCost + item.BaseCost;
            var projectedFraction = projectedTotal.Magnitude / CarryCapacity;
            if (projectedFraction > MaxCapacityFraction)
            {
                return false;
            }

            _items.Add(item);
            TotalCost = projectedTotal;
            OnCarryChanged?.Invoke();
            return true;
        }

        public bool Remove(ILoadoutItem item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (!_items.Remove(item))
            {
                return false;
            }

            RecalculateTotalCost();
            OnCarryChanged?.Invoke();
            return true;
        }

        public void Clear()
        {
            if (_items.Count == 0)
            {
                return;
            }

            _items.Clear();
            TotalCost = new CostSignature();
            OnCarryChanged?.Invoke();
        }

        private void RecalculateTotalCost()
        {
            var total = new CostSignature();
            for (var i = 0; i < _items.Count; i++)
            {
                total += _items[i].BaseCost;
            }

            TotalCost = total;
        }
    }
}

#nullable enable
using ExtractionWeight.Core;
using UnityEngine;

namespace ExtractionWeight.Loot
{
    [CreateAssetMenu(fileName = "LootDefinition", menuName = "Extraction Weight/Loot/Loot Definition")]
    public sealed class LootDefinition : ScriptableObject
    {
        [SerializeField]
        private string _itemId = string.Empty;

        [SerializeField]
        private string _displayName = string.Empty;

        [SerializeField]
        private Sprite? _icon;

        [SerializeField]
        private LootCategory _category = LootCategory.Currency;

        [SerializeField]
        private CostSignature _baseCost;

        [Min(0f)]
        [SerializeField]
        private float _value;

        [SerializeField]
        private bool _isVolatile;

        [SerializeField]
        private AmbientAxisEffect _ambientEffect;

        [Min(0f)]
        [SerializeField]
        private Vector3 _physicalSize = Vector3.one;

        [SerializeField]
        private AudioClip? _pickupSound;

        [SerializeField]
        private AudioClip? _ambientSound;

        public string ItemId => _itemId;

        public string DisplayName => _displayName;

        public Sprite? Icon => _icon;

        public LootCategory Category => _category;

        public CostSignature BaseCost => _baseCost;

        public float Value => _value;

        public bool IsVolatile => _isVolatile;

        public AmbientAxisEffect AmbientEffect => _ambientEffect;

        public Vector3 PhysicalSize => _physicalSize;

        public AudioClip? PickupSound => _pickupSound;

        public AudioClip? AmbientSound => _ambientSound;

        public float TotalBaseCost => _baseCost.Noise + _baseCost.Silhouette + _baseCost.Handling + _baseCost.Mobility;

#if UNITY_EDITOR
        public void EditorSetData(
            string itemId,
            string displayName,
            Sprite icon,
            LootCategory category,
            CostSignature baseCost,
            float value,
            bool isVolatile,
            Vector3 physicalSize,
            AudioClip? pickupSound,
            AudioClip? ambientSound,
            AmbientAxisEffect ambientEffect)
        {
            _itemId = itemId.Trim();
            _displayName = displayName.Trim();
            _icon = icon;
            _category = category;
            _baseCost = baseCost;
            _value = Mathf.Max(0f, value);
            _isVolatile = isVolatile;
            _physicalSize = new Vector3(
                Mathf.Max(0f, physicalSize.x),
                Mathf.Max(0f, physicalSize.y),
                Mathf.Max(0f, physicalSize.z));
            _pickupSound = pickupSound;
            _ambientSound = ambientSound;
            _ambientEffect = ambientEffect;
        }
#endif

        private void OnValidate()
        {
            _itemId = _itemId.Trim();
            _displayName = _displayName.Trim();
            _value = Mathf.Max(0f, _value);
            _physicalSize.x = Mathf.Max(0f, _physicalSize.x);
            _physicalSize.y = Mathf.Max(0f, _physicalSize.y);
            _physicalSize.z = Mathf.Max(0f, _physicalSize.z);
        }
    }
}

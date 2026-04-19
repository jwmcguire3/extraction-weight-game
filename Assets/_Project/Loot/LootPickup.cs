#nullable enable
using ExtractionWeight.Core;
using UnityEngine;

namespace ExtractionWeight.Loot
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class LootPickup : MonoBehaviour, IPickupInteractable
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField]
        private LootDefinition? _definition;

        [SerializeField]
        private MeshFilter? _meshFilter;

        [SerializeField]
        private MeshRenderer? _meshRenderer;

        [SerializeField]
        private Collider? _triggerCollider;

        [SerializeField]
        private Color _glowColor = new(0.43f, 0.85f, 0.64f, 1f);

        public LootDefinition Definition => _definition!;

        public bool IsAvailable => isActiveAndEnabled && gameObject.activeInHierarchy && _definition != null;

        public Vector3 WorldPosition => transform.position;

        private void Awake()
        {
            _triggerCollider ??= GetComponent<Collider>();
            _meshFilter ??= GetComponentInChildren<MeshFilter>();
            _meshRenderer ??= GetComponentInChildren<MeshRenderer>();
            EnsureVisuals();
            EnsureTrigger();
        }

        private void OnTriggerEnter(Collider other)
        {
            var sink = FindPickupInteractionSink(other);
            sink?.RegisterPickupCandidate(this);
        }

        private void OnTriggerExit(Collider other)
        {
            var sink = FindPickupInteractionSink(other);
            sink?.UnregisterPickupCandidate(this);
        }

        public float GetRequiredHoldDuration(IPlayerCarryInteractor player)
        {
            if (_definition == null)
            {
                return 0f;
            }

            var baseDuration = _definition.GetSizeClass() switch
            {
                LootItemSize.Small => 0.3f,
                LootItemSize.Medium => 0.8f,
                _ => 1.5f,
            };

            return baseDuration * Mathf.Max(1f, player.CurrentHandlingMultiplier);
        }

        public bool TryCompletePickup(IPlayerCarryInteractor player, out string failureMessage)
        {
            failureMessage = string.Empty;
            if (_definition == null)
            {
                failureMessage = "Missing loot.";
                return false;
            }

            var item = new LootItem(_definition);
            if (!player.TryAddCarryItem(item))
            {
                failureMessage = "Pack full.";
                return false;
            }

            if (item.IsVolatile)
            {
                player.AttachAmbientEffect(LootAmbientEffectFactory.CreateFor(item));
            }

            if (_definition.PickupSound != null)
            {
                AudioSource.PlayClipAtPoint(_definition.PickupSound, transform.position);
            }

            gameObject.SetActive(false);
            return true;
        }

        private void EnsureTrigger()
        {
            if (_triggerCollider == null)
            {
                return;
            }

            _triggerCollider.isTrigger = true;
        }

        private void EnsureVisuals()
        {
            if (_meshFilter == null || _meshRenderer == null)
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Visual";
                visual.transform.SetParent(transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                visual.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                var collider = visual.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                _meshFilter = visual.GetComponent<MeshFilter>();
                _meshRenderer = visual.GetComponent<MeshRenderer>();
            }

            if (_meshRenderer == null)
            {
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = _meshRenderer.sharedMaterial;
            if (material == null || material.shader != shader)
            {
                material = new Material(shader);
                _meshRenderer.sharedMaterial = material;
            }

            material.color = _glowColor * 0.6f;
            if (material.HasProperty(EmissionColorId))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionColorId, _glowColor * 1.8f);
            }
        }

        private static IPickupInteractionSink? FindPickupInteractionSink(Component source)
        {
            Transform? current = source.transform;
            while (current != null)
            {
                var behaviours = current.GetComponents<MonoBehaviour>();
                for (var i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IPickupInteractionSink sink)
                    {
                        return sink;
                    }
                }

                current = current.parent;
            }

            return null;
        }

#if UNITY_EDITOR
        public void EditorConfigure(LootDefinition definition)
        {
            _definition = definition;
        }
#endif

        public void Configure(LootDefinition definition)
        {
            _definition = definition;
        }
    }
}

#nullable enable
using UnityEngine;
using UnityEngine.EventSystems;

namespace ExtractionWeight.UI
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EventSystem))]
    public sealed class PersistentEventSystem : MonoBehaviour
    {
        private static PersistentEventSystem? s_instance;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }
    }
}

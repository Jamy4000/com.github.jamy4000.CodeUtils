using UnityEditor;
using UnityEngine;

namespace UnityTechnologies.CodeUtils
{
#if UNITY_EDITOR
    [EditorUtils.ResetOnPlayMode(resetMethod: "ResetStaticState")]
#endif
    public abstract class Singleton<T> : MonoBehaviour
        where T : MonoBehaviour
    {
        public delegate void OnInitialiseCallback(T singleton);

        private static T s_Instance;

        public static bool TryGetInstance(out T instance)
        {
            instance = s_Instance;
            return instance != null;
        }

        protected virtual bool Persistent => false;

        protected static void ResetStaticState()
        {
            s_Instance = null;
            s_InitialisationCallback = null;
        }

        public static T Instance => s_Instance;
        private static OnInitialiseCallback s_InitialisationCallback;

        public virtual void Awake()
        {
            if (s_Instance != null)
            {
                Debug.LogError($"[Singleton::Awake] {typeof(T).ToString()} instance already exists");
            }

            s_Instance = this as T;

            s_InitialisationCallback?.Invoke(s_Instance);
            s_InitialisationCallback = null;

            if (Persistent)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        public virtual void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
                s_InitialisationCallback = null;
            }
        }

        public static void OnInitialise(OnInitialiseCallback callback)
        {
            if (s_Instance != null)
            {
                callback(s_Instance);
            }
            else
            {
                s_InitialisationCallback += callback;
            }
        }
    }

    public abstract class AutoSingleton<T> : Singleton<T>
        where T : MonoBehaviour
    {
        protected override bool Persistent => true;

        public new static T Instance
        {
            get
            {
                if (!TryGetInstance(out var instance)

#if UNITY_EDITOR
                && EditorApplication.isPlayingOrWillChangePlaymode //don't allow instantiation in edit mode
#endif
                    )
                {
                    Debug.Log($"Creating new instance of type {typeof(T).Name}");
                    GameObject singleton = new GameObject();
                    var newInstance = singleton.AddComponent<T>();
                    singleton.name = typeof(T).ToString();
                    return newInstance;
                }
                else
                {
                    return instance;
                }
            }
        }
    }
}

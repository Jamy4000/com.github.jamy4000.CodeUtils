using UnityEngine;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityTechnologies.CodeUtils.SpatialPartionning.Samples
{
    /// <summary>
    /// Sample demonstrating how <see cref="Octree"/> is working.
    /// Spawns a set of objects, then periodically picks a random query point and highlights
    /// the closest elements within a given range.
    /// </summary>
    public class OctreeSample : MonoBehaviour
    {
        // ── Spawn ──────────────────────────────────────────────────────────────
        [Header("Spawn")]
        [SerializeField] private int _objectCount = 1000;
        [SerializeField] private float _spawnRange = 50f;

        // ── Query ──────────────────────────────────────────────────────────────
        [Header("Query")]
        [Tooltip("Radius used for the range query.")]
        [SerializeField] private float _queryRange = 15f;
        [Tooltip("Maximum number of results returned by the range query.")]
        [SerializeField] private int _maxResults = 5;
        [Tooltip("Seconds between each random query.")]
        [SerializeField] private float _queryInterval = 2f;

        // ── Private state ──────────────────────────────────────────────────────
        private GameObject[] _spawnedObjects;

        // One interface per mode; only one is non-null at a time.
        private ISpatialPartitioner<Vector3> _octree;

        private QueryResult[] _queryResults;
        private float _timer;
        private int _lastHighlightedBest = -1;

        // The visual sphere that marks the current query position.
        private Transform _queryMarker;

        // ── Unity ──────────────────────────────────────────────────────────────

        private void Start()
        {
            _queryResults = new QueryResult[_maxResults];
            BuildTree();
            SpawnObjects();
            CreateQueryMarker();
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f)
                return;

            _timer = _queryInterval;
            RunQuery();
        }

        private void OnDrawGizmosSelected()
        {
            _octree?.OnDrawGizmos();
        }

        private void OnDrawGizmos()
        {
            if (_spawnedObjects == null || _queryResults == null)
                return;

            Vector3 queryPos = _queryMarker.position;

            for (int i = 0; i < _queryResults.Length; i++)
            {
                int id = _queryResults[i].ElementID;
                if (id < 0 || id >= _spawnedObjects.Length)
                    break;

                Vector3 from = queryPos;
                Vector3 to   = _spawnedObjects[id].transform.position;

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(from, to);

#if UNITY_EDITOR
                Handles.Label((from + to) * 0.5f, $"{_queryResults[i].Distance:F2}m");
#endif
            }
        }

        // ── Public API (used by the custom editor button) ──────────────────────

        /// <summary>
        /// Rebuilds the tree and respawns objects. Useful to call after changing parameters in the inspector.
        /// Safe to call at runtime.
        /// </summary>
        public void RebuildTree()
        {
            if (_queryResults.Length != _maxResults)
                _queryResults = new QueryResult[_maxResults];
            
            DestroySpawnedObjects();
            BuildTree();
            SpawnObjects();
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void BuildTree()
        {
            Vector3 halfSize = new(_spawnRange, _spawnRange, _spawnRange);
            _octree = new Octree(Vector3.zero, halfSize);
        }

        private void SpawnObjects()
        {
            _spawnedObjects = new GameObject[_objectCount];

            for (int i = 0; i < _objectCount; i++)
            {
                Vector3 pos = RandomPosition();

                _spawnedObjects[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _spawnedObjects[i].transform.SetPositionAndRotation(pos, Quaternion.identity);
                _spawnedObjects[i].transform.localScale = Vector3.one * 0.5f;
                _spawnedObjects[i].GetComponent<Renderer>().sharedMaterial.color = Color.red;

                InsertIntoTree(pos, i);
            }
        }

        private void DestroySpawnedObjects()
        {
            if (_spawnedObjects == null)
                return;

            ResetHighlights();

            foreach (var spawnedObject in _spawnedObjects)
            {
                if (spawnedObject != null)
                    Destroy(spawnedObject);
            }

            _spawnedObjects = null;
        }

        private void InsertIntoTree(Vector3 position, int id)
        {
            _octree.Insert(position, id);
        }

        private void CreateQueryMarker()
        {
            _queryMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            _queryMarker.localScale = Vector3.one * 0.75f;
            _queryMarker.GetComponent<Renderer>().material.color = Color.blue;
            _queryMarker.name = "[OctreeSample] Query Marker";
        }

        private void RunQuery()
        {
            ResetHighlights();

            Vector3 queryPos = RandomPosition();
            _queryMarker.position = queryPos;

            int found = _octree.QueryWithinRange_NoAlloc(queryPos, _queryRange, _queryResults);

            if (found > 0)
            {
                Debug.Log($"[OctreeSample] Query at {queryPos} — found {found} object(s) within {_queryRange}m. " +
                          $"Closest: ID {_queryResults[0].ElementID} at {_queryResults[0].Distance:F2}m.");

                _spawnedObjects[_queryResults[0].ElementID].GetComponent<Renderer>().material.color = Color.green;
                _lastHighlightedBest = _queryResults[0].ElementID;

                for (int i = 1; i < found; i++)
                    _spawnedObjects[_queryResults[i].ElementID].GetComponent<Renderer>().material.color = Color.yellow;
            }
            else
            {
                Debug.Log($"[OctreeSample] No objects found within {_queryRange}m of {queryPos}.");
            }
        }

        private void ResetHighlights()
        {
            if (_lastHighlightedBest >= 0)
            {
                _spawnedObjects[_lastHighlightedBest].GetComponent<Renderer>().material.color = Color.red;
                _lastHighlightedBest = -1;
            }

            for (int i = 0; i < _queryResults.Length; i++)
            {
                int id = _queryResults[i].ElementID;
                if (id < 0 || id >= _spawnedObjects.Length)
                    break;

                _spawnedObjects[id].GetComponent<Renderer>().material.color = Color.red;
                _queryResults[i] = new QueryResult(-1, float.MaxValue);
            }
        }

        private Vector3 RandomPosition()
        {
            float half = _spawnRange * 0.5f;
            return new Vector3(
                Random.Range(-half, half),
                Random.Range(-half, half),
                Random.Range(-half, half)
            );
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Custom editor that adds a "Rebuild Tree" button and lets the user switch
    /// </summary>
    [CustomEditor(typeof(OctreeSample))]
    public class OctreeSampleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            OctreeSample sample = (OctreeSample)target;

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Rebuild Tree with Current Mode"))
                    sample.RebuildTree();
            }
        }
    }
#endif
}
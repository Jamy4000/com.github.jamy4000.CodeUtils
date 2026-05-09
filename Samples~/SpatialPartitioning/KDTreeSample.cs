using UnityEngine;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CodeUtils.SpatialPartitioning.Samples
{
    /// <summary>
    /// Sample demonstrating how <see cref="KDTree{TDimension}"/> works in 3-D.
    /// <para>
    /// Showcases the key features that distinguish the KD-tree from the Octree and Quadtree samples:
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>Bulk balanced build</b> — all objects are inserted at once via
    ///     <see cref="ISpatialPartitioner{TData}.InsertRange"/> for an O(n log n) median-split tree.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Nearest-neighbour query</b> — <see cref="ISpatialPartitioner{TData}.TryQueryClosest"/>
    ///     finds the single closest element to the query point.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Range query</b> — <see cref="ISpatialPartitioner{TData}.QueryWithinRange_NoAlloc"/>
    ///     finds all elements within a configurable radius, allocation-free.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Live removal + rebuild</b> — a configurable number of random objects are removed each
    ///     query cycle, and <see cref="ISpatialPartitioner{TData}.Rebuild"/> can be triggered from
    ///     the inspector to defragment the tree afterwards.
    ///   </description></item>
    /// </list>
    /// </para>
    /// </summary>
    public class KDTreeSample : MonoBehaviour
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

        // ── Removal ────────────────────────────────────────────────────────────
        [Header("Live Removal")]
        [Tooltip("How many random objects to remove each query cycle. Set to 0 to disable.")]
        [SerializeField] private int _removePerCycle = 3;

        // ── Private state ──────────────────────────────────────────────────────
        private GameObject[] _spawnedObjects;
        private Renderer[]   _renderers;       // Cached per-object — avoids GetComponent every frame.

        // Parallel arrays used for bulk InsertRange — avoids per-element tuple allocation.
        private Vector3[] _positions;
        private int[]     _ids;

        private ISpatialPartitioner<Vector3> _kdTree;

        private QueryResult[] _queryResults;
        private float _timer;
        private int _lastHighlightedBest  = -1;
        private int _lastHighlightedNearest = -1;

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
            _kdTree?.OnDrawGizmos();
        }

        private void OnDrawGizmos()
        {
            if (_spawnedObjects == null || _queryResults == null || _queryMarker == null)
                return;

            Vector3 queryPos = _queryMarker.position;

            // Draw lines and distance labels to every range-query result.
            // Use continue (not break) so a null/invalid entry doesn't stop later valid entries.
            for (int i = 0; i < _queryResults.Length; i++)
            {
                int id = _queryResults[i].ElementID;
                if (id < 0 || id >= _spawnedObjects.Length || _spawnedObjects[id] == null)
                    continue;

                Vector3 from = queryPos;
                Vector3 to   = _spawnedObjects[id].transform.position;

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(from, to);

#if UNITY_EDITOR
                Handles.Label((from + to) * 0.5f, $"{_queryResults[i].Distance:F2}m");
#endif
            }

            // Draw a wire sphere showing the query radius.
            Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(queryPos, _queryRange);
        }

        // ── Public API (used by the custom editor button) ──────────────────────

        /// <summary>
        /// Destroys all spawned objects, rebuilds the tree from scratch, and respawns.
        /// Uses <see cref="ISpatialPartitioner{TData}.InsertRange"/> for a balanced bulk build.
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

        /// <summary>
        /// Calls <see cref="ISpatialPartitioner{TData}.Rebuild"/> on the live tree to compact
        /// the internal node array and restore optimal balance after removals.
        /// Does not respawn objects.
        /// </summary>
        public void CompactTree()
        {
            _kdTree?.Rebuild();
            Debug.Log("[KDTreeSample] Tree compacted via Rebuild().");
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void BuildTree()
        {
            // Construct an empty tree. SpawnObjects will populate it via InsertRange.
            _kdTree = new KDTree<Vector3>();
        }

        private void SpawnObjects()
        {
            _spawnedObjects = new GameObject[_objectCount];
            _renderers      = new Renderer[_objectCount];
            _positions      = new Vector3[_objectCount];
            _ids            = new int[_objectCount];

            for (int i = 0; i < _objectCount; i++)
            {
                Vector3 pos = RandomPosition();
                _positions[i] = pos;
                _ids[i]       = i;

                _spawnedObjects[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _spawnedObjects[i].transform.SetPositionAndRotation(pos, Quaternion.identity);
                _spawnedObjects[i].transform.localScale = Vector3.one * 0.5f;
                _spawnedObjects[i].name = $"[KDTreeSample] Object {i}";

                // Cache the renderer and give each object its own material instance so
                // colour changes are isolated. All subsequent writes use sharedMaterial
                // on this instance — no further allocations per colour change.
                _renderers[i] = _spawnedObjects[i].GetComponent<Renderer>();
                _renderers[i].sharedMaterial = new Material(_renderers[i].sharedMaterial);
                _renderers[i].sharedMaterial.color = Color.red;
            }

            // Bulk balanced build — O(n log n) median-split instead of sequential inserts.
            // This guarantees a tree depth of ⌈log₂(n)⌉ for optimal query performance.
            _kdTree.InsertRange(_positions, _ids);
        }

        private void DestroySpawnedObjects()
        {
            if (_spawnedObjects == null)
                return;

            ResetHighlights();

            foreach (var obj in _spawnedObjects)
            {
                if (obj != null)
                    Destroy(obj);
            }

            _spawnedObjects = null;
            _renderers      = null;
            _positions      = null;
            _ids            = null;
        }

        private void CreateQueryMarker()
        {
            _queryMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            _queryMarker.localScale = Vector3.one * 0.75f;
            // The query marker only needs one colour change ever, so .material is fine here.
            _queryMarker.GetComponent<Renderer>().material.color = Color.blue;
            _queryMarker.name = "[KDTreeSample] Query Marker";
        }

        private void RunQuery()
        {
            ResetHighlights();

            // 1. Remove a batch of random live objects to demonstrate live removal.
            if (_removePerCycle > 0)
                RemoveRandomObjects(_removePerCycle);

            // 2. Pick a new random query point.
            Vector3 queryPos      = RandomPosition();
            _queryMarker.position = queryPos;

            // 3. Nearest-neighbour query — finds the single closest remaining object.
            if (_kdTree.TryQueryClosest(queryPos, out QueryResult nearest))
            {
                int nearID = nearest.ElementID;
                if (nearID >= 0 && nearID < _renderers.Length && _spawnedObjects[nearID] != null)
                {
                    _renderers[nearID].sharedMaterial.color = Color.green;
                    _lastHighlightedNearest = nearID;
                }

                Debug.Log($"[KDTreeSample] Nearest to {queryPos}: ID {nearest.ElementID} " +
                          $"at {nearest.Distance:F2}m.");
            }

            // 4. Range query — finds all objects within _queryRange, allocation-free.
            int found = _kdTree.QueryWithinRange_NoAlloc(queryPos, _queryRange, _queryResults);

            if (found > 0)
            {
                // The closest result in the range is _queryResults[0] (results are sorted).
                _lastHighlightedBest = _queryResults[0].ElementID;
                if (_lastHighlightedBest >= 0 && _lastHighlightedBest < _renderers.Length
                    && _spawnedObjects[_lastHighlightedBest] != null)
                {
                    _renderers[_lastHighlightedBest].sharedMaterial.color = Color.green;
                }

                for (int i = 1; i < found; i++)
                {
                    int id = _queryResults[i].ElementID;
                    if (id >= 0 && id < _renderers.Length && _spawnedObjects[id] != null)
                        _renderers[id].sharedMaterial.color = Color.yellow;
                }

                Debug.Log($"[KDTreeSample] Range query at {queryPos} — found {found} object(s) " +
                          $"within {_queryRange}m. Closest: ID {_queryResults[0].ElementID} " +
                          $"at {_queryResults[0].Distance:F2}m.");
            }
            else
            {
                Debug.Log($"[KDTreeSample] No objects found within {_queryRange}m of {queryPos}.");
            }
        }

        /// <summary>
        /// Removes <paramref name="count"/> random live objects from both the tree and the scene.
        /// </summary>
        private void RemoveRandomObjects(int count)
        {
            if (_spawnedObjects == null) return;

            int removed = 0;
            int attempts = 0;
            int maxAttempts = count * 10; // Avoid infinite loop when most slots are already empty.

            while (removed < count && attempts < maxAttempts)
            {
                attempts++;
                int id = Random.Range(0, _spawnedObjects.Length);
                if (_spawnedObjects[id] == null)
                    continue;

                // Remove from the tree before destroying the GameObject.
                _kdTree.Remove(_spawnedObjects[id].transform.position, id);

                Destroy(_spawnedObjects[id]);
                _spawnedObjects[id] = null;
                removed++;
            }

            if (removed > 0)
                Debug.Log($"[KDTreeSample] Removed {removed} object(s) from the tree.");
        }

        private void ResetHighlights()
        {
            // Reset the nearest-neighbour highlight.
            if (_lastHighlightedNearest >= 0 && _renderers != null
                && _lastHighlightedNearest < _renderers.Length
                && _spawnedObjects[_lastHighlightedNearest] != null)
            {
                _renderers[_lastHighlightedNearest].sharedMaterial.color = Color.red;
            }
            _lastHighlightedNearest = -1;

            // Reset the closest range-query highlight.
            if (_lastHighlightedBest >= 0 && _renderers != null
                && _lastHighlightedBest < _renderers.Length
                && _spawnedObjects[_lastHighlightedBest] != null)
            {
                _renderers[_lastHighlightedBest].sharedMaterial.color = Color.red;
            }
            _lastHighlightedBest = -1;

            // Reset every slot in the result buffer unconditionally.
            // Using continue (not break) ensures all slots are cleared even when an entry
            // is invalid or belongs to a destroyed object — the previous break caused stale
            // IDs to accumulate across cycles, making objects appear to vanish.
            for (int i = 0; i < _queryResults.Length; i++)
            {
                int id = _queryResults[i].ElementID;

                // Always clear the slot first so no stale ID lingers into the next cycle.
                _queryResults[i] = new QueryResult(-1, float.MaxValue);

                if (id < 0 || _renderers == null || id >= _renderers.Length)
                    continue;

                if (_spawnedObjects[id] != null)
                    _renderers[id].sharedMaterial.color = Color.red;
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
    /// Custom editor that adds <b>Rebuild Tree</b> and <b>Compact Tree (Rebuild)</b> buttons.
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>Rebuild Tree</b> — destroys all objects and starts fresh with a new balanced build.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Compact Tree</b> — calls <see cref="ISpatialPartitioner{TData}.Rebuild"/> on the live
    ///     tree to defragment freed node slots without respawning objects.
    ///   </description></item>
    /// </list>
    /// </summary>
    [CustomEditor(typeof(KDTreeSample))]
    public class KDTreeSampleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            KDTreeSample sample = (KDTreeSample)target;

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Rebuild Tree with Current Settings"))
                    sample.RebuildTree();

                EditorGUILayout.Space(2f);

                if (GUILayout.Button("Compact Tree (Rebuild)"))
                    sample.CompactTree();
            }
        }
    }
#endif
}


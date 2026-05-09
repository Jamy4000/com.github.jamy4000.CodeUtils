namespace CodeUtils.SpatialPartitioning
{
    public readonly struct OctreeElement
    {
        public readonly int ElementID;     // The ID representing an element in this node (if this is a leaf node)
        public readonly UnityEngine.Vector3 Position;

        public OctreeElement(int elementID, UnityEngine.Vector3 position)
        {
            ElementID = elementID;
            Position = position;
        }

        public bool HasElement => ElementID != int.MinValue;
    }
}
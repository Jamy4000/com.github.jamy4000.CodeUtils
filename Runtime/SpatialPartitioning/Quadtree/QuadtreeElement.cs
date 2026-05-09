namespace CodeUtils.SpatialPartitioning
{
    public readonly struct QuadtreeElement
    {
        public readonly int ElementID;     // The ID representing an element in this node (if this is a leaf node)
        public readonly UnityEngine.Vector2 Position;

        public QuadtreeElement(int elementID, UnityEngine.Vector2 position)
        {
            ElementID = elementID;
            Position = position;
        }

        public bool HasElement => ElementID != int.MinValue;
    }
}
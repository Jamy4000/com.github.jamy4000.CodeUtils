namespace CodeUtils.SpatialPartitioning
{
    public readonly struct AABB
    {
        public readonly float MinX, MinY, MaxX, MaxY;

        public AABB(float minX, float minY, float maxX, float maxY)
        {
            this.MinX = minX;
            this.MinY = minY;
            this.MaxX = maxX;
            this.MaxY = maxY;
        }

        public bool Contains(UnityEngine.Vector2 point)
        {
            return point.x >= MinX && point.x <= MaxX && point.y >= MinY && point.y <= MaxY;
        }

        /// <summary>
        /// Checks if the bounding box intersects with another bounding box
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Intersects(AABB other)
        {
            return !(other.MinX > MaxX || other.MaxX < MinX || other.MinY > MaxY || other.MaxY < MinY);
        }

        /// <summary>
        /// Returns the squared distance from a point to the nearest point on (or inside) this AABB.
        /// Returns 0 if the point is inside the box.
        /// </summary>
        public float SqrDistance(UnityEngine.Vector2 point)
        {
            float dx = point.x < MinX ? MinX - point.x : point.x > MaxX ? point.x - MaxX : 0f;
            float dy = point.y < MinY ? MinY - point.y : point.y > MaxY ? point.y - MaxY : 0f;
            return dx * dx + dy * dy;
        }
    }
}
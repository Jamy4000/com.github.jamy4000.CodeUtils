using UnityEngine;

namespace CodeUtils.SpatialPartitioning
{
    /// <summary>
    /// Defines the per-axis operations required by <see cref="KDTree{TDimension}"/> to work
    /// generically across 1-D (<see cref="float"/>), 2-D (<see cref="Vector2"/>) and
    /// 3-D (<see cref="Vector3"/>) position types.
    /// </summary>
    /// <typeparam name="TDimension">The position type this comparer operates on.</typeparam>
    public interface IDimensionComparer<in TDimension>
    {
        /// <summary>
        /// Compares two positions along a single axis.
        /// </summary>
        /// <param name="x">First position.</param>
        /// <param name="y">Second position.</param>
        /// <param name="axis">Zero-based axis index (0 = X, 1 = Y, 2 = Z).</param>
        /// <returns>Negative if <paramref name="x"/> &lt; <paramref name="y"/>, zero if equal, positive otherwise.</returns>
        public int Compare(TDimension x, TDimension y, int axis);

        /// <summary>
        /// Extracts the scalar value of <paramref name="value"/> along the given axis.
        /// </summary>
        /// <param name="value">The position to read.</param>
        /// <param name="axis">Zero-based axis index.</param>
        /// <returns>The component of <paramref name="value"/> on <paramref name="axis"/>.</returns>
        public float GetComponentOnAxis(TDimension value, int axis);

        /// <summary>
        /// Computes the squared Euclidean distance between two positions.
        /// Squared distance is used throughout to avoid costly <see cref="Mathf.Sqrt"/> calls during tree traversal.
        /// </summary>
        /// <param name="x">First position.</param>
        /// <param name="y">Second position.</param>
        /// <returns>Squared distance between <paramref name="x"/> and <paramref name="y"/>.</returns>
        public float CalculateDistanceSq(TDimension x, TDimension y);

        /// <summary>
        /// Converts a position of type <typeparamref name="TDimension"/> to a <see cref="Vector3"/>
        /// for use with the Unity Gizmos API.
        /// </summary>
        /// <param name="position">Position to convert.</param>
        /// <returns>A <see cref="Vector3"/> representing the position in 3-D world space.</returns>
        Vector3 ToVector3(TDimension position);

        /// <summary>
        /// Returns the number of spatial dimensions this comparer operates on.
        /// Used to cycle the splitting axis during KD-tree traversal (<c>axis = depth % GetDimension()</c>).
        /// </summary>
        /// <returns>1 for <see cref="float"/>, 2 for <see cref="Vector2"/>, 3 for <see cref="Vector3"/>.</returns>
        int GetDimension();
    }

    /// <summary>
    /// <see cref="IDimensionComparer{TDimension}"/> implementation for 1-D float positions.
    /// </summary>
    public readonly struct OneDimensionComparer : IDimensionComparer<float>
    {
        /// <inheritdoc/>
        public int Compare(float a, float b, int axis) => a.CompareTo(b);

        /// <inheritdoc/>
        public float GetComponentOnAxis(float value, int axis) => value;

        /// <inheritdoc/>
        public float CalculateDistanceSq(float x, float y)
        {
            var distance = Mathf.Abs(y - x);
            return distance * distance;
        }

        /// <inheritdoc/>
        /// <remarks>Maps the 1-D value to the Y axis: <c>(0, value, 0)</c>.</remarks>
        public Vector3 ToVector3(float position) => new Vector3(0f, position, 0f);

        /// <inheritdoc/>
        public int GetDimension() => 1;
    }

    /// <summary>
    /// <see cref="IDimensionComparer{TDimension}"/> implementation for 2-D <see cref="Vector2"/> positions.
    /// </summary>
    public readonly struct TwoDimensionComparer : IDimensionComparer<Vector2>
    {
        /// <inheritdoc/>
        public int Compare(Vector2 a, Vector2 b, int axis)
            => axis == 0 ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);

        /// <inheritdoc/>
        public float GetComponentOnAxis(Vector2 value, int axis)
            => axis == 0 ? value.x : value.y;

        /// <inheritdoc/>
        public float CalculateDistanceSq(Vector2 x, Vector2 y) => Vector2.SqrMagnitude(y - x);

        /// <inheritdoc/>
        /// <remarks>Maps the 2-D XY position to the XZ plane: <c>(x, 0, y)</c>.</remarks>
        public Vector3 ToVector3(Vector2 position) => new Vector3(position.x, 0f, position.y);

        /// <inheritdoc/>
        public int GetDimension() => 2;
    }

    /// <summary>
    /// <see cref="IDimensionComparer{TDimension}"/> implementation for 3-D <see cref="Vector3"/> positions.
    /// </summary>
    public readonly struct ThreeDimensionComparer : IDimensionComparer<Vector3>
    {
        /// <inheritdoc/>
        public int Compare(Vector3 a, Vector3 b, int axis)
        {
            if (axis == 0) return a.x.CompareTo(b.x);
            if (axis == 1) return a.y.CompareTo(b.y);
            return a.z.CompareTo(b.z);
        }

        /// <inheritdoc/>
        public float GetComponentOnAxis(Vector3 value, int axis)
            => axis == 0 ? value.x : axis == 1 ? value.y : value.z;

        /// <inheritdoc/>
        public float CalculateDistanceSq(Vector3 x, Vector3 y) => Vector3.SqrMagnitude(y - x);

        /// <inheritdoc/>
        public Vector3 ToVector3(Vector3 position) => position;

        /// <inheritdoc/>
        public int GetDimension() => 3;
    }
}
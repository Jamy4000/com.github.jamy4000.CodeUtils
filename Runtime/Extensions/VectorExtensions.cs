using System.Runtime.CompilerServices;
using UnityEngine;

namespace CodeUtils
{
    public static class VectorExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 DirectionXZ(Vector3 start, Vector3 end)
        {
            Vector2 startFlat = new Vector2(start.x, start.z);
            Vector2 endFlat = new Vector2(end.x, end.z);
            return (endFlat - startFlat).normalized;
        }

        public static Vector2 XZ(this Vector3 vec)
        {
            return new Vector2(vec.x, vec.z);
        }

        public static Vector3 X(this Vector3 vec)
        {
            return new Vector3(vec.x, 0, 0);
        }

        public static Vector3 Y(this Vector3 vec)
        {
            return new Vector3(0, vec.y, 0);
        }

        public static Vector3 Flatten(this Vector3 vec)
        {
            return new Vector3(vec.x, 0.0f, vec.z);
        }

        public static Vector3 X0Z(this Vector2 vec)
        {
            return new Vector3(vec.x, 0.0f, vec.y);
        }

        public static bool Approximately(Vector3 a, Vector3 b, float tolerance)
        {
            return (a - b).sqrMagnitude <= tolerance * tolerance;
        }

        public static bool Approximately(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude <= Vector3.kEpsilon * Vector3.kEpsilon;
        }

        public static bool ApproximatelyXZ(Vector3 a, Vector3 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.z, b.z);
        }

        public static Vector3 ClosestAxis(Vector3 direction, Transform transform)
        {
            return ClosestAxis(direction, transform.forward, transform.up, transform.right);
        }

        public static Vector3 ClosestAxis(Vector3 direction, Vector3 forward, Vector3 up, Vector3 right)
        {
            direction = SafeNormalize(direction);
            forward = SafeNormalize(forward);
            up = SafeNormalize(up);
            right = SafeNormalize(right);

            float forwardDot = Vector3.Dot(direction, forward);
            float upDot = Vector3.Dot(direction, up);
            float rightDot = Vector3.Dot(direction, right);

            float forwardAbs = Mathf.Abs(forwardDot);
            float upAbs = Mathf.Abs(upDot);
            float rightAbs = Mathf.Abs(rightDot);

            if (forwardAbs >= upAbs && forwardAbs >= rightAbs)
            {
                return forwardDot > 0.0f ? forward : -forward;
            }
            else if (upAbs >= rightAbs)
            {
                return upDot > 0.0f ? up : -up;
            }
            else
            {
                return rightDot > 0.0f ? right : -right;
            }
        }

        public static Vector3 ClosestPointOnRay(Vector3 start, Vector3 direction, Vector3 query, out float distance)
        {
            Vector3 offset = query - start;
            distance = Vector3.Dot(direction, offset);
            return start + (distance * direction);
        }

        public static Vector3 ClosestPointOnLine(Vector3 start, Vector3 end, Vector3 query, out float distanceNormalized)
        {
            Vector3 delta = end - start;
            float magnitude = delta.magnitude;

            // Degenerate segment: start and end are the same point.
            if (magnitude < Vector3.kEpsilon)
            {
                distanceNormalized = 0f;
                return start;
            }

            float invMagnitude = 1.0f / magnitude;
            Vector3 direction = delta * invMagnitude;

            Vector3 point = ClosestPointOnRay(start, direction, query, out float distance);
            distanceNormalized = distance * invMagnitude;

            if (distanceNormalized > 1.0f)
            {
                distanceNormalized = 1.0f;
                return end;
            }

            if (distanceNormalized < 0.0f)
            {
                distanceNormalized = 0.0f;
                return start;
            }

            return point;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MagnitudeXZ(Vector3 vec)
        {
            return Mathf.Sqrt(vec.x * vec.x + vec.z * vec.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SafeMagnitudeXZ(Vector3 vec)
        {
            float squareMagnitudeXZ = SquaredMagnitudeXZ(vec);
            if (squareMagnitudeXZ > 0.0001f)
            {
                vec.y = 0.0f;
                return Mathf.Sqrt(squareMagnitudeXZ);
            }

            return 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SquaredMagnitudeXZ(Vector3 vec)
        {
            return vec.x * vec.x + vec.z * vec.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceSq(Vector3 a, Vector3 b)
        {
            return Vector3.SqrMagnitude(a - b);
        }

        public static Vector3 ProjectOntoPlaneNormalized(Vector3 direction, Vector3 planeNormal, Vector3 fallback)
        {
            return SafeNormalize(ProjectOntoPlane(direction, planeNormal), fallback);
        }

        public static Vector3 ProjectOntoPlane(Vector3 line, Vector3 planeNormal)
        {
            return line - (planeNormal * Vector3.Dot(line, planeNormal));
        }

        public static Vector3 ProjectOntoDirection(Vector3 input, Vector3 directionNormal)
        {
            return directionNormal * Vector3.Dot(input, directionNormal);
        }

        public static Vector3 ProjectOntoPlaneAndScale(Vector3 vector, Vector3 normal)
        {
            float magnitude = vector.magnitude;
            vector = ProjectOntoPlane(vector, normal).normalized;
            return vector * magnitude;
        }

        /// <summary>
        /// Calculates the closest point on a plane from the input point.
        /// Equivalent to Plane.ClosestPointOnPlane() but without the need to instance a Plane.
        /// </summary>
        public static Vector3 ClosestPointOnPlane(Vector3 point, Vector3 planeNormal, Vector3 planeOrigin)
        {
            return point - Vector3.Dot(planeNormal, point - planeOrigin) * planeNormal;
        }

        /// <summary>
        /// Calculates the shortest distance between an arbitrary point and a plane.
        /// </summary>
        public static float DistanceToPlane(Vector3 point, Vector3 planeNormal, Vector3 planeOrigin)
        {
            // If you have an arbitrary vector (V) and a unit vector (U),
            // the dot product of U and V will calculate the length of the projection of V onto U.
            return Vector3.Dot(planeNormal, (point - planeOrigin));
        }

        public static void ForceMinimumLength(ref Vector3 vect, float minLength)
        {
            if (vect.sqrMagnitude < (minLength * minLength))
            {
                vect = vect.normalized * minLength;
            }
        }

        public static void LimitLength(ref Vector3 vect, float maxLength)
        {
            if (vect.sqrMagnitude > (maxLength * maxLength))
            {
                vect = vect.normalized * maxLength;
            }
        }

        public static void LimitLength(ref Vector2 vect, float maxLength)
        {
            if (vect.sqrMagnitude > (maxLength * maxLength))
            {
                vect = vect.normalized * maxLength;
            }
        }

        public static Vector3 RetainSignAndSquare(Vector3 vect)
        {
            return new Vector3(Mathf.Sign(vect.x) * vect.x * vect.x,
                               Mathf.Sign(vect.y) * vect.y * vect.y,
                               Mathf.Sign(vect.z) * vect.z * vect.z);
        }

        public static Vector3 RetainSignAndSquareRoot(Vector3 vect)
        {
            float mag = vect.magnitude;
            if (mag > 0.0f)
            {
                return vect / Mathf.Sqrt(mag);
            }

            return Vector3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 SafeNormalize(Vector3 vec, Vector3 fallback)
        {
            float squareMagnitude = Vector3.SqrMagnitude(vec);
            if (squareMagnitude > 0.0001f)
            {
                return vec / Mathf.Sqrt(squareMagnitude);
            }

            return fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 SafeNormalize(Vector3 vec)
        {
            float squareMagnitude = Vector3.SqrMagnitude(vec);
            if (squareMagnitude > 0.0001f)
            {
                return vec / Mathf.Sqrt(squareMagnitude);
            }

            return Vector3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 SafeNormalizeXZ(Vector3 vec, Vector3 fallback)
        {
            float squareMagnitudeXZ = SquaredMagnitudeXZ(vec);
            if (squareMagnitudeXZ > 0.0001f)
            {
                vec.y = 0.0f;
                return vec / Mathf.Sqrt(squareMagnitudeXZ);
            }

            return fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 SafeNormalizeXZ(Vector3 vec)
        {
            float squareMagnitudeXZ = SquaredMagnitudeXZ(vec);
            if (squareMagnitudeXZ > 0.0001f)
            {
                vec.y = 0.0f;
                return vec / Mathf.Sqrt(squareMagnitudeXZ);
            }

            return Vector3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 NormalizeXZ(Vector3 vec)
        {
            float mag = MagnitudeXZ(vec);
            if (mag < 1e-4f) return Vector3.zero;
            vec.y = 0.0f;
            return vec / mag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 NormalizeXZWithMagnitude(Vector3 vec, out float magnitude)
        {
            magnitude = MagnitudeXZ(vec);
            if (magnitude < float.Epsilon) return Vector3.zero;
            vec.y = 0.0f;
            vec.x /= magnitude;
            vec.z /= magnitude;
            return vec;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 NormalizedWithMagnitude(Vector3 vec, out float magnitude)
        {
            magnitude = Vector3.Magnitude(vec);
            return vec / magnitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 SafeNormalizedWithMagnitude(Vector3 vec, out float magnitude)
        {
            float squareMagnitude = Vector3.SqrMagnitude(vec);
            if (squareMagnitude > 0.0001f)
            {
                magnitude = Mathf.Sqrt(squareMagnitude);
                return vec / magnitude;
            }

            magnitude = 0f;
            return Vector3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 SafeNormalizedXZWithMagnitude(Vector3 vec, out float magnitude)
        {
            float squareMagnitude = vec.x * vec.x + vec.z * vec.z;
            if (squareMagnitude > 0.0001f)
            {
                magnitude = Mathf.Sqrt(squareMagnitude);
                vec.y = 0.0f;
                vec.x /= magnitude;
                vec.z /= magnitude;    // No need for vec.y / magnitude because it is 0/magnitude 
                return vec;
            }

            magnitude = 0f;
            return Vector3.zero;
        }

        public static Quaternion LookRotation(Vector3 fwd)
        {
            return Quaternion.LookRotation(fwd);
        }

        //This will get a Quaternion to make the X Axis face the target
        public static Quaternion XLookRotation(Vector3 right, Vector3 up)
        {
            Quaternion rightToForward = Quaternion.Euler(0f, -90f, 0f);
            Quaternion forwardToTarget = Quaternion.LookRotation(right, up);

            return forwardToTarget * rightToForward;
        }

        public static Quaternion SafeLookRotation(Vector3 fwd, Vector3 up, Quaternion fallback)
        {
            Quaternion rotation = fallback;

            if ((up.sqrMagnitude > 0.0001f) && (fwd.sqrMagnitude > 0.0001f))
            {
                fwd = fwd.normalized;
                up = up.normalized;

                if (Vector3.Dot(up, fwd) < 0.9999f)
                {
                    rotation = Quaternion.LookRotation(fwd, up);
                }
            }

            return rotation;
        }

        public static bool LineSegmentIntersect2D(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, ref Vector2 res)
        {
            // Store the values for fast access and easy
            // equations-to-code conversion
            float x1 = p1.x, x2 = p2.x, x3 = p3.x, x4 = p4.x;
            float y1 = p1.y, y2 = p2.y, y3 = p3.y, y4 = p4.y;

            float d = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            // If d is zero, there is no intersection
            if (d == 0) return false;

            // Get the x and y
            float pre = (x1 * y2 - y1 * x2);
            float post = (x3 * y4 - y3 * x4);
            float x = (pre * (x3 - x4) - (x1 - x2) * post) / d;
            float y = (pre * (y3 - y4) - (y1 - y2) * post) / d;

            // Check if the x and y coordinates are within both line segments
            if (x < Mathf.Min(x1, x2) || x > Mathf.Max(x1, x2) || x < Mathf.Min(x3, x4) || x > Mathf.Max(x3, x4)) return false;
            if (y < Mathf.Min(y1, y2) || y > Mathf.Max(y1, y2) || y < Mathf.Min(y3, y4) || y > Mathf.Max(y3, y4)) return false;

            // Return the point of intersection
            res.x = x;
            res.y = y;
            return true;
        }

        public static Vector3 FlatSlerp(Vector3 stDir, Vector3 edDir, float ratio)
        {
            float stX = 0.0f;
            float stY = 0.0f;
            float edX = 0.0f;
            float edY = 0.0f;

            MathUtils.CalcAnglesFromDir(stDir, ref stY, ref stX);
            MathUtils.CalcAnglesFromDir(edDir, ref edY, ref edX);

            float diffX = MathUtils.CalcMinAngleDif(stX, edX);
            float diffY = MathUtils.CalcMinAngleDif(stY, edY);

            float angX = stX + (diffX * ratio);
            float angY = stY + (diffY * ratio);

            Vector3 res = MathUtils.CalcDirFromAngles(angY, angX);

            return res;
        }

        public static Vector3 CubicHermite(Vector3 P0, Vector3 P1, Vector3 P2, Vector3 P3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            float tension = 0.5f;   // 0.5 equivale a catmull-rom

            Vector3 T1 = tension * (P2 - P0);
            Vector3 T2 = tension * (P3 - P1);

            float blend1 = 2 * t3 - 3 * t2 + 1;
            float blend2 = -2 * t3 + 3 * t2;
            float blend3 = t3 - 2 * t2 + t;
            float blend4 = t3 - t2;

            return blend1 * P1 + blend2 * P2 + blend3 * T1 + blend4 * T2;
        }

        public static bool CircleIntersect2D(Vector2 circlePos, float radius, Vector2 start, Vector2 end, out Vector2 result)
        {
            int intersections = FindLineCircleIntersections(circlePos, radius * radius, start, end, out Vector2 intersection1, out Vector2 intersection2);

            if (intersections == 1)
            {
                result = intersection1;//one intersection
                return true;
            }

            if (intersections == 2)
            {
                float dist1 = Vector2.Distance(intersection1, start);
                float dist2 = Vector2.Distance(intersection2, start);

                if (dist1 < dist2)
                {
                    result = intersection1;
                }
                else
                {
                    result = intersection2;
                }

                return true;
            }

            result = Vector2.zero;
            return false;
        }

        // Find the points of intersection.
        public static int FindLineCircleIntersections(Vector2 circlePos, float radiusSq, Vector2 point1, Vector2 point2, out Vector2 intersection1, out Vector2 intersection2)
        {
            float dx = point2.x - point1.x;
            float dy = point2.y - point1.y;

            float a = dx * dx + dy * dy;
            float b = 2.0f * (dx * (point1.x - circlePos.x) + dy * (point1.y - circlePos.y));
            float c = (point1.x - circlePos.x) * (point1.x - circlePos.x) + (point1.y - circlePos.y) * (point1.y - circlePos.y) - radiusSq;

            float det = b * b - 4.0f * a * c;
            if ((a <= Mathf.Epsilon) || (det < 0.0f))
            {
                // No real solutions.
                intersection1 = new Vector2(float.NaN, float.NaN);
                intersection2 = new Vector2(float.NaN, float.NaN);
                return 0;
            }
            else if (Mathf.Approximately(det, 0.0f))
            {
                // One solution.
                float t = -b / (2.0f * a);
                intersection1 = new Vector2(point1.x + t * dx, point1.y + t * dy);
                intersection2 = new Vector2(float.NaN, float.NaN);
                return 1;
            }
            else
            {
                // Two solutions.
                float t = (float)((-b + Mathf.Sqrt(det)) / (2.0f * a));
                intersection1 = new Vector2(point1.x + t * dx, point1.y + t * dy);
                t = (float)((-b - Mathf.Sqrt(det)) / (2.0f * a));
                intersection2 = new Vector2(point1.x + t * dx, point1.y + t * dy);
                return 2;
            }
        }

        // Reference in https://en.wikipedia.org/wiki/Line-sphere_intersection
        public static bool RaySphereIntersection(Vector3 origin, Vector3 direction, Vector3 center, float radius, out Vector3 intersection, bool unclamped = false)
        {
            intersection = Vector3.zero;

            Vector3 toOrigin = origin - center;
            float scalarProj = Vector3.Dot(direction, toOrigin);
            float radiusSq = radius * radius;
            float discriminant = scalarProj * scalarProj - (toOrigin.sqrMagnitude - radiusSq);
            if (discriminant >= 0.0f)
            {
                float ratio = -scalarProj + Mathf.Sqrt(discriminant);
                // Only accept intersection if it is "in front of" the ray origin or with unclamped flag
                if (ratio > 0.0 || unclamped)
                {
                    intersection = origin + direction * ratio;
                    return true;
                }
            }

            return false;
        }

        public static Vector3 ClosestCirclePoint(Vector3 center, Vector3 normal, float radius, Vector3 point)
        {
            point = ClosestPointOnPlane(point, normal, center);
            Vector3 toPoint = Vector3.Normalize(point - center);
            return center + toPoint * radius;
        }

        public static Vector3 ClosestCirclePointToRay(Vector3 center, Vector3 normal, float radius, Vector3 origin, Vector3 direction)
        {
            Vector3 closestPoint = ClosestPointOnRay(origin, direction, center, out float distance);
            // If distance to closest point on Ray is smaller than radius, then there is at least one intersection.
            if (distance < radius)
            {
                RaySphereIntersection(origin, direction, center, radius, out closestPoint, true);
            }

            closestPoint = ClosestPointOnPlane(closestPoint, normal, center);
            Vector3 toPoint = Vector3.Normalize(closestPoint - center);
            return center + toPoint * radius;
        }

        /// <summary>
        /// Calculates the angle between vectors in radians.
        /// </summary>
        public static double AngleBetweenVectors(Vector3 vecA, Vector3 vecB)
        {
            float dotProduct = Vector3.Dot(vecA, vecB);
            float vectorsMagnitude = vecA.magnitude * vecB.magnitude;
            double angle = System.Math.Acos(dotProduct / vectorsMagnitude);

            if (double.IsNaN(angle))
                return 0;

            return angle;
        }

        public static bool TryParse(string str, out Vector3 position)
        {
            string[] strings = str.Split(',');
            bool result = strings.Length == 3;
            if (result)
            {
                result &= float.TryParse(strings[0], out position.x);
                result &= float.TryParse(strings[1], out position.y);
                result &= float.TryParse(strings[2], out position.z);
            }
            else
            {
                position = Vector3.zero;
            }
            return result;
        }

        /// <summary>
        /// Compares if the angle between two vector is smaller than theta.
        /// Replacement function to doing Vector3.Angle(a,b) < theta.
        /// This performs much faster if the cos of theta is cached ahead of TimeSystem.
        /// </summary>
        /// <param name="unitA">A unit vector A</param>
        /// <param name="unitB">A unit vector B</param>
        /// <param name="maxThetaAngle">the cos of some max angle theta</param>
        /// <returns>true if the angle is smaller than theta otherwise false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastWithinAngle(Vector3 unitA, Vector3 unitB, float maxThetaAngle)
        {
            //angles are increasing, cos is decreasing hence the >
            return Vector3.Dot(unitA, unitB) >= maxThetaAngle;
        }

        public static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
        {
            return rotation * (point - pivot) + pivot;
        }

        public static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 angles)
        {
            return RotatePointAroundPivot(point, pivot, Quaternion.Euler(angles));
        }

        public static Vector3 TransformDirection(Vector3 rawDir, Quaternion transformRot)
        {
            return transformRot * rawDir;
        }

        public static Vector3 TransformPoint(Vector3 localPos, Vector3 transformPos, Quaternion transformRot)
        {
            return transformPos + TransformDirection(localPos, transformRot);
        }

        public static Vector3 ApproximateOrthogonalDirection(Vector3 vector)
        {
            if (vector == Vector3.zero)
                return vector;

            float x = Mathf.Abs(vector.x);
            float y = Mathf.Abs(vector.y);
            float z = Mathf.Abs(vector.z);

            if (x > y && x > z)
                return new Vector3(Mathf.Sign(vector.x), 0, 0);
            if (y > x && y > z)
                return new Vector3(0, Mathf.Sign(vector.y), 0);
            else
                return new Vector3(0, 0, Mathf.Sign(vector.z));
        }

        public static Vector3 CalcForwardXZ(Vector3 forward, Vector3 up)
        {
            Vector3 upFromUserCenterEye = Vector3.Lerp(up, -up, forward.y); // use down instead of up the more you look up
            Vector3 userCenterEyeForwardXZ = Vector3.Lerp(upFromUserCenterEye, forward, up.y); // use up instead of forward the more you look down
            userCenterEyeForwardXZ.y = 0.0f;
            return Vector3.Normalize(userCenterEyeForwardXZ);
        }

        public static bool IsNaN(this Vector3 vec)
        {
            return float.IsNaN(vec.x) || float.IsNaN(vec.y) || float.IsNaN(vec.z);
        }

        public static bool IsInfinity(this Vector3 vec)
        {
            return float.IsInfinity(vec.x) || float.IsInfinity(vec.y) || float.IsInfinity(vec.z);
        }

        public static bool IsInvalidPosition(this Vector3 vec)
        {
            return vec.IsNaN() || vec.IsInfinity();
        }

        public static Vector3 Floor(Vector3 vector3)
        {
            vector3.x = Mathf.Floor(vector3.x);
            vector3.y = Mathf.Floor(vector3.y);
            vector3.z = Mathf.Floor(vector3.z);
            return vector3;
        }
    }
}

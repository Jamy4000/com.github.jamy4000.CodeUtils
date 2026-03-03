using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Unity.Collections;

namespace UnityTechnologies.CodeUtils
{
    public static class MathUtils
    {
        public const float TWO_PI = 6.283185307f;
        public const float COS_45 = 0.707106781f;
        public const float GRAVITY = 9.80665f;
        public const float DOUBLE_GRAVITY = 9.80665f * 2f;
        public const float HALF_GRAVITY = 9.80665f * 0.5f;

        public const float MAX_ANGLE_DEGREES = 359.999969482f;
        private const float TOLERANCE = 1E-2f;

        public static float HooksLawDampen(float springStrength, float springLength, float dampeningFactor, float currentDistanceToCenterOfMass, ref float previousDistanceToCenterOfMass)
        {
            float forceAmount = springStrength * (springLength - currentDistanceToCenterOfMass) + (dampeningFactor * (previousDistanceToCenterOfMass - currentDistanceToCenterOfMass));
            forceAmount = Mathf.Max(0f, forceAmount);
            previousDistanceToCenterOfMass = currentDistanceToCenterOfMass;

            return forceAmount;
        }

        /// <summary>
        /// Determine if there are points in the plane.
        /// </summary>
        /// <param name="p">Points to investigate.</param>
        /// <param name="t1">Plane point.</param>
        /// <param name="t2">Plane point.</param>
        /// <param name="t3">Plane point.</param>
        /// <summary>
        /// <returns>Whether points exist in the triangle plane.</returns>
        public static bool ExistPointInPlane(Vector3 p, Vector3 t1, Vector3 t2, Vector3 t3)
        {
            var v1 = t2 - t1;
            var v2 = t3 - t1;
            var vp = p - t1;

            var nv = Vector3.Cross(v1, v2);
            var val = Vector3.Dot(nv.normalized, vp.normalized);
            if (-TOLERANCE < val && val < TOLERANCE)
                return true;
            return false;
        }

        /// <summary>
        /// Investigate whether a point exists on an edge.
        /// </summary>
        /// <param name="p">Points to investigate.</param>
        /// <param name="v1">Edge forming point.</param>
        /// <param name="v2">Edge forming point.</param>
        /// <returns>Whether a point exists on an edge.</returns>
        public static bool ExistPointOnEdge(Vector3 p, Vector3 v1, Vector3 v2)
        {
            return 1 - TOLERANCE < Vector3.Dot((v2 - p).normalized, (v2 - v1).normalized);
        }

        /// <summary>
        /// Investigate whether a point exists on a side of a triangle.
        /// </summary>
        /// <param name="p">Points to investigate.</param>
        /// <param name="t1">Vertex of triangle.</param>
        /// <param name="t2">Vertex of triangle.</param>
        /// <param name="t3">Vertex of triangle.</param>
        /// <returns>Whether points lie on the sides of the triangle.</returns>
        public static bool ExistPointOnTriangleEdge(Vector3 p, Vector3 t1, Vector3 t2, Vector3 t3)
        {
            if (ExistPointOnEdge(p, t1, t2) || ExistPointOnEdge(p, t2, t3) || ExistPointOnEdge(p, t3, t1))
                return true;
            return false;
        }

        /// <summary>
        /// Investigate whether a point exists inside the triangle.
        /// All points to be entered must be on the same plane.
        /// </summary>
        /// <param name="p">Points to investigate.</param>
        /// <param name="t1">Vertex of triangle.</param>
        /// <param name="t2">Vertex of triangle.</param>
        /// <param name="t3">Vertex of triangle.</param>
        /// <returns>Whether the point exists inside the triangle.</returns>
        public static bool ExistPointInTriangle(Vector3 p, Vector3 t1, Vector3 t2, Vector3 t3)
        {
            var a = Vector3.Cross(t1 - t3, p - t1).normalized;
            var b = Vector3.Cross(t2 - t1, p - t2).normalized;
            var c = Vector3.Cross(t3 - t2, p - t3).normalized;

            var d_ab = Vector3.Dot(a, b);
            var d_bc = Vector3.Dot(b, c);

            if (1 - TOLERANCE < d_ab && 1 - TOLERANCE < d_bc)
                return true;
            return false;
        }

        /// <summary>
        /// Calculate UV coordinates within a triangle of points.
        /// The point to be investigated needs to be a point inside the triangle.
        /// </summary>
        /// <param name="p">Points to investigate.</param>
        /// <param name="t1">Vertex of triangle.</param>
        /// <param name="t1UV">UV coordinates of t1.</param>
        /// <param name="t2">Vertex of triangle.</param>
        /// <param name="t2UV">UV coordinates of t2.</param>
        /// <param name="t3">Vertex of triangle.</param>
        /// <param name="t3UV">UV coordinates of t3.</param>
        /// <param name="transformMatrix">MVP transformation matrix.</param>
        /// <returns>UV coordinates of the point to be investigated.</returns>
        public static Vector2 TextureCoordinateCalculation(Vector3 p, Vector3 t1, Vector2 t1UV, Vector3 t2, Vector2 t2UV, Vector3 t3, Vector2 t3UV, Matrix4x4 transformMatrix)
        {
            Vector4 p1_p = transformMatrix * new Vector4(t1.x, t1.y, t1.z, 1);
            Vector4 p2_p = transformMatrix * new Vector4(t2.x, t2.y, t2.z, 1);
            Vector4 p3_p = transformMatrix * new Vector4(t3.x, t3.y, t3.z, 1);
            Vector4 p_p = transformMatrix * new Vector4(p.x, p.y, p.z, 1);
            Vector2 p1_n = new Vector2(p1_p.x, p1_p.y) / p1_p.w;
            Vector2 p2_n = new Vector2(p2_p.x, p2_p.y) / p2_p.w;
            Vector2 p3_n = new Vector2(p3_p.x, p3_p.y) / p3_p.w;
            Vector2 p_n = new Vector2(p_p.x, p_p.y) / p_p.w;
            var s = 0.5f * ((p2_n.x - p1_n.x) * (p3_n.y - p1_n.y) - (p2_n.y - p1_n.y) * (p3_n.x - p1_n.x));
            var s1 = 0.5f * ((p3_n.x - p_n.x) * (p1_n.y - p_n.y) - (p3_n.y - p_n.y) * (p1_n.x - p_n.x));
            var s2 = 0.5f * ((p1_n.x - p_n.x) * (p2_n.y - p_n.y) - (p1_n.y - p_n.y) * (p2_n.x - p_n.x));
            var u = s1 / s;
            var v = s2 / s;
            var w = 1 / ((1 - u - v) * 1 / p1_p.w + u * 1 / p2_p.w + v * 1 / p3_p.w);
            return w * ((1 - u - v) * t1UV / p1_p.w + u * t2UV / p2_p.w + v * t3UV / p3_p.w);
        }

        /// <summary>
        /// Returns the vertex of the triangle with the closest vertex to the point to be examined from the given vertex and triangle list.
        /// </summary>
        /// <param name="p">Points to investigate.</param>
        /// <param name="vertices">Vertex list.</param>
        /// <param name="triangles">Triangle list.</param>
        /// <returns>The triangle closest to the point to be investigated.</returns>
        public static Vector3[] GetNearestVerticesTriangle(Vector3 p, Vector3[] vertices, int[] triangles)
        {
            List<Vector3> ret = new List<Vector3>();

            int nearestIndex = triangles[0];
            float nearestDistanceSq = Vector3.SqrMagnitude(vertices[nearestIndex] - p);

            for (int i = 0; i < vertices.Length; ++i)
            {
                float distanceSq = Vector3.SqrMagnitude(vertices[i] - p);
                if (distanceSq < nearestDistanceSq)
                {
                    nearestDistanceSq = distanceSq;
                    nearestIndex = i;
                }
            }

            for (int i = 0; i < triangles.Length; ++i)
            {
                if (triangles[i] == nearestIndex)
                {
                    var m = i % 3;
                    int i0 = i, i1 = 0, i2 = 0;
                    switch (m)
                    {
                        case 0:
                            i1 = i + 1;
                            i2 = i + 2;
                            break;

                        case 1:
                            i1 = i - 1;
                            i2 = i + 1;
                            break;

                        case 2:
                            i1 = i - 1;
                            i2 = i - 2;
                            break;

                        default:
                            break;
                    }
                    ret.Add(vertices[triangles[i0]]);
                    ret.Add(vertices[triangles[i1]]);
                    ret.Add(vertices[triangles[i2]]);
                }
            }
            return ret.ToArray();
        }


        /// <summary>
        /// Returns the vertex of the triangle with the closest vertex to the point to be examined from the given vertex and triangle list.
        /// </summary>
        /// <param name="p">Points to investigate.</param>
        /// <param name="vertices">Vertex list.</param>
        /// <param name="triangles">Triangle list.</param>
        /// <returns>The triangle closest to the point to be investigated.</returns>
        public static Vector3[] GetNearestVerticesTriangle(float3 p, NativeArray<float3> vertices, NativeArray<int> triangles)
        {
            List<Vector3> ret = new List<Vector3>();

            int nearestIndex = triangles[0];
            float nearestDistanceSq = math.lengthsq(vertices[nearestIndex] - p);

            for (int i = 0; i < vertices.Length; ++i)
            {
                float distanceSq = math.lengthsq(vertices[i] - p);
                if (distanceSq < nearestDistanceSq)
                {
                    nearestDistanceSq = distanceSq;
                    nearestIndex = i;
                }
            }

            for (int i = 0; i < triangles.Length; ++i)
            {
                if (triangles[i] == nearestIndex)
                {
                    var m = i % 3;
                    int i0 = i, i1 = 0, i2 = 0;
                    switch (m)
                    {
                        case 0:
                            i1 = i + 1;
                            i2 = i + 2;
                            break;

                        case 1:
                            i1 = i - 1;
                            i2 = i + 1;
                            break;

                        case 2:
                            i1 = i - 1;
                            i2 = i - 2;
                            break;

                        default:
                            break;
                    }
                    ret.Add(vertices[triangles[i0]]);
                    ret.Add(vertices[triangles[i1]]);
                    ret.Add(vertices[triangles[i2]]);
                }
            }
            return ret.ToArray();
        }

        /// <summary>
        /// Returns the vertex of the triangle with the closest vertex to the point to be examined from the given vertex and triangle list.
        /// </summary>
        /// <param name="p">Points to investigate.</param>
        /// <param name="vertices">Vertex list.</param>
        /// <param name="triangles">Triangle list.</param>
        /// <returns>The triangle closest to the point to be investigated.</returns>
        public static Vector3[] GetNearestVerticesTriangle(Vector3 p, Unity.Collections.NativeArray<Vector3> vertices, Unity.Collections.NativeArray<int> triangles)
        {
            List<Vector3> ret = new List<Vector3>();

            int nearestIndex = triangles[0];
            float nearestDistance = Vector3.SqrMagnitude(vertices[nearestIndex] - p);

            for (int i = 0; i < vertices.Length; ++i)
            {
                float distanceSq = Vector3.SqrMagnitude(vertices[i] - p);
                if (distanceSq < nearestDistance)
                {
                    nearestDistance = distanceSq;
                    nearestIndex = i;
                }
            }

            for (int i = 0; i < triangles.Length; ++i)
            {
                if (triangles[i] == nearestIndex)
                {
                    var m = i % 3;
                    int i0 = i, i1 = 0, i2 = 0;
                    switch (m)
                    {
                        case 0:
                            i1 = i + 1;
                            i2 = i + 2;
                            break;

                        case 1:
                            i1 = i - 1;
                            i2 = i + 1;
                            break;

                        case 2:
                            i1 = i - 1;
                            i2 = i - 2;
                            break;

                        default:
                            break;
                    }
                    ret.Add(vertices[triangles[i0]]);
                    ret.Add(vertices[triangles[i1]]);
                    ret.Add(vertices[triangles[i2]]);
                }
            }
            return ret.ToArray();
        }

        /// <summary>
        /// Use this sparingly, as it isn't guaranteed to be as fast
        /// as doing so in-place with a local variable. This is for
        /// one-Time precomputes like fetching a value from a settings
        /// singleton, and exists for readability on low-cost operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SquaredSlow(this float x) => x * x;

        public static float SqrtRetainSign(float x)
        {
            return Mathf.Sqrt(Mathf.Abs(x)) * SignExcludingZero(x);
        }

        public static bool SolveQuadraticMax(float a, float b, float c, ref float res)
        {
            //< solve quadratic and return highest result
            float dSqr = b * b - 4.0f * a * c;
            if ((a * a) > 0.000001f)
            {
                if (dSqr > 0.0f)
                {
                    float d = Mathf.Sqrt(dSqr);
                    float inv2a = 1.0f / (2.0f * a);

                    float t1 = (-b + d) * inv2a;
                    float t2 = (-b - d) * inv2a;

                    res = Mathf.Max(t1, t2);
                    return true;
                }
                else if (Mathf.Abs(dSqr) < 0.00001f)
                {
                    res = -b / (2.0f * a);
                    return true;
                }
            }
            else if ((b * b) > 0.000001f)
            {
                res = -c / b;
                return true;
            }
            else if (Mathf.Abs(c) < 0.00001f)
            {
                res = 0.0f;
                return true;
            }

            return false;
        }

        public static bool SolveQuadraticMaxAboveZero(float a, float b, float c, ref float res)
        {
            //< solve quadratic and return highest result
            float dSqr = b * b - 4.0f * a * c;
            if ((a * a) > 0.000001f)
            {
                if (dSqr > 0.0f)
                {
                    float d = Mathf.Sqrt(dSqr);
                    float inv2a = 1.0f / (2.0f * a);

                    float t1 = (-b + d) * inv2a;
                    float t2 = (-b - d) * inv2a;

                    res = Mathf.Max(t1, t2);
                    return res >= 0;
                }
                else if (Mathf.Abs(dSqr) < 0.00001f)
                {
                    res = -b / (2.0f * a);
                    return res >= 0;
                }
            }
            else if ((b * b) > 0.000001f)
            {
                res = -c / b;
                return res >= 0;
            }
            else if (Mathf.Abs(c) < 0.00001f)
            {
                res = 0.0f;
                return true;
            }

            return false;
        }

        public static bool SolveQuadraticMin(float a, float b, float c, ref float res)
        {
            //< solve quadratic and return highest result
            float dSqr = b * b - 4.0f * a * c;
            if ((a * a) > 0.000001f)
            {
                if (dSqr > 0.0f)
                {
                    float d = Mathf.Sqrt(dSqr);
                    float inv2a = 1.0f / (2.0f * a);

                    float t1 = (-b + d) * inv2a;
                    float t2 = (-b - d) * inv2a;

                    res = Mathf.Min(t1, t2);
                    return true;
                }
                else if (Mathf.Abs(dSqr) < 0.00001f)
                {
                    res = -b / (2.0f * a);
                    return true;
                }
            }
            else if ((b * b) > 0.000001f)
            {
                res = -c / b;
                return true;
            }
            else if (Mathf.Abs(c) < 0.00001f)
            {
                res = 0.0f;
                return true;
            }

            return false;
        }

        public static bool SolveQuadratic(float a, float b, float c, ref float resLow, ref float resHigh)
        {
            //< solve quadratic and return highest result
            float dSqr = b * b - 4.0f * a * c;
            if ((a * a) > 0.000001f)
            {
                if (dSqr > 0.0f)
                {
                    float d = Mathf.Sqrt(dSqr);
                    float inv2a = 1.0f / (2.0f * a);

                    float t1 = (-b + d) * inv2a;
                    float t2 = (-b - d) * inv2a;

                    resHigh = Mathf.Max(t1, t2);
                    resLow = Mathf.Min(t1, t2);
                    return true;
                }
                else if (Mathf.Abs(dSqr) < 0.00001f)
                {
                    resHigh = resLow = -b / (2.0f * a);
                    return true;
                }
            }
            else if ((b * b) > 0.000001f)
            {
                resHigh = resLow = -c / b;
                return true;
            }
            else if (Mathf.Abs(c) < 0.00001f)
            {
                resHigh = resLow = 0.0f;
                return true;
            }

            return false;
        }

        public static int SolveCubic(float a, float b, float c, float d, ref float res1, ref float res2, ref float res3)
        {
            //< calculates roots for Cubics in the form ax� + bx� + cx + d = 0 .. returns number of solutions
            if (a == 0)
            {
                //< it's just a quadratic .. solve that instead
                if (SolveQuadratic(b, c, d, ref res1, ref res2))
                {
                    return 2;
                }
                return 0;
            }

            float disc, q, r, dum1, s, t, term1, r13;

            b /= a;
            c /= a;
            d /= a;
            q = (3.0f * c - (b * b)) / 9.0f;
            r = -(27.0f * d) + b * (9.0f * c - 2.0f * (b * b));
            r /= 54.0f;

            disc = q * q * q + r * r;
            term1 = (b / 3.0f);

            res1 = 0.0f;

            if (disc > 0)
            {
                // one root real, two complex
                s = r + Mathf.Sqrt(disc);
                s = ((s < 0) ? -Mathf.Pow(-s, (1.0f / 3.0f)) : Mathf.Pow(s, (1.0f / 3.0f)));
                t = r - Mathf.Sqrt(disc);
                t = ((t < 0) ? -Mathf.Pow(-t, (1.0f / 3.0f)) : Mathf.Pow(t, (1.0f / 3.0f)));

                res1 = -term1 + s + t;
                return 1;
            }

            //< The remaining options are all real
            if (disc == 0)
            {
                //< All roots real, at least two are equal.
                r13 = ((r < 0) ? -Mathf.Pow(-r, (1.0f / 3.0f)) : Mathf.Pow(r, (1.0f / 3.0f)));

                res1 = -term1 + 2.0f * r13;
                res2 = -(r13 + term1);
                res3 = res2;
                return 2;
            }

            //< All roots are real and different
            q = -q;
            dum1 = q * q * q;
            dum1 = Mathf.Acos(r / Mathf.Sqrt(dum1));
            r13 = 2.0f * Mathf.Sqrt(q);
            res1 = -term1 + r13 * Mathf.Cos(dum1 / 3.0f);
            res2 = -term1 + r13 * Mathf.Cos((dum1 + 2.0f * Mathf.PI) / 3.0f);
            res3 = -term1 + r13 * Mathf.Cos((dum1 + 4.0f * Mathf.PI) / 3.0f);
            return 3;
        }

        public static bool SolveCubicMax(float a, float b, float c, float d, ref float result)
        {
            float res1 = 0;
            float res2 = 0;
            float res3 = 0;

            int vals = SolveCubic(a, b, c, d, ref res1, ref res2, ref res3);

            if (vals == 3)
            {
                result = Mathf.Max(res1, Mathf.Max(res2, res3));
                return true;
            }
            else if (vals == 2)
            {
                result = Mathf.Max(res1, res2);
                return true;
            }
            else if (vals == 1)
            {
                result = res1;
                return true;
            }

            return false;
        }

        public static bool SolveCubicMin(float a, float b, float c, float d, ref float result)
        {
            float res1 = 0;
            float res2 = 0;
            float res3 = 0;

            int vals = SolveCubic(a, b, c, d, ref res1, ref res2, ref res3);

            if (vals == 3)
            {
                result = Mathf.Min(res1, Mathf.Min(res2, res3));
                return true;
            }
            else if (vals == 2)
            {
                result = Mathf.Min(res1, res2);
                return true;
            }
            else if (vals == 1)
            {
                result = res1;
                return true;
            }

            return false;
        }

        public static bool SolveCubicMinAboveZero(float a, float b, float c, float d, ref float result)
        {
            float res1 = 0;
            float res2 = 0;
            float res3 = 0;

            int vals = SolveCubic(a, b, c, d, ref res1, ref res2, ref res3);

            if (vals == 3)
            {
                float maxVal = Mathf.Max(res1, Mathf.Max(res2, res3));
                if (maxVal < 0.0f) return false; //< no result above zero;

                if (res1 < 0.0f) res1 = maxVal;
                if (res2 < 0.0f) res2 = maxVal;
                if (res3 < 0.0f) res3 = maxVal;

                result = Mathf.Min(res1, Mathf.Min(res2, res3));
                return true;
            }
            else if (vals == 2)
            {
                float maxVal = Mathf.Max(res1, res2);
                if (maxVal < 0.0f) return false; //< no result above zero;

                if (res1 < 0.0f) res1 = maxVal;
                if (res2 < 0.0f) res2 = maxVal;

                result = Mathf.Min(res1, res2);
                return true;
            }
            else if (vals == 1)
            {
                if (res1 < 0.0f) return false; //< no result above zero;

                result = res1;
                return true;
            }

            return false;
        }

        public static float SolveForPlane(Vector2 flatCoords, Vector3 planePoint, Vector3 planeNormal)
        {
            return planePoint.y - (planeNormal.x * (flatCoords.x - planePoint.x) + planeNormal.z * (flatCoords.y - planePoint.z)) / planeNormal.y;
        }

        public static float Quantize(float value, float steps)
        {
            return Mathf.Floor((value / steps) + 0.5f) * steps;
        }

        //------------------------------------------------------------------------
        //------------------------------------------------------------------------
        public static float AbsDifferenceOverSum(float a, float b)
        {
            return Mathf.Abs(a - b) / (a + b);
        }

        //------------------------------------------------------------------------
        // returns the range between the biggest and smallest angles within this triangle with respect to a given normal .. IN RADIANS!!
        //------------------------------------------------------------------------
        public static float AngleRangeRads(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            float minVal = Mathf.PI;
            float maxVal = -Mathf.PI;

            Vector3 line01 = (v1 - v0).normalized;
            Vector3 line12 = (v2 - v1).normalized;
            Vector3 line20 = (v0 - v2).normalized;

            float ang012 = Vector3.Angle(line12, line01) * Mathf.Deg2Rad;
            float ang120 = Vector3.Angle(line20, line12) * Mathf.Deg2Rad;
            float ang201 = Vector3.Angle(line01, line20) * Mathf.Deg2Rad;

            minVal = Mathf.Min(minVal, ang012);
            minVal = Mathf.Min(minVal, ang120);
            minVal = Mathf.Min(minVal, ang201);

            maxVal = Mathf.Max(maxVal, ang012);
            maxVal = Mathf.Max(maxVal, ang120);
            maxVal = Mathf.Max(maxVal, ang201);

            return maxVal - minVal;
        }

        //------------------------------------------------------------------------
        // returns the range between the biggest and smallest sines of angles within this triangle
        //------------------------------------------------------------------------
        public static float FlatnessScore(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            float minVal = Mathf.PI;
            float maxVal = -Mathf.PI;

            Vector3 line01 = (v1 - v0).normalized;
            Vector3 line12 = (v2 - v1).normalized;
            Vector3 line20 = (v0 - v2).normalized;

            float ang012 = Vector3.Angle(line12, line01) * Mathf.Deg2Rad;
            float ang120 = Vector3.Angle(line20, line12) * Mathf.Deg2Rad;
            float ang201 = Vector3.Angle(line01, line20) * Mathf.Deg2Rad;

            minVal = Mathf.Min(minVal, ang012);
            minVal = Mathf.Min(minVal, ang120);
            minVal = Mathf.Min(minVal, ang201);

            maxVal = Mathf.Max(maxVal, ang012);
            maxVal = Mathf.Max(maxVal, ang120);
            maxVal = Mathf.Max(maxVal, ang201);

            return (maxVal - minVal) / Mathf.PI;
        }

        public static bool IsConvexQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 normal)
        {
            Vector3 line0to1 = v1 - v0;
            Vector3 line1to2 = v2 - v1;
            Vector3 line2to3 = v3 - v2;
            Vector3 line3to0 = v0 - v3;

            Vector3 perp = -Vector3.Cross(line1to2, line0to1);
            if (Vector3.Dot(perp, normal) < 0.0f) return false;

            perp = -Vector3.Cross(line2to3, line1to2);
            if (Vector3.Dot(perp, normal) < 0.0f) return false;

            perp = -Vector3.Cross(line3to0, line2to3);
            if (Vector3.Dot(perp, normal) < 0.0f) return false;

            perp = -Vector3.Cross(line0to1, line3to0);
            if (Vector3.Dot(perp, normal) < 0.0f) return false;

            return true;
        }

        public static float ShortestEdgeLength(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            float mag01Sq = (v1 - v0).sqrMagnitude;
            float mag12Sq = (v2 - v1).sqrMagnitude;
            float mag20Sq = (v0 - v2).sqrMagnitude;

            float minMag = (mag01Sq < mag12Sq) ? mag01Sq : mag12Sq;
            minMag = (minMag < mag20Sq) ? minMag : mag20Sq;

            return Mathf.Sqrt(minMag);
        }

        public static float ConvexPolygonArea(params Vector3[] sortedPolygonVertices)
        {
            if ((sortedPolygonVertices?.Length ?? 0) < 3)
                return 0.0f;

            float totalArea = 0.0f;
            for (int i = 1; i < sortedPolygonVertices.Length - 1; i++)
            {
                float area = TriangleArea(sortedPolygonVertices[0], sortedPolygonVertices[i], sortedPolygonVertices[i + 1]);
                totalArea += area;
            }

            return totalArea;
        }

        public static float TriangleArea(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            Vector3 edge10 = v1 - v0;
            Vector3 edge12 = v1 - v2;
            Vector3 perp = Vector3.Cross(edge10.normalized, edge12.normalized);
            float res = (edge10.magnitude * edge12.magnitude * 0.5f) * perp.magnitude;
            return res;
        }

        public static Vector3 TriangleNormal(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            Vector3 edge10 = (v1 - v0).normalized;
            Vector3 edge12 = (v1 - v2).normalized;
            Vector3 res = Vector3.Cross(edge12, edge10).normalized;
            return res;
        }

        // NOTE: Please don't use any Mathf code in here as it's created for use within DOTS unmanaged/parallelised
        // code which doesn't support it.
        public static bool RayPlaneIntersection(Vector3 rayOrigin, Vector3 rayDirection, Vector3 pointOnPlane, Vector3 planeNormal, out float distance, out bool allPointsIntersect)
        {
            float lDotN = Vector3.Dot(rayDirection, planeNormal);
            float p0MinusL0DotN = Vector3.Dot((pointOnPlane - rayOrigin), planeNormal);

            // if l.n = 0 then there are two cases:
            // 1) if (p0 - l0).n = 0 then every point on the line intersects
            // 2) otherwise none of them do.
            if (Math.Abs(lDotN) <= float.Epsilon)
            {
                if (Math.Abs(p0MinusL0DotN) <= float.Epsilon)
                {
                    allPointsIntersect = true;
                    distance = 0.0f;
                    return true;
                }
                else
                {
                    allPointsIntersect = false;
                    distance = float.PositiveInfinity;
                    return false;
                }
            }

            // if d is negative, then it's behind the origin point of the raycast
            // and shouldn't count as a success.
            allPointsIntersect = false;
            distance = p0MinusL0DotN / lDotN;
            return distance >= 0.0f;
        }

        public static bool TriangleIntersection(Vector3 rayStart, Vector3 rayEnd, Vector3 v0, Vector3 v1, Vector3 v2, ref Vector3 result)
        {
            Vector3 fullRay = rayEnd - rayStart;
            Vector3 rayDir = (fullRay).normalized;

            // Vectors from p1 to p2/p3 (edges)
            Vector3 e1, e2;

            Vector3 p, q, t;
            float det, invDet, u, v;


            //Find vectors for two edges sharing vertex/point p1
            e1 = v1 - v0;
            e2 = v2 - v0;

            // calculating determinant
            p = Vector3.Cross(rayDir, e2);

            //Calculate determinat
            det = Vector3.Dot(e1, p);

            //if determinant is near zero, ray lies in plane of triangle otherwise not
            if (det > -Mathf.Epsilon && det < Mathf.Epsilon)
            {
                return false;
            }

            invDet = 1.0f / det;

            //calculate distance from p1 to ray origin
            t = rayStart - v0;

            //Calculate u parameter
            u = Vector3.Dot(t, p) * invDet;

            //Check for ray hit
            if (u < 0 || u > 1) { return false; }

            //Prepare to OnWaterPuddleWasPooled v parameter
            q = Vector3.Cross(t, e1);

            //Calculate v parameter
            v = Vector3.Dot(rayDir, q) * invDet;

            //Check for ray hit
            if (v < 0 || u + v > 1)
            {
                return false;
            }

            if ((Vector3.Dot(e2, q) * invDet) > Mathf.Epsilon)
            {
                //ray does intersect

                float w = 1.0f - (u + v);

                result = (v0 * u) + (v1 * v) + (v2 * w);

                Vector3 resLine = result - rayStart;

                if (resLine.sqrMagnitude <= fullRay.sqrMagnitude)
                {
                    //< intersection exists within ray
                    return true;
                }
            }

            // No hit at all
            return false;
        }

        public static float GetAdjustmentByPlaneAlongAxis(Vector3 offset, Vector3 axis, Vector3 planeNormal)
        {
            float diff = Vector3.Dot(axis, planeNormal);

            Vector3 flatOffset = VectorExtensions.ProjectOntoPlane(offset, planeNormal);
            float dist = flatOffset.magnitude;
            float extra = dist / diff;

            return extra;
        }

        public static Vector3 AdjustByPlaneAlongAxis(Vector3 offset, Vector3 axis, Vector3 planeNormal)
        {
            float diff = Vector3.Dot(axis, planeNormal);

            Vector3 flatOffset = VectorExtensions.ProjectOntoPlane(offset, planeNormal);
            float dist = flatOffset.magnitude;
            float extra = dist / diff;

            return offset + (axis * extra);
        }

        public static Vector3 ProjectOntoPlaneIfBelow(Vector3 line, Vector3 planeNormal)
        {
            float planeDot = Vector3.Dot(line, planeNormal);
            if (planeDot < 0)
            {
                return VectorExtensions.ProjectOntoPlane(line, planeNormal);
            }
            return line;
        }

        public static Vector3 ProjectOntoPlaneIfAbove(Vector3 line, Vector3 planeNormal)
        {
            float planeDot = Vector3.Dot(line, planeNormal);
            if (planeDot > 0)
            {
                return VectorExtensions.ProjectOntoPlane(line, planeNormal);
            }
            return line;
        }

        public static Vector3 NearpointOnLine(Vector3 p0, Vector3 p1, Vector3 test)
        {
            Vector3 line = p1;

            test -= p0;
            line -= p0;

            float lenSq = line.sqrMagnitude;
            float t = 0.0f;

            if (lenSq > 0.0f)
            {
                float offset = Vector3.Dot(test, line);
                t = Mathf.Clamp01(offset / lenSq);
            }

            Vector3 res = (line * t) + p0;

            return res;
        }

        public static float NearpointOnLine(Vector3 p0, Vector3 p1, Vector3 test, out Vector3 res)
        {
            Vector3 line = p1;

            test -= p0;
            line -= p0;

            float lenSq = line.sqrMagnitude;
            float t = 0.0f;

            if (lenSq > 0.0f)
            {
                float offset = Vector3.Dot(test, line);
                t = Mathf.Clamp01(offset / lenSq);
            }

            res = (line * t) + p0;

            return t;
        }

        public static Vector3 Barycentric(Vector3 test, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            Vector3 v0 = p1 - p0;
            Vector3 v1 = p2 - p0;
            Vector3 v2 = test - p0;

            float d00 = Vector3.Dot(v0, v0);
            float d01 = Vector3.Dot(v0, v1);
            float d11 = Vector3.Dot(v1, v1);
            float d20 = Vector3.Dot(v2, v0);
            float d21 = Vector3.Dot(v2, v1);
            float invDenom = 1.0f / (d00 * d11 - d01 * d01);

            float v = (d11 * d20 - d01 * d21) * invDenom;
            float w = (d00 * d21 - d01 * d20) * invDenom;
            float u = 1.0f - v - w;

            return new Vector3(u, v, w);
        }

        public static Vector3 Barycentric(Vector2 test, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            Vector2 v0 = p1 - p0;
            Vector2 v1 = p2 - p0;
            Vector2 v2 = test - p0;

            float d00 = Vector2.Dot(v0, v0);
            float d01 = Vector2.Dot(v0, v1);
            float d11 = Vector2.Dot(v1, v1);
            float d20 = Vector2.Dot(v2, v0);
            float d21 = Vector2.Dot(v2, v1);
            float invDenom = 1.0f / (d00 * d11 - d01 * d01);

            float v = (d11 * d20 - d01 * d21) * invDenom;
            float w = (d00 * d21 - d01 * d20) * invDenom;
            float u = 1.0f - v - w;

            return new Vector3(u, v, w);
        }

        public static bool IsPointInsideConvexPolygon(Vector3 point, params Vector3[] sortedPolygonVertices)
        {
            if ((sortedPolygonVertices?.Length ?? 0) < 3)
                return false;

            for (int i = 1; i < sortedPolygonVertices.Length - 1; i++)
            {
                if (IsPointInsideTriangle(point, sortedPolygonVertices[0], sortedPolygonVertices[i], sortedPolygonVertices[i + 1]))
                    return true;
            }

            return false;
        }

        public static bool IsPointInsideConvexPolygon(Vector3 point, List<Vector3> sortedPolygonVertices)
        {
            int vertexCount = sortedPolygonVertices.Count;
            if (vertexCount < 3)
            {
                return false;
            }

            for (int i = 1; i < vertexCount - 1; i++)
            {
                if (IsPointInsideTriangle(point, sortedPolygonVertices[0], sortedPolygonVertices[i], sortedPolygonVertices[i + 1]))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsPointInsideTriangle(Vector3 point, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            Vector3 barycentricCoords = Barycentric(point, p0, p1, p2);

            bool isInside = barycentricCoords.y >= 0.0f && barycentricCoords.y <= 1.0f &&
                barycentricCoords.z >= 0.0f && barycentricCoords.z <= 1.0f &&
                barycentricCoords.y + barycentricCoords.z <= 1.0f;

            return isInside;
        }

        public static bool IsPointInsideTriangleXZ(Vector3 point, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            point.y = 0;
            p0.y = 0;
            p1.y = 0;
            p2.y = 0;

            return IsPointInsideTriangle(point, p0, p1, p2);
        }

        public static Vector3 ClosesPointOnTriangle(Vector3[] triangle, Vector3 sourcePosition)
        {
            Vector3 edge0 = triangle[1] - triangle[0];
            Vector3 edge1 = triangle[2] - triangle[0];
            Vector3 v0 = triangle[0] - sourcePosition;

            float a = Vector3.Dot(edge0, edge0);
            float b = Vector3.Dot(edge0, edge1);
            float c = Vector3.Dot(edge1, edge1);
            float d = Vector3.Dot(edge0, v0);
            float e = Vector3.Dot(edge1, v0);

            float det = a * c - b * b;
            float s = b * e - c * d;
            float t = b * d - a * e;

            if (s + t < det)
            {
                if (s < 0)
                {
                    if (t < 0)
                    {
                        if (d < 0)
                        {
                            s = Mathf.Clamp(-d / a, 0, 1);
                            t = 0;
                        }
                        else
                        {
                            s = 0;
                            t = Mathf.Clamp(-e / c, 0, 1);
                        }
                    }
                    else
                    {
                        s = 0;
                        t = Mathf.Clamp(-e / c, 0, 1);
                    }
                }
                else if (t < 0)
                {
                    s = Mathf.Clamp(-d / a, 0, 1);
                    t = 0;
                }
                else
                {
                    float invDet = 1 / det;
                    s *= invDet;
                    t *= invDet;
                }
            }
            else
            {
                if (s < 0)
                {
                    float tmp0 = b + d;
                    float tmp1 = c + e;
                    if (tmp1 > tmp0)
                    {
                        float numer = tmp1 - tmp0;
                        float denom = a - 2 * b + c;
                        s = Mathf.Clamp(numer / denom, 0, 1);
                        t = 1 - s;
                    }
                    else
                    {
                        t = Mathf.Clamp(-e / c, 0, 1);
                        s = 0;
                    }
                }
                else if (t < 0)
                {
                    if (a + d > b + e)
                    {
                        float numer = c + e - b - d;
                        float denom = a - 2 * b + c;
                        s = Mathf.Clamp(numer / denom, 0, 1);
                        t = 1 - s;
                    }
                    else
                    {
                        s = Mathf.Clamp(-e / c, 0, 1);
                        t = 0;
                    }
                }
                else
                {
                    float numer = c + e - b - d;
                    float denom = a - 2 * b + c;
                    s = Mathf.Clamp(numer / denom, 0, 1);
                    t = 1 - s;
                }
            }
            return triangle[0] + s * edge0 + t * edge1;
        }

        public static float Frac(float value)
        {
            return value - Mathf.Floor(value);
        }

        public static float SolveMotionForDistance(float vel, float acc, float time)
        {
            //< solve s = ut + (at^2) / 2

            float timeSqr = time * time;

            return (vel * time) + ((acc * timeSqr) * 0.5f);
        }

        public static void SolveProjectileMotionForDistance(Vector3 velocity, float time, out float distanceXZ, out float deltaY)
        {
            // HorizontalDisplacement = launchSpeedHorizontal * ElapsedTime
            // VerticalDisplacement = launchSpeedVertical * elapsedTime - Utilities.HalfGravity * elapsedTime * elapsedTime;
            distanceXZ = VectorExtensions.MagnitudeXZ(velocity) * time;
            deltaY = velocity.y * time - HALF_GRAVITY * time * time;
        }

        public static bool SolveProjectileMotionForSpeed(float angleInDegrees, float distance, ref float res, float deltaY = 0.0f, float gravity = GRAVITY)
        {
            // ignoring drag

            // straight up
            if (angleInDegrees == 90.0f)
            {
                if (distance != 0.0f)
                {
                    return false;
                }
                if (deltaY <= 0.0f)
                {
                    res = 0.0f;
                    return true;
                }
                res = Mathf.Sqrt(DOUBLE_GRAVITY * deltaY);
                return true;
            }

            // http://www.ilectureonline.com/lectures/subject/PHYSICS/1/10/286
            res = distance * (1.0f / Mathf.Cos(angleInDegrees * Mathf.Deg2Rad)) * Mathf.Sqrt(-gravity / (2.0f * (deltaY - distance * Mathf.Tan(angleInDegrees * Mathf.Deg2Rad))));
            return !float.IsNaN(res);
        }

        public static bool SolveProjectileMotionForAngle(float speed, float distance, ref float resLow, ref float resHigh, float deltaY = 0.0f, float gravity = GRAVITY)
        {
            // ignoring drag
            // http://www.ilectureonline.com/lectures/subject/PHYSICS/1/10/287
            // quadratic equation:
            // [-0.5g * distance^2 * 1/v0^2] * tan^2(phi) + [distance] * tan(phi) + [(-0.5g * distance^2 * 1/v0^2) - deltaY] = 0
            //              a                                   b                                      c
            // substitute: x = tan(phi)
            float speedSquared = speed * speed;
            float distanceSquared = distance * distance;
            float a = -0.5f * gravity * distanceSquared * (1.0f / speedSquared);
            float b = distance;
            float c = a - deltaY;
            if (!SolveQuadratic(a, b, c, ref resLow, ref resHigh))
            {
                return false;
            }
            // reverse substitution: phi = atan(x)
            resLow = Mathf.Atan(resLow) * Mathf.Rad2Deg;
            resHigh = Mathf.Atan(resHigh) * Mathf.Rad2Deg;
            return true;
        }

        public static void SolveProjectileMotionForVelocityAndDuration(float targetDeltaY, float distanceXZ, float launchAngle,
            out float launchSpeedVertical, out float launchSpeedHorizontal, out float duration)
        {
            float absoluteDeltaY = Mathf.Abs(targetDeltaY);
            Debug.Assert(absoluteDeltaY > float.Epsilon || distanceXZ > float.Epsilon);

            // https://physics.stackexchange.com/questions/502753/projectile-motion-solving-for-initial-velocity
            float angleTan = Mathf.Tan(launchAngle * Mathf.Deg2Rad);
            float sqrDuration = 2.0f * ((absoluteDeltaY + distanceXZ * angleTan) / GRAVITY);
            // sqr duration cannot be negative as all values we use are positives, so no need to check for that

            duration = Mathf.Sqrt(sqrDuration);
            launchSpeedHorizontal = distanceXZ / duration;
            launchSpeedVertical = (targetDeltaY + (HALF_GRAVITY * sqrDuration)) / duration;
        }

        //------------------------------------------------------------------------
        //------------------------------------------------------------------------
        public static float CalcMinAngleDif(float startAng, float endAng)
        {
            while ((startAng < 0) || (endAng < 0))
            {
                startAng += TWO_PI;
                endAng += TWO_PI;
            }

            startAng = startAng % TWO_PI;
            endAng = endAng % TWO_PI;

            float up, down;

            if (endAng > startAng)
            {
                up = endAng - startAng;
                down = (endAng - TWO_PI) - startAng;
            }
            else
            {
                up = (endAng + TWO_PI) - startAng;
                down = endAng - startAng;
            }

            if (up < -down)
            {
                return up;
            }

            return down;
        }

        //------------------------------------------------------------------------
        //------------------------------------------------------------------------
        public static float CalcMinAngleDifferenceDegrees(float startAngleDegrees, float endAngleDegrees)
        {
            return CalcMinAngleDif(Mathf.Deg2Rad * startAngleDegrees, Mathf.Deg2Rad * endAngleDegrees) * Mathf.Rad2Deg;
        }

        //------------------------------------------------------------------------
        //------------------------------------------------------------------------
        public static void CalcAnglesFromDir(Vector3 source, ref float angY, ref float angX)
        {
            Vector3 dirXZ = new Vector3(source.x, 0.0f, source.z);
            Vector3 dirFull = source;

            dirXZ = dirXZ.normalized;
            dirFull = dirFull.normalized;

            angY = Mathf.Acos(dirXZ.z);
            angX = Mathf.Asin(dirFull.y);

            if (dirXZ.x < 0.0f)
            {
                angY = -angY;
            }
        }

        //------------------------------------------------------------------------
        //------------------------------------------------------------------------
        public static void CalcDirFromAngles(ref Vector3 dest, float angY, float angX)
        {
            dest.y = Mathf.Sin(angX);

            float xzLen = Mathf.Sqrt(1.0f - (dest.y * dest.y));

            dest.x = Mathf.Sin(angY);
            dest.z = Mathf.Cos(angY);

            dest.x *= xzLen;
            dest.z *= xzLen;

            dest = dest.normalized;
        }

        //------------------------------------------------------------------------
        //------------------------------------------------------------------------
        public static Vector3 CalcDirFromAngles(float angY, float angX)
        {
            Vector3 dest = Vector3.zero;

            dest.y = Mathf.Sin(angX);

            float xzLen = Mathf.Sqrt(1.0f - (dest.y * dest.y));

            dest.x = Mathf.Sin(angY);
            dest.z = Mathf.Cos(angY);

            dest.x *= xzLen;
            dest.z *= xzLen;

            dest = dest.normalized;
            return dest;
        }

        public static Vector3 ClosestPointOnLine(Vector3 point1, Vector3 point2, Vector3 testPoint)
        {
            Vector3 point1ToTestPoint = testPoint - point1;
            Vector3 normPoint1ToPoint2 = (point2 - point1).normalized;
            float lineLength = Vector3.Distance(point1, point2);

            return point1 + Mathf.Clamp(Vector3.Dot(point1ToTestPoint, normPoint1ToPoint2), 0.0f, lineLength) * normPoint1ToPoint2;
        }

        public static float DistanceToLineFromPoint(Vector3 point1, Vector3 point2, Vector3 testPoint)
        {
            Vector3 closestPointOnLine = ClosestPointOnLine(point1, point2, testPoint);
            return Vector3.Distance(testPoint, closestPointOnLine);
        }

        public static bool LinesIntersectsXZ(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
        {
            Vector3 cmp = new Vector3(p3.x - p1.x, 0, p3.z - p1.z);
            Vector3 r = new Vector3(p2.x - p1.x, 0, p2.z - p1.z);
            Vector3 s = new Vector3(p4.x - p3.x, 0, p4.z - p3.z);

            float cmpxr = cmp.x * r.z - cmp.z * r.x;
            float cmpxs = cmp.x * s.z - cmp.z * s.x;
            float rxs = r.x * s.z - r.z * s.x;

            if (cmpxr == 0f)
            {
                // Lines are collinear, and so intersect if they have any overlap
                return ((p3.x - p1.x < 0f) != (p3.x - p2.x < 0f))
                    || ((p3.z - p1.z < 0f) != (p3.z - p2.z < 0f));
            }

            if (rxs == 0f)
                return false; // Lines are parallel.

            float rxsr = 1f / rxs;
            float t = cmpxs * rxsr;
            float u = cmpxr * rxsr;

            return (t >= 0f) && (t <= 1f) && (u >= 0f) && (u <= 1f);
        }

        public static bool LineSegmentIntersection(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, out Vector3 intersection)
        {
            Vector3 lineVec3 = p3 - p1;
            Vector3 crossVec1and2 = Vector3.Cross(p2, p4);
            Vector3 crossVec3and2 = Vector3.Cross(lineVec3, p4);

            float planarFactor = Vector3.Dot(lineVec3, crossVec1and2);

            if (Mathf.Abs(planarFactor) < 0.0001f && crossVec1and2.sqrMagnitude > 0.0001f)
            {
                float s = Vector3.Dot(crossVec3and2, crossVec1and2) / crossVec1and2.sqrMagnitude;
                intersection = p1 + (p2 * s);
                return true;
            }
            else
            {
                intersection = Vector3.zero;
                return false;
            }
        }

        public static bool IsSphereOccludedByPlanet(Vector3 objectPos, float objectRadius, Vector3 planetPos, float planetRadius, Vector3 camPos)
        {
            Vector3 ac = objectPos - camPos;
            Vector3 bc = planetPos - camPos;
            Vector3 ba = planetPos - objectPos;

            float ia = 1.0f / ac.magnitude;
            float ib = 1.0f / bc.magnitude;

            float dot = Vector3.Dot(ac, bc);
            float k0 = dot * ia * ib;
            float k1 = objectRadius * ia;
            float k2 = planetRadius * ib;

            float k0Sq = k0 * k0;
            float k1Sq = k1 * k1;
            float k2Sq = k2 * k2;

            if (k0Sq + k1Sq + k2Sq + 2.0f * k0 * k1 * k2 - 1.0f < 0.0f) return false;
            else if (k0Sq + k1Sq + k2Sq - 2.0f * k0 * k1 * k2 - 1.0f < 0.0f) return false;
            else if (Vector3.Dot(ba, bc) > 0) return false;

            return true;
        }

        public static float GetFitness(Vector3 testPos, Vector3 sourceLocation, Vector3 sourceDirection, float limitAngle, float cosLimitAngle, float limitRange)
        {
            Vector3 line = testPos - sourceLocation;
            float distSq = line.sqrMagnitude;
            float limitRangeSq = limitRange * limitRange;
            if (distSq < limitRangeSq)
            {
                float angDot = Vector3.Dot(line.normalized, sourceDirection);

                if (angDot >= cosLimitAngle)
                {
                    float rangeFitness = 1 - (distSq / limitRangeSq);
                    float ang = Mathf.Acos(angDot);
                    float angleRatio = Mathf.Min(1.0f, ang / limitAngle);
                    float angleFitness = Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - (angleRatio * angleRatio)));
                    return angleFitness * rangeFitness;
                }
            }

            return 0.0f;
        }

        public static float GetFitness(Vector3 testPos, Transform source, float limitAngle, float cosLimitAngle, float limitRange)
        {
            return GetFitness(testPos, source.position, source.forward, limitAngle, cosLimitAngle, limitRange);
        }

        public static float MaxByMagnitude(float x, float y)
        {
            if (Mathf.Abs(x) > Mathf.Abs(y))
            {
                return x;
            }
            return y;
        }

        public static float SignExcludingZero(float val)
        {
            if (val > 0.0f)
            {
                return 1.0f;
            }
            else if (val < -0.0f)
            {
                return -1.0f;
            }

            return 0.0f;
        }

        public static float Sum(params float[] values)
        {
            if ((values?.Length ?? 0) == 0)
                return 0.0f;

            float total = 0.0f;
            for (int i = 0; i < values.Length; i++)
            {
                total += values[i];
            }

            return total;
        }

        public static float Sum(List<float> values)
        {
            if ((values?.Count ?? 0) == 0)
                return 0.0f;

            float total = 0.0f;
            for (int i = 0; i < values.Count; i++)
            {
                total += values[i];
            }

            return total;
        }

        public static Vector3 RandomVectorWithinAngleInDirection(Vector3 vector, float angleDegs)
        {
            uint timeout = 100;
            float cosTest = Mathf.Cos(angleDegs * Mathf.Deg2Rad);

            while (timeout > 0)
            {
                timeout--;
                Vector3 randomVector = UnityEngine.Random.insideUnitSphere;
                Vector3 crossVector = Vector3.Cross(vector, randomVector).normalized;

                float s = UnityEngine.Random.value;
                float r = UnityEngine.Random.value;

                float h = Mathf.Cos(angleDegs * Mathf.Deg2Rad);

                float phi = 2 * Mathf.PI * s;
                float z = h + (1 - h) * r;
                float sinT = Mathf.Sqrt(1 - z * z);
                float x = Mathf.Cos(phi) * sinT;
                float y = Mathf.Sin(phi) * sinT;

                Vector3 res = (randomVector * x) + (crossVector * y) + (vector * z);

                if (Vector3.Dot(res.normalized, vector.normalized) > cosTest)
                {
                    return res;
                }
            }
            return Vector3.zero;
        }

        public static float LimitMagnitude(float value, float limit)
        {
            return Mathf.Clamp(value, -limit, limit);
        }

        //------------------------------------------------------------------------
        //------------------For Hermite Spline Controller-------------------------
        //------------------------------------------------------------------------
        public static float GetQuatLength(Quaternion q)
        {
            return Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        }

        public static Quaternion GetQuatConjugate(Quaternion q)
        {
            return new Quaternion(-q.x, -q.y, -q.z, q.w);
        }

        /// <summary>
        /// Logarithm of a unit quaternion. The result is not necessary a unit quaternion.
        /// </summary>
        public static Quaternion GetQuatLog(Quaternion q)
        {
            Quaternion res = q;
            res.w = 0;

            if (Mathf.Abs(q.w) < 1.0f)
            {
                float theta = Mathf.Acos(q.w);
                float sin_theta = Mathf.Sin(theta);

                if (Mathf.Abs(sin_theta) > 0.0001)
                {
                    float coef = theta / sin_theta;
                    res.x = q.x * coef;
                    res.y = q.y * coef;
                    res.z = q.z * coef;
                }
            }

            return res;
        }

        public static Quaternion GetQuatExp(Quaternion q)
        {
            Quaternion res = q;

            float fAngle = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z);
            float fSin = Mathf.Sin(fAngle);

            res.w = Mathf.Cos(fAngle);

            if (Mathf.Abs(fSin) > 0.0001)
            {
                float coef = fSin / fAngle;
                res.x = coef * q.x;
                res.y = coef * q.y;
                res.z = coef * q.z;
            }

            return res;
        }

        public static float ExponentialEase(float t, float power)
        {
            if (t < 0.5f)
            {
                return Mathf.Pow(2.0f * t, power) * 0.5f;
            }

            return 1.0f - Mathf.Pow(2.0f * (1.0f - t), power) * 0.5f;
        }

        /// <summary>
        /// SQUAD Spherical Quadrangle interpolation [Shoe87]
        /// </summary>
        public static Quaternion GetQuatSquad(float t, Quaternion q0, Quaternion q1, Quaternion a0, Quaternion a1)
        {
            float slerpT = 2.0f * t * (1.0f - t);

            Quaternion slerpP = Slerp(q0, q1, t);
            Quaternion slerpQ = Slerp(a0, a1, t);

            return Slerp(slerpP, slerpQ, slerpT);
        }

        public static Quaternion GetSquadIntermediate(Quaternion q0, Quaternion q1, Quaternion q2)
        {
            Quaternion q1Inv = GetQuatConjugate(q1);
            Quaternion p0 = GetQuatLog(q1Inv * q0);
            Quaternion p2 = GetQuatLog(q1Inv * q2);
            Quaternion sum = new Quaternion(-0.25f * (p0.x + p2.x), -0.25f * (p0.y + p2.y), -0.25f * (p0.z + p2.z), -0.25f * (p0.w + p2.w));

            return q1 * GetQuatExp(sum);
        }

        /// <summary>
        /// Smooths the input parameter t.
        /// If less than k1 ir greater than k2, it uses a sin.
        /// Between k1 and k2 it uses linear interp.
        /// </summary>
        public static float Ease(float t, float k1, float k2)
        {
            float f; float s;

            f = k1 * 2 / Mathf.PI + k2 - k1 + (1.0f - k2) * 2 / Mathf.PI;

            if (t < k1)
            {
                s = k1 * (2 / Mathf.PI) * (Mathf.Sin((t / k1) * Mathf.PI / 2 - Mathf.PI / 2) + 1);
            }
            else
                if (t < k2)
            {
                s = (2 * k1 / Mathf.PI + t - k1);
            }
            else
            {
                s = 2 * k1 / Mathf.PI + k2 - k1 + ((1 - k2) * (2 / Mathf.PI)) * Mathf.Sin(((t - k2) / (1.0f - k2)) * Mathf.PI / 2);
            }

            return (s / f);
        }

        /// <summary>
        /// We need this because Quaternion.Slerp always uses the shortest arc.
        /// </summary>
        public static Quaternion Slerp(Quaternion p, Quaternion q, float t)
        {
            Quaternion ret;

            float fCos = Quaternion.Dot(p, q);

            if ((1.0f + fCos) > 0.00001)
            {
                float fCoeff0, fCoeff1;

                if ((1.0f - fCos) > 0.00001)
                {
                    float omega = Mathf.Acos(fCos);
                    float invSin = 1.0f / Mathf.Sin(omega);
                    fCoeff0 = Mathf.Sin((1.0f - t) * omega) * invSin;
                    fCoeff1 = Mathf.Sin(t * omega) * invSin;
                }
                else
                {
                    fCoeff0 = 1.0f - t;
                    fCoeff1 = t;
                }

                ret.x = fCoeff0 * p.x + fCoeff1 * q.x;
                ret.y = fCoeff0 * p.y + fCoeff1 * q.y;
                ret.z = fCoeff0 * p.z + fCoeff1 * q.z;
                ret.w = fCoeff0 * p.w + fCoeff1 * q.w;
            }
            else
            {
                float fCoeff0 = Mathf.Sin((1.0f - t) * Mathf.PI * 0.5f);
                float fCoeff1 = Mathf.Sin(t * Mathf.PI * 0.5f);

                ret.x = fCoeff0 * p.x - fCoeff1 * p.y;
                ret.y = fCoeff0 * p.y + fCoeff1 * p.x;
                ret.z = fCoeff0 * p.z - fCoeff1 * p.w;
                ret.w = p.z;
            }

            return ret;
        }

        public static void BoundingSphereOfPoints(List<Vector3> points, out Vector3 boundingSphereCentre, out float boundingSphereRadius)
        {
            //Calculate bounding sphere
            boundingSphereCentre = new Vector3();
            boundingSphereRadius = 0.0f;
            for (int i = 0, count = points.Count; i < count; ++i)
            {
                Vector3 sphereCentre = points[i];

                if (boundingSphereCentre == Vector3.zero)
                {
                    boundingSphereCentre = sphereCentre;
                }
                else
                {
                    float distBetweenCentres = (boundingSphereCentre - sphereCentre).magnitude;
                    Vector3 dirBetweenCentres = (sphereCentre - boundingSphereCentre).normalized;
                    float newRadius = (distBetweenCentres + boundingSphereRadius) / 2;
                    float radiusDifference = Mathf.Max(0.0f, newRadius - boundingSphereRadius);

                    if (newRadius > boundingSphereRadius)
                    {
                        boundingSphereRadius = newRadius;
                        boundingSphereCentre = boundingSphereCentre + dirBetweenCentres * radiusDifference;
                    }
                }
            }
        }

        public static void BoundingSphereOfPoints(Vector3[] points, out Vector3 boundingSphereCentre, out float boundingSphereRadius)
        {
            //Calculate bounding sphere
            boundingSphereCentre = new Vector3();
            boundingSphereRadius = 0.0f;
            for (int i = 0, count = points.Length; i < count; ++i)
            {
                Vector3 sphereCentre = points[i];

                if (boundingSphereCentre == Vector3.zero)
                {
                    boundingSphereCentre = sphereCentre;
                }
                else
                {
                    float distBetweenCentres = (boundingSphereCentre - sphereCentre).magnitude;
                    Vector3 dirBetweenCentres = (sphereCentre - boundingSphereCentre).normalized;
                    float newRadius = (distBetweenCentres + boundingSphereRadius) / 2;
                    float radiusDifference = Mathf.Max(0.0f, newRadius - boundingSphereRadius);

                    if (newRadius > boundingSphereRadius)
                    {
                        boundingSphereRadius = newRadius;
                        boundingSphereCentre = boundingSphereCentre + dirBetweenCentres * radiusDifference;
                    }
                }
            }
        }

        public static float MaxAngle(Vector3 pointFrom, List<Vector3> points)
        {
            float largestAngle = 0;

            //Two directions which are the furthest apart from each other
            Vector3 dir1 = new Vector3();
            Vector3 dir2 = new Vector3();

            int lim = points.Count;
            for (int i = 0; i < lim; ++i)
            {
                Vector3 dir = (pointFrom - points[i]).normalized;

                if (dir1 == Vector3.zero)
                {
                    dir1 = dir;
                }
                else if (dir2 == Vector3.zero)
                {
                    dir2 = dir;
                    largestAngle = Vector3.Angle(dir1, dir2);
                }
                else
                {
                    NewAngle(dir, ref largestAngle, ref dir1, ref dir2);
                }
            }

            return largestAngle;
        }

        private static void NewAngle(Vector3 newDir, ref float largestAngle, ref Vector3 dir1, ref Vector3 dir2)
        {
            float newAngle1 = Vector3.Angle(dir1, newDir);
            float newAngle2 = Vector3.Angle(dir2, newDir);

            //If the new direction has a greater angle between either of the current directions, replace the direction, resulting in the largest new angle
            if (newAngle1 > largestAngle || newAngle2 > largestAngle)
            {
                if (newAngle1 > newAngle2)
                {
                    dir2 = newDir;
                    largestAngle = newAngle1;
                }
                else
                {
                    dir1 = newDir;
                    largestAngle = newAngle2;
                }
            }
        }

        public static int RoundUp(int value, int segmentation)
        {
            return segmentation * ((value + (segmentation - 1)) / segmentation);
        }

        public static float RoundUp(float value, float segmentation)
        {
            return Mathf.Ceil(value / segmentation) * segmentation;
        }

        public static int RoundUpToInt(float value, int segmentation)
        {
            return Mathf.CeilToInt(value / (float)segmentation) * segmentation;
        }

        public static int RoundDown(int value, int segmentation)
        {
            return segmentation * (value / segmentation);
        }

        public static float RoundDown(float value, float segmentation)
        {
            return Mathf.Floor(value / segmentation) * segmentation;
        }

        public static int RoundDownToInt(float value, int segmentation)
        {
            return Mathf.FloorToInt(value / (float)segmentation) * segmentation;
        }

        public static float MeanAngle(float[] angles)
        {
            float yPart = 0.0f;
            float xPart = 0.0f;

            int numAngles = angles.Length;
            for (int i = 0; i < numAngles; i++)
            {
                xPart += Mathf.Cos(angles[i]);
                yPart += Mathf.Sin(angles[i]);
            }

            return Mathf.Atan2(yPart / numAngles, xPart / numAngles);
        }

        public static float WeightedAverageAngle(float[] angles, int[] weights)
        {
            float y_part = 0.0f;
            float x_part = 0.0f;

            int totalWeight = 0;

            int numAngles = angles.Length;
            for (int i = 0; i < numAngles; i++)
            {
                if (i > weights.Length)
                {
                    //Assume all other weights would have been 0, so return current result
                    break;
                }
                else
                {
                    totalWeight += weights[i];
                    x_part += Mathf.Cos(angles[i]) * weights[i];
                    y_part += Mathf.Sin(angles[i]) * weights[i];
                }
            }

            return Mathf.Atan2(y_part / totalWeight, x_part / totalWeight);
        }

        public static Vector3 ExpectMatch(Vector3 value, Vector3 test, GameObject context)
        {
            if (Mathf.Abs(value.magnitude - test.magnitude) > 0.001f)
            {
                Debug.LogWarning("Vector LengthTest Failed : Magnitude", context);
            }

            if (Vector3.Dot(value, test) < 0.999f)
            {
                Debug.LogWarning("Vector LengthTest Failed : Direction", context);
            }

            return value;
        }

        public static float ExpectMatch(float value, float test, GameObject context)
        {
            if (Mathf.Abs(value - test) > 0.001f)
            {
                Debug.LogWarning("Float Match Failed : Magnitude", context);
            }

            return value;
        }

        public static Vector3 WeightedAverageVector(Vector3[] vectors, int[] weights)
        {
            Vector3 vector = Vector3.zero;
            int totalWeight = 0;

            int numVectors = vectors.Length;
            for (int i = 0; i < numVectors; i++)
            {
                if (i > weights.Length)
                {
                    //Assume all other weights would have been 0, so return current result
                    break;
                }
                else
                {
                    totalWeight += weights[i];
                    vector += vectors[i] * weights[i];
                }
            }

            return vector / totalWeight;
        }

        public static int IntLog2(int v)
        {
            int r;
            int shift;

            r = ((v > 0xFFFF) ? 1 : 0) << 4;
            v >>= r;
            shift = ((v > 0xFF) ? 1 : 0) << 3;
            v >>= shift;
            r |= shift;
            shift = ((v > 0xF) ? 1 : 0) << 2;
            v >>= shift;
            r |= shift;
            shift = ((v > 0x3) ? 1 : 0) << 1;
            v >>= shift;
            r |= shift;

            r |= (v >> 1);

            return r;
        }

        public static int NextPowerOfTwo(int v)
        {
            if (v < 0) return 1;
            if (v == 0) return 1;
            if (v == (v & (~v + 1))) return v;

            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;

            return v;
        }

        public static bool IsPowerOfTwo(int v)
        {
            if (v < 0) return false;
            return v == (v & (~v + 1));
        }

        public static bool IsPowerOfTwo(uint v)
        {
            return v == (v & (~v + 1));
        }

        public static uint BinaryToGray(uint v)
        {
            return (v >> 1) ^ v;
        }

        public static uint GrayToBinary(uint v)
        {
            v ^= (v >> 16);
            v ^= (v >> 8);
            v ^= (v >> 4);
            v ^= (v >> 2);
            v ^= (v >> 1);
            return v;
        }

        public static int BinaryToGray(int v)
        {
            return (v >> 1) ^ v;
        }

        public static int GrayToBinary(int v)
        {
            v ^= (v >> 16);
            v ^= (v >> 8);
            v ^= (v >> 4);
            v ^= (v >> 2);
            v ^= (v >> 1);
            return v;
        }

        public static int IntPow(int x, int pow)
        {
            int ret = 1;
            while (pow != 0)
            {
                if ((pow & 1) == 1)
                {
                    ret *= x;
                }
                x *= x;
                pow >>= 1;
            }
            return ret;
        }

        /// <summary>Quadratic Bezier curve from <paramref name="a"/> to <paramref name="c"/> with control point <paramref name="b"/>.</summary>
        /// <returns>CheckpointPosition on curve at Time <paramref name="t"/>.</returns>
        public static Vector3 QuadraticBezier(float t, Vector3 a, Vector3 b, Vector3 c)
        {
            // Quadratic Bezier curve: (1-t)^2a + 2t(1-t)b + t^2c
            float oneMinusT = 1f - t;
            return (a * oneMinusT * oneMinusT) + (2f * b * t * oneMinusT) + (c * t * t);
        }

        public static Vector3 CubicBezier(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            float u = 1.0f - t;
            float uu = u * u;
            float tt = t * t;

            Vector3 p = uu * u * p0;
            p += 3 * uu * t * p1;
            p += 3 * u * tt * p2;
            p += tt * t * p3;
            return p;
        }

        public static int CycleFence(int value, int min, int max)
        {
            Debug.Assert(min <= max);
            if (min < max)
            {
                int lim = max - min;
                int offset = (value - min) % lim;
                if (offset >= 0)
                    return min + offset;
                else
                    return max + offset;
            }
            return min;
        }

        public static float CycleFence(float value, float min, float max)
        {
            Debug.Assert(min <= max);
            if (min < max)
            {
                float lim = max - min;
                float offset = (value - min) % lim;
                if (offset >= 0)
                    return min + offset;
                else
                    return max + offset;
            }
            return min;
        }

        public static Vector3 Position(this Matrix4x4 m)
        {
            return m.GetColumn(3);
        }

        public static void DecomposeToTRS(this Matrix4x4 m, out Vector3 pos, out Quaternion quat, out Vector3 scale)
        {
            pos = m.GetColumn(3);
            quat = m.rotation;
            scale = m.lossyScale;
        }

        /// <summary>
        /// This gives you a matrix composed from the local position, rotation, and scale.
        /// this differs from the Transfrom.worldtoLocalMatrix which is equivelent to Transfrom.localToWorldMatrix.ineverse
        /// </summary>
        /// <param name="t">the matrix to use</param>
        /// <returns>a local space matrix</returns>
        public static Matrix4x4 ToLocalMatrix(this Transform t)
        {
            return Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
        }

        public static Matrix4x4 LocalToWorldNormalised(Transform transform)
        {
            Matrix4x4 result = transform.localToWorldMatrix;
            Vector3 tmp;
            float mag;

            tmp = new Vector3(result.m00, result.m10, result.m20);
            mag = tmp.magnitude;
            result.m00 = tmp.x / mag;
            result.m10 = tmp.y / mag;
            result.m20 = tmp.z / mag;
            tmp = new Vector3(result.m01, result.m11, result.m21);
            mag = tmp.magnitude;
            result.m01 = tmp.x / mag;
            result.m11 = tmp.y / mag;
            result.m21 = tmp.z / mag;
            tmp = new Vector3(result.m02, result.m12, result.m22);
            mag = tmp.magnitude;
            result.m02 = tmp.x / mag;
            result.m12 = tmp.y / mag;
            result.m22 = tmp.z / mag;

            return result;
        }

        public static bool SphereSphereCollision(Vector3 center1, float radius1, Vector3 center2, float radius2, out Vector3 outCollisionPoint)
        {
            outCollisionPoint = center1;

            Vector3 distance = center1 - center2;
            float length = distance.magnitude;
            float radii = radius1 + radius2;

            if (length <= radii)
            {
                outCollisionPoint = center1 + -distance * radius1 / radii;
                return true;
            }

            return false;
        }

        public static bool DynamicSphereSphereCollision(Vector3 center1, float radius1, Vector3 velocity1, Vector3 center2, float radius2, Vector3 velocity2, out float outCollisionTime, out Vector3 outCollisionPoint)
        {
            outCollisionTime = 0.0f;
            outCollisionPoint = center1;

            Vector3 s = center1 - center2;
            Vector3 v = velocity1 - velocity2;
            float r = radius1 + radius2;

            float c1 = Vector3.SqrMagnitude(s) - r * r;
            float b1 = Vector3.Dot(v, s);
            if (c1 > 0.0f && b1 > 0.0f)
            {
                return false;
            }

            float a1 = Vector3.SqrMagnitude(v);
            if (a1 < 0.00001f)
            {
                return false;
            }

            float d1 = b1 * b1 - a1 * c1;
            if (d1 < 0.0)
            {
                return false;
            }

            outCollisionTime = (-b1 - Mathf.Sqrt(d1)) / a1;
            outCollisionPoint = center1 + outCollisionTime * s;

            return true;
        }

        public static float RemapValue(float value, float mappedLow, float mappedHigh, float originalLow, float originalHigh)
        {
            float slope = 1.0f * (mappedHigh - mappedLow) / (originalHigh - originalLow);
            float output = mappedLow + (slope * (value - originalLow));
            return output;
        }

        public static Vector3 RemapValue(Vector3 value, float mappedLow, float mappedHigh, float originalLow, float originalHigh)
        {
            return RemapValue(value, Vector3.one * mappedLow, Vector3.one * mappedHigh, Vector3.one * originalLow, Vector3.one * originalHigh);
        }

        public static Vector3 RemapValue(Vector3 value, Vector3 mappedLow, Vector3 mappedHigh, Vector3 originalLow, Vector3 originalHigh)
        {
            return new Vector3(RemapValue(value.x, mappedLow.x, mappedHigh.x, originalLow.x, originalHigh.x),
                RemapValue(value.y, mappedLow.y, mappedHigh.y, originalLow.y, originalHigh.y),
                RemapValue(value.z, mappedLow.z, mappedHigh.z, originalLow.z, originalHigh.z));
        }

        public static bool Consume(this ref float accumulator, float amount)
        {
            if (accumulator >= amount)
            {
                accumulator -= amount;
                return true;
            }

            return false;
        }

        public static bool AlmostEquals(this float aTarget, float aSecond, float aFloatDiff = float.Epsilon)
        {
            return Mathf.Abs(aTarget - aSecond) < aFloatDiff;
        }

        public static float ClampDegrees(float angle)
        {
            return Mathf.Clamp(angle, 0, MAX_ANGLE_DEGREES);
        }

        public static int CalculateSliceIndex(float angle, float offset, float sliceSize)
        {
            // The slices cross the 360/0 boundary so offset it.
            angle += offset;
            if (angle > 360f)
                angle -= 360f;

            // Clamp angle as there's an edge case due to floating point math
            return (int)(ClampDegrees(angle) / sliceSize);
        }

        /// <summary>
        /// Maps a 32-bit int value index >= 0 to an alternating series of 
        /// incrementing positive and negative numbers, starting with zero.
        /// 
        ///   index  result
        ///     0       0
        ///     1      -1
        ///     2       1
        ///     3      -2
        ///     4       2
        ///    
        /// </summary>
        public static int ZigZag(int index)
        {
            Debug.Assert(index >= 0);
            return (index >> 1) ^ (-(index & 1));
        }

        public static float Convert180Degrees(float val)
        {
            float v = val % 360f;
            if (v < -180)
                v += 360f;
            if (v > 180)
                v -= 360f;

            return v;
        }

        /// <summary>
        /// Much like Mathf.Min, but returns the smallest number regardless of sign
        /// </summary>
        public static float SmallestAbs(float a, float b)
        {
            return Mathf.Abs(a) < Mathf.Abs(b) ? a : b;
        }

        public static float SmoothDampAngleSafe(float current, float target, ref float currentVelocity, float smoothTime, float deltaTime)
        {
            if (deltaTime >= Mathf.Epsilon)
            {
                return Mathf.SmoothDampAngle(current, target, ref currentVelocity, smoothTime);
            }

            return current;
        }

        public static float SmoothDampSafe(float current, float target, ref float currentVelocity, float smoothTime, float deltaTime)
        {
            if (deltaTime >= Mathf.Epsilon)
            {
                return Mathf.SmoothDamp(current, target, ref currentVelocity, smoothTime);
            }
            return current;
        }

        public static bool QuaternionsAreExactlyEqual(Quaternion a, Quaternion b)
        {
            return (a.x == b.x) && (a.y == b.y) && (a.z == b.z) && (a.w == b.w);
        }
    }
}

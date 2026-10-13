using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoTweenTimeline.Runtime.Block.Transform {
    public enum PathKind { Linear, CatmullRom }

    /// <summary>Evaluates a point along a path at constant speed (arc-length parameterised).</summary>
    public class PathSampler {
        private const int StepsPerSegment = 16;

        private readonly Vector3[] _dense;
        private readonly float[] _cumulative;
        private readonly float _total;

        public PathSampler(Vector3[] points, PathKind kind) {
            var dense = new List<Vector3>();
            if (kind == PathKind.CatmullRom && points.Length > 2) {
                for (var i = 0; i < points.Length - 1; i++) {
                    var p0 = points[Mathf.Max(i - 1, 0)];
                    var p1 = points[i];
                    var p2 = points[i + 1];
                    var p3 = points[Mathf.Min(i + 2, points.Length - 1)];
                    for (var s = 0; s < StepsPerSegment; s++) dense.Add(CatmullRom(p0, p1, p2, p3, s / (float)StepsPerSegment));
                }
                dense.Add(points[points.Length - 1]);
            }
            else {
                dense.AddRange(points);
            }

            _dense = dense.ToArray();
            _cumulative = new float[_dense.Length];
            for (var i = 1; i < _dense.Length; i++) _cumulative[i] = _cumulative[i - 1] + Vector3.Distance(_dense[i - 1], _dense[i]);
            _total = _cumulative[_cumulative.Length - 1];
        }

        public Vector3 Evaluate(float t) {
            if (_total <= 0f) return _dense[0];

            var distance = Mathf.Clamp01(t) * _total;
            var index = Array.BinarySearch(_cumulative, distance);
            if (index < 0) index = ~index;
            index = Mathf.Clamp(index, 1, _cumulative.Length - 1);

            var segment = _cumulative[index] - _cumulative[index - 1];
            var u = segment > 0f ? (distance - _cumulative[index - 1]) / segment : 0f;
            return Vector3.LerpUnclamped(_dense[index - 1], _dense[index], u);
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u) {
            var u2 = u * u;
            var u3 = u2 * u;
            return 0.5f * (2f * p1
                           + (-p0 + p2) * u
                           + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2
                           + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }
    }
}

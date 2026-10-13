using System;
using DG.Tweening;
using UnityEngine;
using UTransform = UnityEngine.Transform; // the namespace 'Transform' would shadow the Unity type

namespace DoTweenTimeline.Runtime.Block.Transform {

    #region Vector3 blocks (start value -> end value)

    [Serializable]
    public abstract class Vector3TransformBlock : TweenBlock {
        public Vector3 startValue;
        public Vector3 endValue;

        protected abstract void Apply(UTransform target, Vector3 value);

        public override Tween GenerateTween() {
            if (!TryGetTarget(out UTransform target)) return null;
            return Finish(DOTween.To(() => startValue, v => Apply(target, v), endValue, duration));
        }
    }

    [Serializable]
    public class DoMoveBlock : Vector3TransformBlock {
        protected override void Apply(UTransform target, Vector3 value) => target.position = value;
    }

    [Serializable]
    public class DoLocalMoveBlock : Vector3TransformBlock {
        protected override void Apply(UTransform target, Vector3 value) => target.localPosition = value;
    }

    [Serializable]
    public class DoRotateBlock : Vector3TransformBlock {
        protected override void Apply(UTransform target, Vector3 value) => target.eulerAngles = value;
    }

    [Serializable]
    public class DoLocalRotateBlock : Vector3TransformBlock {
        protected override void Apply(UTransform target, Vector3 value) => target.localEulerAngles = value;
    }

    [Serializable]
    public class DoScaleBlock : Vector3TransformBlock {
        public DoScaleBlock() {
            startValue = Vector3.one;
            endValue = Vector3.one;
        }

        protected override void Apply(UTransform target, Vector3 value) => target.localScale = value;
    }

    #endregion

    #region Single-axis blocks

    [Serializable]
    public abstract class FloatTransformBlock : TweenBlock {
        public float startValue;
        public float endValue;

        protected FloatTransformBlock(float defaultValue = 0f) {
            startValue = defaultValue;
            endValue = defaultValue;
        }

        protected abstract void Apply(UTransform target, float value);

        public override Tween GenerateTween() {
            if (!TryGetTarget(out UTransform target)) return null;
            return Finish(DOTween.To(() => startValue, v => Apply(target, v), endValue, duration));
        }
    }

    [Serializable]
    public class DoMoveXBlock : FloatTransformBlock {
        protected override void Apply(UTransform target, float value) {
            var p = target.position;
            p.x = value;
            target.position = p;
        }
    }

    [Serializable]
    public class DoMoveYBlock : FloatTransformBlock {
        protected override void Apply(UTransform target, float value) {
            var p = target.position;
            p.y = value;
            target.position = p;
        }
    }

    [Serializable]
    public class DoMoveZBlock : FloatTransformBlock {
        protected override void Apply(UTransform target, float value) {
            var p = target.position;
            p.z = value;
            target.position = p;
        }
    }

    [Serializable]
    public class DoScaleXBlock : FloatTransformBlock {
        public DoScaleXBlock() : base(1f) { }

        protected override void Apply(UTransform target, float value) {
            var s = target.localScale;
            s.x = value;
            target.localScale = s;
        }
    }

    [Serializable]
    public class DoScaleYBlock : FloatTransformBlock {
        public DoScaleYBlock() : base(1f) { }

        protected override void Apply(UTransform target, float value) {
            var s = target.localScale;
            s.y = value;
            target.localScale = s;
        }
    }

    [Serializable]
    public class DoScaleZBlock : FloatTransformBlock {
        public DoScaleZBlock() : base(1f) { }

        protected override void Apply(UTransform target, float value) {
            var s = target.localScale;
            s.z = value;
            target.localScale = s;
        }
    }

    #endregion

    #region Quaternion rotation (shortest path, euler fields in the inspector)

    [Serializable]
    public abstract class QuaternionTransformBlock : TweenBlock {
        public Vector3 startEuler;
        public Vector3 endEuler;

        protected abstract void Apply(UTransform target, Quaternion value);

        public override Tween GenerateTween() {
            if (!TryGetTarget(out UTransform target)) return null;
            var from = Quaternion.Euler(startEuler);
            var to = Quaternion.Euler(endEuler);
            return Finish(Normalized(f => Apply(target, Quaternion.SlerpUnclamped(from, to, f)), duration));
        }
    }

    [Serializable]
    public class DoRotateQuaternionBlock : QuaternionTransformBlock {
        protected override void Apply(UTransform target, Quaternion value) => target.rotation = value;
    }

    [Serializable]
    public class DoLocalRotateQuaternionBlock : QuaternionTransformBlock {
        protected override void Apply(UTransform target, Quaternion value) => target.localRotation = value;
    }

    #endregion

    #region Look at

    /// <summary>Turns from a start rotation toward a Transform (or a world point when no Transform is set).</summary>
    [Serializable]
    public class DoLookAtBlock : TweenBlock {
        public Vector3 startEuler;
        public UTransform lookTarget;
        public Vector3 lookPoint;
        public Vector3 worldUp = Vector3.up;

        public override Tween GenerateTween() {
            if (!TryGetTarget(out UTransform target)) return null;
            var from = Quaternion.Euler(startEuler);
            return Finish(Normalized(f => {
                var point = lookTarget != null ? lookTarget.position : lookPoint;
                var direction = point - target.position;
                var to = direction.sqrMagnitude > 0f ? Quaternion.LookRotation(direction, worldUp) : from;
                target.rotation = Quaternion.SlerpUnclamped(from, to, f);
            }, duration));
        }
    }

    #endregion

    #region Jump

    [Serializable]
    public abstract class JumpTransformBlock : TweenBlock {
        public Vector3 startValue;
        public Vector3 endValue;
        public float jumpPower = 1f;
        public int numJumps = 1;

        protected abstract void Apply(UTransform target, Vector3 value);

        public override Tween GenerateTween() {
            if (!TryGetTarget(out UTransform target)) return null;
            var jumps = Mathf.Max(1, numJumps);
            return Finish(Normalized(f => {
                var segment = f * jumps;
                var arc = Mathf.Repeat(segment, 1f); // 0 at each landing, so the last jump ends on the ground
                var height = jumpPower * 4f * arc * (1f - arc);
                Apply(target, Vector3.LerpUnclamped(startValue, endValue, f) + Vector3.up * height);
            }, duration));
        }
    }

    [Serializable]
    public class DoJumpBlock : JumpTransformBlock {
        protected override void Apply(UTransform target, Vector3 value) => target.position = value;
    }

    [Serializable]
    public class DoLocalJumpBlock : JumpTransformBlock {
        protected override void Apply(UTransform target, Vector3 value) => target.localPosition = value;
    }

    #endregion

    #region Path

    [Serializable]
    public abstract class PathTransformBlock : TweenBlock {
        public Vector3[] points = { Vector3.zero, Vector3.right };
        public PathKind pathKind = PathKind.CatmullRom;

        protected abstract void Apply(UTransform target, Vector3 value);

        public override Tween GenerateTween() {
            if (!TryGetTarget(out UTransform target)) return null;
            if (points == null || points.Length < 2) {
                ThrowError($"{GetType().Name} '{name}' needs at least 2 path points");
                return null;
            }
            var sampler = new PathSampler(points, pathKind);
            return Finish(Normalized(f => Apply(target, sampler.Evaluate(f)), duration));
        }
    }

    [Serializable]
    public class DoPathBlock : PathTransformBlock {
        protected override void Apply(UTransform target, Vector3 value) => target.position = value;
    }

    [Serializable]
    public class DoLocalPathBlock : PathTransformBlock {
        protected override void Apply(UTransform target, Vector3 value) => target.localPosition = value;
    }

    #endregion

    #region Spiral

    /// <summary>Spirals outward from a centre: the radius grows from 0, and the object travels 'depth' along the axis.</summary>
    [Serializable]
    public class DoSpiralBlock : TweenBlock {
        public Vector3 center;
        public Vector3 axis = Vector3.forward;
        public float radius = 1f;
        public float turns = 2f;
        public float depth = 1f;
        public bool local;

        public override Tween GenerateTween() {
            if (!TryGetTarget(out UTransform target)) return null;
            if (axis.sqrMagnitude <= 0f) {
                ThrowError($"{nameof(DoSpiralBlock)} '{name}' needs a non-zero axis");
                return null;
            }

            var axisN = axis.normalized;
            var reference = Mathf.Abs(Vector3.Dot(axisN, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;
            var perpendicular = Vector3.Cross(axisN, reference).normalized;

            return Finish(Normalized(f => {
                var angle = 360f * turns * f;
                var offset = Quaternion.AngleAxis(angle, axisN) * perpendicular * (radius * f) + axisN * (depth * f);
                var position = center + offset;
                if (local) target.localPosition = position;
                else target.position = position;
            }, duration));
        }
    }

    #endregion
}

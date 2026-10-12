using System;
using DG.Tweening;
using UnityEngine;

namespace DoTweenTimeline.Runtime.Block.Transform {
    
    public class DoMoveBlock : TweenBlock {
        public override Tween GenerateTween() {
            return null;
        }
    }
    // public class DoMoveXBlock : TweenBlock { }
    // public class DoMoveYBlock : TweenBlock { }
    // public class DoMoveZBlock : TweenBlock { }
    
    [Serializable]
     public class DoLocalMoveBlock : TweenBlock {
         public Vector3 startPosition;
         public Vector3 endPosition;

         public override Tween GenerateTween() {
             if (gameObject == null) {
                 ThrowError($"{nameof(DoLocalMoveBlock)} '{name}' has no GameObject");
                 return null;
             }
             var rect = gameObject.GetComponent<UnityEngine.Transform>();
             if (!rect) {
                 return null;
             }
             rect.localPosition = startPosition;
             return rect.DOLocalMove(endPosition, duration).SetEase(easeType) ;
                 //? rect.DOLocalMove(endPosition, duration).SetEase(curve)
         }
     }
     
    //
    // public class DoRotateBlock : TweenBlock { }
    public class DoLocalRotateBlock : TweenBlock {
        public override Tween GenerateTween() {
            return null;
        }
    }
    // public class DoRotateQuaternionBlock : TweenBlock { }
    // public class DoLocalRotateQuaternionBlock : TweenBlock { }
    //
    // public class DoScaleBlock : TweenBlock { }
    // public class DoScaleXBlock : TweenBlock { }
    // public class DoScaleYBlock : TweenBlock { }
    // public class DoScaleZBlock : TweenBlock { }
    //
    // public class DoLookAtBlock : TweenBlock { }
    //
    // public class DoJumpBlock : TweenBlock { }
    // public class DoLocalJumpBlock : TweenBlock { }
    //
    // public class DoPathBlock : TweenBlock { }
    // public class DoLocalPathBlock : TweenBlock { }
    //
    // public class DoSpiralBlock : TweenBlock { }
    
}
using System.Collections.Generic;
using UnityEngine;

namespace DoTweenTimeline.DoTweenTimeline.Scripts.Editor {
    public class ColorRegistry {

        public enum ColorType {
            Primary,
            Secondary,
            Tertiary,
        }
        
        public static Dictionary<ColorType, Color> ColorPool =  new () {
            {ColorType.Primary, Color.deepSkyBlue},
            {ColorType.Secondary, Color.green},
            {ColorType.Tertiary, Color.red},
        };
        
    }
}
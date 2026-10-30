using System.Collections.Generic;
using UnityEngine;

namespace MercurioIdle.Construction
{
    public static class ProbeFactoryArt
    {
        public const float WorldWidth = 3.2f;
        public const float WorldHeight = 3f;
        public const float DoorCenter = 0.5f;

        private static Sprite probeSprite;

        public static Sprite GetSprite()
        {
            if (probeSprite != null) return probeSprite;

            var shapes = new List<ShapeRaster.Shape>
            {
                ShapeRaster.Polygon(new[] { new Vector2(32f, 0f), new Vector2(39f, 16f), new Vector2(25f, 16f) }, ShapeRaster.Gray(242)),
                ShapeRaster.Rounded(29f, 14f, 6f, 10f, 0f, ShapeRaster.Gray(122)),
                ShapeRaster.Dome(32f, 26f, 30f, 18f, ShapeRaster.Gray(242)),
                ShapeRaster.Rounded(0f, 24f, 64f, 36f, 3f, ShapeRaster.Gray(181)),
                ShapeRaster.Rounded(20f, 48f, 24f, 12f, 0f, ShapeRaster.Gray(11)),
                ShapeRaster.Dome(32f, 48f, 12f, 12f, ShapeRaster.Gray(11))
            };

            probeSprite = ShapeRaster.Render(shapes, 64f, 60f, WorldWidth);
            return probeSprite;
        }
    }
}

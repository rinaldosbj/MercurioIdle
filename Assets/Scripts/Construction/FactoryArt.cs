using System.Collections.Generic;
using UnityEngine;

namespace MercurioIdle.Construction
{
    public static class FactoryArt
    {
        public const float WorldWidth = 3.6f;
        public const float WorldHeight = 2.2f;
        public const float DoorCenter = 15f / 72f;

        private static Sprite factorySprite;

        public static Sprite GetFactorySprite()
        {
            if (factorySprite != null) return factorySprite;

            var shapes = new List<ShapeRaster.Shape>
            {
                ShapeRaster.Rounded(54f, 0f, 10f, 22f, 2f, ShapeRaster.Gray(122)),
                ShapeRaster.Polygon(new[]
                {
                    new Vector2(0f, 18f), new Vector2(16f, 6f), new Vector2(16f, 18f), new Vector2(32f, 6f),
                    new Vector2(32f, 18f), new Vector2(48f, 6f), new Vector2(48f, 18f)
                }, ShapeRaster.Gray(181)),
                ShapeRaster.Rounded(0f, 18f, 72f, 26f, 3f, ShapeRaster.Gray(242)),
                ShapeRaster.Rounded(8f, 28f, 14f, 16f, 2f, ShapeRaster.Gray(11)),
                ShapeRaster.Rounded(30f, 26f, 10f, 6f, 1.5f, ShapeRaster.Gray(74)),
                ShapeRaster.Rounded(46f, 26f, 10f, 6f, 1.5f, ShapeRaster.Gray(74))
            };

            factorySprite = ShapeRaster.Render(shapes, 72f, 44f, WorldWidth);
            return factorySprite;
        }
    }
}

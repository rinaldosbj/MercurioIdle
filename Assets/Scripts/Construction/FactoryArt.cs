using System.Collections.Generic;
using UnityEngine;

namespace MercurioIdle.Construction
{
    public static class FactoryArt
    {
        public const float WorldWidth = 3.6f;
        public const float WorldHeight = 2.2f;
        public const float DoorCenter = 15f / 72f;

        private const float ViewWidth = 72f;
        private const float ViewHeight = 44f;
        private const int PixelsPerViewUnit = 5;
        private const int Samples = 2;

        private struct Shape
        {
            public Vector2[] Points;
            public float X;
            public float Y;
            public float Width;
            public float Height;
            public float Radius;
            public Color Color;
        }

        private static Sprite factorySprite;
        private static Sprite squareSprite;

        public static Sprite GetFactorySprite()
        {
            if (factorySprite != null) return factorySprite;

            int width = (int)ViewWidth * PixelsPerViewUnit;
            int height = (int)ViewHeight * PixelsPerViewUnit;
            var shapes = BuildShapes();

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[width * height];
            float total = Samples * Samples;

            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    float r = 0f;
                    float g = 0f;
                    float b = 0f;
                    float a = 0f;

                    for (int sy = 0; sy < Samples; sy++)
                    {
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            float x = (px + (sx + 0.5f) / Samples) / PixelsPerViewUnit;
                            float y = ViewHeight - (py + (sy + 0.5f) / Samples) / PixelsPerViewUnit;

                            for (int k = shapes.Count - 1; k >= 0; k--)
                            {
                                if (!Contains(shapes[k], x, y)) continue;
                                r += shapes[k].Color.r;
                                g += shapes[k].Color.g;
                                b += shapes[k].Color.b;
                                a += 1f;
                                break;
                            }
                        }
                    }

                    pixels[py * width + px] = a > 0f ? new Color(r / a, g / a, b / a, a / total) : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            factorySprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), width / WorldWidth);
            return factorySprite;
        }

        public static Sprite GetSquareSprite()
        {
            if (squareSprite != null) return squareSprite;

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            squareSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return squareSprite;
        }

        private static List<Shape> BuildShapes()
        {
            return new List<Shape>
            {
                Rounded(54f, 0f, 10f, 22f, 2f, Gray(122)),
                new Shape
                {
                    Points = new[]
                    {
                        new Vector2(0f, 18f), new Vector2(16f, 6f), new Vector2(16f, 18f), new Vector2(32f, 6f),
                        new Vector2(32f, 18f), new Vector2(48f, 6f), new Vector2(48f, 18f)
                    },
                    Color = Gray(181)
                },
                Rounded(0f, 18f, 72f, 26f, 3f, Gray(242)),
                Rounded(8f, 28f, 14f, 16f, 2f, Gray(11)),
                Rounded(30f, 26f, 10f, 6f, 1.5f, Gray(74)),
                Rounded(46f, 26f, 10f, 6f, 1.5f, Gray(74))
            };
        }

        private static Shape Rounded(float x, float y, float width, float height, float radius, Color color)
        {
            return new Shape { X = x, Y = y, Width = width, Height = height, Radius = radius, Color = color };
        }

        private static Color Gray(int value)
        {
            float v = value / 255f;
            return new Color(v, v, v, 1f);
        }

        private static bool Contains(Shape shape, float x, float y)
        {
            if (shape.Points != null) return PolygonContains(shape.Points, x, y);

            if (x < shape.X || x > shape.X + shape.Width || y < shape.Y || y > shape.Y + shape.Height) return false;

            float dx = Mathf.Max(Mathf.Max(shape.X + shape.Radius - x, 0f), x - (shape.X + shape.Width - shape.Radius));
            float dy = Mathf.Max(Mathf.Max(shape.Y + shape.Radius - y, 0f), y - (shape.Y + shape.Height - shape.Radius));
            return dx * dx + dy * dy <= shape.Radius * shape.Radius;
        }

        private static bool PolygonContains(Vector2[] points, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }

            return inside;
        }
    }
}

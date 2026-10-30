using System.Collections.Generic;
using UnityEngine;

namespace MercurioIdle.Construction
{
    public static class ShapeRaster
    {
        public enum ShapeKind { RoundedRect, Polygon, Dome }

        public struct Shape
        {
            public ShapeKind Kind;
            public Vector2[] Points;
            public float X;
            public float Y;
            public float Width;
            public float Height;
            public float Radius;
            public Color Color;
        }

        private static Sprite squareSprite;

        public static Shape Rounded(float x, float y, float width, float height, float radius, Color color)
        {
            return new Shape { Kind = ShapeKind.RoundedRect, X = x, Y = y, Width = width, Height = height, Radius = radius, Color = color };
        }

        public static Shape Polygon(Vector2[] points, Color color)
        {
            return new Shape { Kind = ShapeKind.Polygon, Points = points, Color = color };
        }

        public static Shape Dome(float centerX, float centerY, float radiusX, float radiusY, Color color)
        {
            return new Shape { Kind = ShapeKind.Dome, X = centerX, Y = centerY, Width = radiusX, Height = radiusY, Color = color };
        }

        public static Color Gray(int value)
        {
            float v = value / 255f;
            return new Color(v, v, v, 1f);
        }

        public static Sprite Render(IList<Shape> shapes, float viewWidth, float viewHeight, float worldWidth, int pixelsPerUnit = 5, int samples = 2)
        {
            int width = Mathf.CeilToInt(viewWidth * pixelsPerUnit);
            int height = Mathf.CeilToInt(viewHeight * pixelsPerUnit);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[width * height];
            float total = samples * samples;

            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    float r = 0f;
                    float g = 0f;
                    float b = 0f;
                    float a = 0f;

                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float x = (px + (sx + 0.5f) / samples) / pixelsPerUnit;
                            float y = viewHeight - (py + (sy + 0.5f) / samples) / pixelsPerUnit;

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
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), width / worldWidth);
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

        private static bool Contains(Shape shape, float x, float y)
        {
            switch (shape.Kind)
            {
                case ShapeKind.Polygon:
                    return PolygonContains(shape.Points, x, y);

                case ShapeKind.Dome:
                    if (y > shape.Y) return false;
                    float nx = (x - shape.X) / shape.Width;
                    float ny = (y - shape.Y) / shape.Height;
                    return nx * nx + ny * ny <= 1f;

                default:
                    if (x < shape.X || x > shape.X + shape.Width || y < shape.Y || y > shape.Y + shape.Height) return false;
                    float dx = Mathf.Max(Mathf.Max(shape.X + shape.Radius - x, 0f), x - (shape.X + shape.Width - shape.Radius));
                    float dy = Mathf.Max(Mathf.Max(shape.Y + shape.Radius - y, 0f), y - (shape.Y + shape.Height - shape.Radius));
                    return dx * dx + dy * dy <= shape.Radius * shape.Radius;
            }
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

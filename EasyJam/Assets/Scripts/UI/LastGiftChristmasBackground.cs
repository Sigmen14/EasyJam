using UnityEngine;

namespace TheLastGift.UI
{
    /// <summary>
    /// Creates a lightweight, self-contained Christmas workshop backdrop.
    /// No external image package or runtime asset reference is required.
    /// </summary>
    public static class LastGiftChristmasBackground
    {
        private const int Width = 960;
        private const int Height = 540;

        public static Texture2D CreateTexture()
        {
            Color32[] pixels = new Color32[Width * Height];
            DrawBaseGradient(pixels);
            DrawWindow(pixels);
            DrawTree(pixels, 83, 105, 300, 225, true);
            DrawTree(pixels, 876, 95, 250, 190, false);
            DrawFireplaceGlow(pixels);
            DrawGarland(pixels);
            DrawPresent(pixels, 18, 12, 86, 62, new Color32(169, 57, 48, 255));
            DrawPresent(pixels, 112, 7, 72, 53, new Color32(42, 105, 72, 255));
            DrawPresent(pixels, 814, 9, 86, 58, new Color32(183, 132, 45, 255));
            DrawPresent(pixels, 900, 16, 48, 43, new Color32(163, 55, 48, 255));
            DrawSnow(pixels);

            Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.name = "Рождественский фон мастерской";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void DrawBaseGradient(Color32[] pixels)
        {
            Color top = new Color32(24, 42, 65, 255);
            Color bottom = new Color32(95, 60, 48, 255);
            for (int y = 0; y < Height; y++)
            {
                float t = y / (float)(Height - 1);
                Color32 color = Color.Lerp(bottom, top, t);
                int offset = y * Width;
                for (int x = 0; x < Width; x++)
                    pixels[offset + x] = color;
            }

            FillRect(pixels, 0, 0, Width, 92, new Color32(91, 56, 44, 255));
            FillRect(pixels, 0, 92, Width, 104, new Color32(129, 83, 57, 255));
            FillRect(pixels, 0, 101, Width, 106, new Color32(160, 112, 75, 255));
        }

        private static void DrawWindow(Color32[] pixels)
        {
            FillRect(pixels, 142, 145, 818, 444, new Color32(66, 42, 35, 255));
            FillRect(pixels, 157, 160, 803, 429, new Color32(24, 44, 72, 255));

            // Winter evening sky and a soft moon glow.
            FillCircle(pixels, 706, 365, 66, new Color32(64, 87, 112, 75));
            FillCircle(pixels, 706, 365, 31, new Color32(246, 222, 164, 255));
            FillCircle(pixels, 697, 373, 30, new Color32(24, 44, 72, 255));

            System.Random stars = new System.Random(12);
            for (int i = 0; i < 50; i++)
            {
                int x = 174 + stars.Next(0, 610);
                int y = 260 + stars.Next(0, 145);
                FillCircle(pixels, x, y, i % 7 == 0 ? 2 : 1,
                    new Color32(255, 242, 205, 230));
            }

            // Snow-covered hills outside the window.
            FillTriangle(pixels, 360, 315, 157, 198, 565, 198, new Color32(73, 112, 142, 255));
            FillTriangle(pixels, 530, 312, 370, 194, 803, 194, new Color32(91, 132, 157, 255));
            FillRect(pixels, 157, 160, 803, 206, new Color32(224, 232, 224, 255));
            DrawLine(pixels, 157, 207, 803, 207, 5, new Color32(244, 241, 224, 255));

            // Frost along the window edges.
            for (int x = 164; x < 798; x += 22)
            {
                int length = 5 + (x % 17);
                FillRect(pixels, x, 424 - length, x + 3, 429,
                    new Color32(215, 233, 239, 220));
            }

            // Wooden crossbars.
            FillRect(pixels, 462, 160, 478, 429, new Color32(118, 72, 48, 255));
            FillRect(pixels, 157, 284, 803, 300, new Color32(118, 72, 48, 255));
            DrawLine(pixels, 150, 146, 810, 146, 4, new Color32(185, 132, 83, 255));
            DrawLine(pixels, 150, 438, 810, 438, 4, new Color32(185, 132, 83, 255));
        }

        private static void DrawTree(Color32[] pixels, int centerX, int baseY, int height, int width, bool leftTree)
        {
            Color deep = leftTree ? new Color32(16, 72, 56, 255) : new Color32(18, 65, 53, 255);
            Color mid = new Color32(29, 100, 68, 255);
            Color light = new Color32(47, 126, 78, 255);

            FillRect(pixels, centerX - 13, baseY - 26, centerX + 13, baseY + 8,
                new Color32(98, 59, 39, 255));
            FillTriangle(pixels, centerX, baseY + height, centerX - width / 2, baseY,
                centerX + width / 2, baseY, deep);
            FillTriangle(pixels, centerX, baseY + (int)(height * 0.78f),
                centerX - (int)(width * 0.40f), baseY + (int)(height * 0.22f),
                centerX + (int)(width * 0.40f), baseY + (int)(height * 0.22f), mid);
            FillTriangle(pixels, centerX, baseY + (int)(height * 0.56f),
                centerX - (int)(width * 0.31f), baseY + (int)(height * 0.43f),
                centerX + (int)(width * 0.31f), baseY + (int)(height * 0.43f), light);

            DrawLine(pixels, centerX - width / 2 + 12, baseY + 42,
                centerX, baseY + height - 23, 3, new Color32(79, 148, 92, 255));
            DrawLine(pixels, centerX, baseY + height - 23,
                centerX + width / 2 - 12, baseY + 42, 3, new Color32(79, 148, 92, 255));

            // Snow resting on the branches.
            DrawLine(pixels, centerX - width / 2 + 12, baseY + 42,
                centerX - 8, baseY + 53, 5, new Color32(226, 238, 230, 240));
            DrawLine(pixels, centerX + 8, baseY + 53,
                centerX + width / 2 - 12, baseY + 42, 5, new Color32(226, 238, 230, 240));

            Color32[] bulbs =
            {
                new Color32(239, 194, 88, 255),
                new Color32(186, 57, 52, 255),
                new Color32(230, 222, 187, 255)
            };
            for (int i = 0; i < 13; i++)
            {
                int y = baseY + (int)(height * (0.18f + (i % 4) * 0.16f));
                int span = (int)(width * (1f - (float)(y - baseY) / height) * 0.42f);
                int x = centerX + (i % 2 == 0 ? -span : span) / 2;
                FillCircle(pixels, x, y, 5, bulbs[i % bulbs.Length]);
                FillCircle(pixels, x - 1, y + 2, 1, new Color32(255, 247, 213, 255));
            }

            FillTriangle(pixels, centerX, baseY + height + 18,
                centerX - 12, baseY + height - 2, centerX + 12, baseY + height - 2,
                new Color32(241, 197, 91, 255));
            FillCircle(pixels, centerX, baseY + height + 9, 6, new Color32(255, 225, 146, 255));
        }

        private static void DrawFireplaceGlow(Color32[] pixels)
        {
            FillCircle(pixels, 845, 214, 110, new Color32(196, 103, 44, 22));
            FillRect(pixels, 835, 92, 946, 244, new Color32(73, 43, 35, 255));
            FillRect(pixels, 850, 105, 932, 232, new Color32(32, 25, 30, 255));
            FillTriangle(pixels, 891, 215, 854, 110, 926, 110,
                new Color32(168, 70, 38, 255));
            FillTriangle(pixels, 892, 185, 865, 111, 917, 111,
                new Color32(246, 152, 62, 255));
            FillTriangle(pixels, 893, 160, 877, 111, 909, 111,
                new Color32(255, 213, 112, 255));
            DrawLine(pixels, 832, 98, 949, 98, 8, new Color32(149, 94, 60, 255));
        }

        private static void DrawGarland(Color32[] pixels)
        {
            int previousX = 0;
            int previousY = 485;
            for (int x = 0; x <= Width; x += 8)
            {
                int y = 478 - (int)(18f * Mathf.Sin(x / (float)Width * Mathf.PI));
                if (x > 0)
                    DrawLine(pixels, previousX, previousY, x, y, 3, new Color32(33, 75, 58, 255));

                if (x % 40 == 0)
                {
                    Color32 bulb = x % 120 == 0
                        ? new Color32(247, 193, 91, 255)
                        : x % 80 == 0
                            ? new Color32(190, 62, 52, 255)
                            : new Color32(238, 225, 177, 255);
                    FillCircle(pixels, x, y - 5, 6, new Color32(249, 207, 114, 55));
                    FillCircle(pixels, x, y - 5, 3, bulb);
                }

                previousX = x;
                previousY = y;
            }
        }

        private static void DrawPresent(Color32[] pixels, int x, int y, int width, int height, Color32 color)
        {
            FillRect(pixels, x, y, x + width, y + height, color);
            FillRect(pixels, x + width / 2 - 4, y, x + width / 2 + 4, y + height,
                new Color32(235, 196, 118, 255));
            FillRect(pixels, x, y + height / 2 - 3, x + width, y + height / 2 + 3,
                new Color32(235, 196, 118, 255));
            FillCircle(pixels, x + width / 2 - 5, y + height - 2, 8,
                new Color32(235, 196, 118, 255));
            FillCircle(pixels, x + width / 2 + 5, y + height - 2, 8,
                new Color32(235, 196, 118, 255));
        }

        private static void DrawSnow(Color32[] pixels)
        {
            System.Random random = new System.Random(2026);
            for (int i = 0; i < 130; i++)
            {
                int x = random.Next(0, Width);
                int y = random.Next(95, Height);
                int radius = i % 9 == 0 ? 3 : 1 + (i % 2);
                FillCircle(pixels, x, y, radius,
                    new Color32(242, 247, 241, (byte)(135 + i % 110)));
            }
        }

        private static void FillRect(Color32[] pixels, int xMin, int yMin, int xMax, int yMax, Color32 color)
        {
            xMin = Mathf.Clamp(xMin, 0, Width);
            xMax = Mathf.Clamp(xMax, 0, Width);
            yMin = Mathf.Clamp(yMin, 0, Height);
            yMax = Mathf.Clamp(yMax, 0, Height);
            for (int y = yMin; y < yMax; y++)
            {
                int row = y * Width;
                for (int x = xMin; x < xMax; x++)
                    BlendPixel(pixels, row + x, color);
            }
        }

        private static void FillCircle(Color32[] pixels, int centerX, int centerY, int radius, Color32 color)
        {
            int radiusSquared = radius * radius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                if (y < 0 || y >= Height) continue;
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    if (x < 0 || x >= Width) continue;
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= radiusSquared)
                        BlendPixel(pixels, y * Width + x, color);
                }
            }
        }

        private static void FillTriangle(Color32[] pixels,
            int x1, int y1, int x2, int y2, int x3, int y3, Color32 color)
        {
            // Rasterize a triangle from three arbitrary vertices. The previous
            // helper accepted only an apex and a horizontal base, while the
            // artwork calls pass complete (x, y) pairs for all three corners.
            float area = EdgeFunction(x1, y1, x2, y2, x3, y3);
            if (Mathf.Abs(area) < 0.001f)
                return;

            int minX = Mathf.Clamp(Mathf.Min(x1, Mathf.Min(x2, x3)), 0, Width - 1);
            int maxX = Mathf.Clamp(Mathf.Max(x1, Mathf.Max(x2, x3)), 0, Width - 1);
            int minY = Mathf.Clamp(Mathf.Min(y1, Mathf.Min(y2, y3)), 0, Height - 1);
            int maxY = Mathf.Clamp(Mathf.Max(y1, Mathf.Max(y2, y3)), 0, Height - 1);
            bool positiveWinding = area > 0f;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float w1 = EdgeFunction(x2, y2, x3, y3, x, y);
                    float w2 = EdgeFunction(x3, y3, x1, y1, x, y);
                    float w3 = EdgeFunction(x1, y1, x2, y2, x, y);
                    bool inside = positiveWinding
                        ? w1 >= 0f && w2 >= 0f && w3 >= 0f
                        : w1 <= 0f && w2 <= 0f && w3 <= 0f;

                    if (inside)
                        BlendPixel(pixels, y * Width + x, color);
                }
            }
        }

        private static float EdgeFunction(int ax, int ay, int bx, int by, int px, int py)
        {
            return (px - ax) * (float)(by - ay) - (py - ay) * (float)(bx - ax);
        }

        private static void DrawLine(Color32[] pixels, int x0, int y0, int x1, int y1,
            int thickness, Color32 color)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            if (steps == 0)
            {
                FillCircle(pixels, x0, y0, Mathf.Max(1, thickness / 2), color);
                return;
            }

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                FillCircle(pixels, x, y, Mathf.Max(1, thickness / 2), color);
            }
        }

        private static void BlendPixel(Color32[] pixels, int index, Color32 color)
        {
            if (color.a == 255)
            {
                pixels[index] = color;
                return;
            }

            Color32 behind = pixels[index];
            float alpha = color.a / 255f;
            float inverse = 1f - alpha;
            pixels[index] = new Color32(
                (byte)(color.r * alpha + behind.r * inverse),
                (byte)(color.g * alpha + behind.g * inverse),
                (byte)(color.b * alpha + behind.b * inverse),
                255);
        }
    }
}

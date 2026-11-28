using UnityEngine;

namespace TheLastGift.Packing
{
    /// <summary>Creates small in-game illustrations for named story gifts when no authored sprite is assigned.</summary>
    internal static class GiftIllustrationFactory
    {
        private static Sprite trainSprite;
        private static Sprite snowGlobeSprite;

        public static Sprite CreateFor(GiftDefinition definition)
        {
            if (definition == null) return null;

            var name = definition.GiftName.ToLowerInvariant();
            if (name.Contains("поезд"))
                return trainSprite != null ? trainSprite : (trainSprite = CreateTrain());
            if (name.Contains("шар"))
                return snowGlobeSprite != null ? snowGlobeSprite : (snowGlobeSprite = CreateSnowGlobe());

            return null;
        }

        private static Sprite CreateTrain()
        {
            const int width = 384;
            const int height = 96;
            var texture = CreateCanvas(width, height, "Gift_Train_Illustration");
            var outline = new Color32(48, 29, 27, 255);
            var red = new Color32(171, 37, 34, 255);
            var redLight = new Color32(218, 59, 43, 255);
            var blue = new Color32(32, 91, 128, 255);
            var blueLight = new Color32(59, 143, 177, 255);
            var gold = new Color32(226, 160, 52, 255);
            var goldLight = new Color32(255, 215, 121, 255);
            var wood = new Color32(104, 60, 39, 255);
            var coal = new Color32(44, 44, 49, 255);

            // Tender and coal.
            Rect(texture, 19, 34, 112, 68, outline);
            Rect(texture, 23, 38, 108, 65, red);
            Rect(texture, 23, 38, 108, 43, blue);
            Rect(texture, 20, 63, 113, 70, gold);
            Rect(texture, 25, 68, 105, 78, wood);
            Circle(texture, 38, 76, 8, coal);
            Circle(texture, 52, 82, 10, coal);
            Circle(texture, 68, 84, 11, coal);
            Circle(texture, 84, 81, 10, coal);
            Circle(texture, 98, 75, 7, coal);
            Rect(texture, 38, 49, 94, 59, new Color32(187, 43, 37, 255));
            Rect(texture, 43, 52, 89, 55, gold);
            Rect(texture, 63, 50, 69, 58, goldLight);

            // Locomotive cab and roof.
            Rect(texture, 128, 38, 197, 83, outline);
            Rect(texture, 133, 43, 192, 81, red);
            Rect(texture, 140, 66, 184, 77, new Color32(190, 43, 36, 255));
            Rect(texture, 144, 69, 180, 74, gold);
            Rect(texture, 149, 70, 175, 73, red);
            Rect(texture, 124, 81, 202, 88, wood);
            Rect(texture, 119, 87, 206, 94, blue);
            Rect(texture, 125, 90, 200, 93, blueLight);

            // Boiler, bands and front details.
            Rect(texture, 184, 51, 315, 78, outline);
            Circle(texture, 190, 64, 14, outline);
            Circle(texture, 309, 64, 14, outline);
            Rect(texture, 188, 54, 310, 76, redLight);
            Circle(texture, 190, 65, 11, redLight);
            Circle(texture, 309, 65, 11, redLight);
            Rect(texture, 217, 53, 225, 77, gold);
            Rect(texture, 220, 54, 223, 76, goldLight);
            Rect(texture, 271, 53, 279, 77, gold);
            Rect(texture, 274, 54, 277, 76, goldLight);

            // Smokestack, steam dome and headlamp.
            Rect(texture, 279, 72, 294, 91, gold);
            Rect(texture, 276, 89, 297, 94, goldLight);
            Rect(texture, 276, 70, 297, 76, blue);
            Circle(texture, 251, 76, 10, gold);
            Circle(texture, 251, 79, 6, goldLight);
            Circle(texture, 330, 64, 11, outline);
            Circle(texture, 330, 64, 8, gold);
            Circle(texture, 330, 64, 5, goldLight);
            Triangle(texture, 316, 52, 350, 52, 345, 39, new Color32(169, 40, 35, 255), outline);

            // Wheels and brass hubs.
            Wheel(texture, 42, 33, 12, outline, gold, red);
            Wheel(texture, 89, 33, 12, outline, gold, red);
            Wheel(texture, 205, 31, 15, outline, gold, red);
            Wheel(texture, 255, 31, 15, outline, gold, red);
            Wheel(texture, 302, 31, 15, outline, gold, red);
            Line(texture, 205, 31, 255, 31, 3, goldLight);
            Line(texture, 255, 31, 302, 31, 3, goldLight);
            Line(texture, 118, 51, 132, 51, 5, gold);

            // Gold star on the cab.
            Star(texture, 163, 57, 7, 3, goldLight, outline);

            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateSnowGlobe()
        {
            const int size = 160;
            var texture = CreateCanvas(size, size, "Gift_Snow_Globe_Illustration");
            var outline = new Color32(34, 52, 66, 255);
            var gold = new Color32(224, 159, 53, 255);
            var goldLight = new Color32(255, 217, 126, 255);
            var red = new Color32(166, 35, 34, 255);
            var blue = new Color32(83, 171, 213, 255);
            var snow = new Color32(245, 250, 252, 255);
            var green = new Color32(19, 101, 73, 255);

            // Wooden stand with gold trim.
            Rect(texture, 30, 13, 130, 42, outline);
            Rect(texture, 34, 16, 126, 38, red);
            Rect(texture, 27, 10, 133, 18, gold);
            Rect(texture, 33, 12, 127, 15, goldLight);
            Rect(texture, 39, 23, 121, 34, new Color32(191, 55, 42, 255));
            Rect(texture, 44, 25, 116, 28, gold);

            // Glass globe and blue interior.
            Circle(texture, 80, 91, 61, outline);
            Circle(texture, 80, 91, 56, blue);
            Circle(texture, 80, 91, 51, new Color32(98, 187, 224, 255));
            Circle(texture, 80, 91, 47, new Color32(86, 169, 211, 255));

            // Snow-covered ground.
            Ellipse(texture, 38, 52, 122, 76, snow);
            Ellipse(texture, 45, 54, 115, 69, new Color32(255, 255, 255, 255));

            // Fir tree, trunk, tiers and snow.
            Rect(texture, 76, 54, 84, 68, new Color32(104, 61, 34, 255));
            Triangle(texture, 80, 119, 53, 73, 107, 73, green, outline);
            Triangle(texture, 80, 105, 46, 61, 114, 61, new Color32(14, 87, 64, 255), outline);
            Triangle(texture, 80, 91, 39, 49, 121, 49, new Color32(13, 78, 59, 255), outline);
            Line(texture, 61, 86, 73, 92, 3, snow);
            Line(texture, 84, 99, 100, 91, 3, snow);
            Line(texture, 54, 66, 69, 72, 3, snow);
            Line(texture, 90, 77, 105, 70, 3, snow);
            Circle(texture, 68, 88, 4, red);
            Circle(texture, 91, 79, 4, goldLight);
            Circle(texture, 79, 105, 4, goldLight);
            Star(texture, 80, 124, 8, 4, goldLight, outline);

            // Floating snowflakes and glass reflections.
            Snowflake(texture, 48, 105, 9, snow);
            Snowflake(texture, 112, 101, 8, snow);
            Snowflake(texture, 117, 78, 5, snow);
            Line(texture, 42, 119, 49, 133, 3, new Color32(235, 250, 255, 220));
            Line(texture, 46, 126, 59, 139, 2, new Color32(255, 255, 255, 180));

            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Texture2D CreateCanvas(int width, int height, string name)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);
            texture.SetPixels32(pixels);
            return texture;
        }

        private static void Rect(Texture2D t, int x0, int y0, int x1, int y1, Color32 color)
        {
            for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
                Pixel(t, x, y, color);
        }

        private static void Circle(Texture2D t, int cx, int cy, int radius, Color32 color)
        {
            var r2 = radius * radius;
            for (var y = cy - radius; y <= cy + radius; y++)
            for (var x = cx - radius; x <= cx + radius; x++)
            {
                var dx = x - cx;
                var dy = y - cy;
                if (dx * dx + dy * dy <= r2) Pixel(t, x, y, color);
            }
        }

        private static void Ellipse(Texture2D t, int x0, int y0, int x1, int y1, Color32 color)
        {
            var cx = (x0 + x1) * 0.5f;
            var cy = (y0 + y1) * 0.5f;
            var rx = Mathf.Max(1f, (x1 - x0) * 0.5f);
            var ry = Mathf.Max(1f, (y1 - y0) * 0.5f);
            for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                var dx = (x - cx) / rx;
                var dy = (y - cy) / ry;
                if (dx * dx + dy * dy <= 1f) Pixel(t, x, y, color);
            }
        }

        private static void Triangle(Texture2D t, int ax, int ay, int bx, int by, int cx, int cy,
            Color32 fill, Color32 outline)
        {
            var minX = Mathf.Min(ax, Mathf.Min(bx, cx));
            var maxX = Mathf.Max(ax, Mathf.Max(bx, cx));
            var minY = Mathf.Min(ay, Mathf.Min(by, cy));
            var maxY = Mathf.Max(ay, Mathf.Max(by, cy));
            var area = Edge(ax, ay, bx, by, cx, cy);
            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
            {
                var e1 = Edge(ax, ay, bx, by, x, y);
                var e2 = Edge(bx, by, cx, cy, x, y);
                var e3 = Edge(cx, cy, ax, ay, x, y);
                if ((area >= 0f && e1 >= 0f && e2 >= 0f && e3 >= 0f) ||
                    (area < 0f && e1 <= 0f && e2 <= 0f && e3 <= 0f))
                    Pixel(t, x, y, fill);
            }
            Line(t, ax, ay, bx, by, 2, outline);
            Line(t, bx, by, cx, cy, 2, outline);
            Line(t, cx, cy, ax, ay, 2, outline);
        }

        private static float Edge(int ax, int ay, int bx, int by, int px, int py)
        {
            return (px - ax) * (by - ay) - (py - ay) * (bx - ax);
        }

        private static void Line(Texture2D t, int x0, int y0, int x1, int y1, int thickness, Color32 color)
        {
            var dx = x1 - x0;
            var dy = y1 - y0;
            var steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
            if (steps == 0) { Circle(t, x0, y0, thickness, color); return; }
            for (var i = 0; i <= steps; i++)
            {
                var x = Mathf.RoundToInt(x0 + dx * (i / (float)steps));
                var y = Mathf.RoundToInt(y0 + dy * (i / (float)steps));
                Circle(t, x, y, thickness, color);
            }
        }

        private static void Wheel(Texture2D t, int x, int y, int radius, Color32 outline, Color32 rim, Color32 fill)
        {
            Circle(t, x, y, radius, outline);
            Circle(t, x, y, radius - 3, rim);
            Circle(t, x, y, radius - 6, fill);
            Circle(t, x, y, 3, new Color32(255, 218, 124, 255));
        }

        private static void Star(Texture2D t, int cx, int cy, int outer, int inner, Color32 fill, Color32 outline)
        {
            var points = new Vector2[10];
            for (var i = 0; i < points.Length; i++)
            {
                var angle = (-90f + i * 36f) * Mathf.Deg2Rad;
                var radius = i % 2 == 0 ? outer : inner;
                points[i] = new Vector2(cx + Mathf.Cos(angle) * radius, cy + Mathf.Sin(angle) * radius);
            }
            // Small star rendered as a compact gold diamond with a contrasting center.
            Triangle(t, Mathf.RoundToInt(points[0].x), Mathf.RoundToInt(points[0].y),
                Mathf.RoundToInt(points[3].x), Mathf.RoundToInt(points[3].y),
                Mathf.RoundToInt(points[6].x), Mathf.RoundToInt(points[6].y), fill, outline);
            Triangle(t, Mathf.RoundToInt(points[0].x), Mathf.RoundToInt(points[0].y),
                Mathf.RoundToInt(points[6].x), Mathf.RoundToInt(points[6].y),
                Mathf.RoundToInt(points[9].x), Mathf.RoundToInt(points[9].y), fill, outline);
        }

        private static void Snowflake(Texture2D t, int cx, int cy, int radius, Color32 color)
        {
            Line(t, cx - radius, cy, cx + radius, cy, 1, color);
            Line(t, cx, cy - radius, cx, cy + radius, 1, color);
            Line(t, cx - radius / 2, cy - radius / 2, cx + radius / 2, cy + radius / 2, 1, color);
            Line(t, cx - radius / 2, cy + radius / 2, cx + radius / 2, cy - radius / 2, 1, color);
            Circle(t, cx, cy, 2, color);
        }

        private static void Pixel(Texture2D t, int x, int y, Color32 color)
        {
            if (x >= 0 && x < t.width && y >= 0 && y < t.height)
                t.SetPixel(x, y, color);
        }

        public static void Release()
        {
            ReleaseSprite(ref trainSprite);
            ReleaseSprite(ref snowGlobeSprite);
        }

        private static void ReleaseSprite(ref Sprite sprite)
        {
            if (sprite == null) return;

            Texture2D texture = sprite.texture;
            UnityEngine.Object.Destroy(sprite);
            if (texture != null) UnityEngine.Object.Destroy(texture);
            sprite = null;
        }
    }
}
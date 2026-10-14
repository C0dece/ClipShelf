using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public class IconProcessor
{
    public static void Main(string[] args)
    {
        string srcPath = args.Length > 0 ? args[0] : @"C:\Users\codece\.gemini\antigravity\brain\03924531-3b35-46f4-ad3c-cd6b1b55f919\clipshelf_app_icon_v2_1791960924461.jpg";
        string outPng = args.Length > 1 ? args[1] : @"d:\Dev\Projects\Tools\PasteImageAsFile\assets\app_icon.png";
        string outIco = args.Length > 2 ? args[2] : @"d:\Dev\Projects\Tools\PasteImageAsFile\assets\app.ico";

        Console.WriteLine("Loading source: " + srcPath);
        using (Bitmap src = new Bitmap(srcPath))
        {
            int w = src.Width;
            int h = src.Height;

            BitmapData srcData = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] pixels = new byte[Math.Abs(srcData.Stride) * h];
            Marshal.Copy(srcData.Scan0, pixels, 0, pixels.Length);
            src.UnlockBits(srcData);

            int stride = Math.Abs(srcData.Stride);

            // Маска фона
            bool[] isBg = new bool[w * h];
            Queue<int> q = new Queue<int>();

            // Заполняем по периметру
            for (int x = 0; x < w; x++)
            {
                q.Enqueue(x); isBg[x] = true;
                int bottomIdx = (h - 1) * w + x;
                q.Enqueue(bottomIdx); isBg[bottomIdx] = true;
            }
            for (int y = 0; y < h; y++)
            {
                int leftIdx = y * w;
                q.Enqueue(leftIdx); isBg[leftIdx] = true;
                int rightIdx = y * w + (w - 1);
                q.Enqueue(rightIdx); isBg[rightIdx] = true;
            }

            // Отверстие в зажиме (центр ~522, 155)
            int holeIdx = 155 * w + 522;
            if (holeIdx >= 0 && holeIdx < w * h)
            {
                q.Enqueue(holeIdx); isBg[holeIdx] = true;
            }

            // Flood fill
            while (q.Count > 0)
            {
                int idx = q.Dequeue();
                int px = idx % w;
                int py = idx / w;

                int offset = py * stride + px * 4;
                byte b = pixels[offset];
                byte g = pixels[offset + 1];
                byte r = pixels[offset + 2];

                // Белый фон: R > 235 && G > 235 && B > 235
                if (r > 235 && g > 235 && b > 235)
                {
                    // 4-соседи
                    if (px > 0 && !isBg[idx - 1]) { isBg[idx - 1] = true; q.Enqueue(idx - 1); }
                    if (px < w - 1 && !isBg[idx + 1]) { isBg[idx + 1] = true; q.Enqueue(idx + 1); }
                    if (py > 0 && !isBg[idx - w]) { isBg[idx - w] = true; q.Enqueue(idx - w); }
                    if (py < h - 1 && !isBg[idx + w]) { isBg[idx + w] = true; q.Enqueue(idx + w); }
                }
                else
                {
                    // Не фон
                    isBg[idx] = false;
                }
            }

            // Создаем Bitmap с прозрачностью
            Bitmap cleanBmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData cleanData = cleanBmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            byte[] outPixels = new byte[Math.Abs(cleanData.Stride) * h];

            int minX = w, maxX = 0, minY = h, maxY = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    int off = y * stride + x * 4;

                    if (isBg[idx])
                    {
                        outPixels[off] = 0;
                        outPixels[off + 1] = 0;
                        outPixels[off + 2] = 0;
                        outPixels[off + 3] = 0;
                    }
                    else
                    {
                        byte b = pixels[off];
                        byte g = pixels[off + 1];
                        byte r = pixels[off + 2];

                        int dist = Math.Max(255 - r, Math.Max(255 - g, 255 - b));
                        byte alpha = 255;
                        if (dist < 32)
                        {
                            alpha = (byte)(dist * 255 / 32);
                        }

                        if (alpha > 12)
                        {
                            double aNorm = alpha / 255.0;
                            double invA = 1.0 - aNorm;

                            byte cleanR = (byte)Math.Min(255, Math.Max(0, (r - 255.0 * invA) / aNorm));
                            byte cleanG = (byte)Math.Min(255, Math.Max(0, (g - 255.0 * invA) / aNorm));
                            byte cleanB = (byte)Math.Min(255, Math.Max(0, (b - 255.0 * invA) / aNorm));

                            outPixels[off] = cleanB;
                            outPixels[off + 1] = cleanG;
                            outPixels[off + 2] = cleanR;
                            outPixels[off + 3] = alpha;

                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                        else
                        {
                            outPixels[off] = 0;
                            outPixels[off + 1] = 0;
                            outPixels[off + 2] = 0;
                            outPixels[off + 3] = 0;
                        }
                    }
                }
            }

            Marshal.Copy(outPixels, 0, cleanData.Scan0, outPixels.Length);
            cleanBmp.UnlockBits(cleanData);

            Console.WriteLine(string.Format("Object bounds: X={0}..{1} (W={2}), Y={3}..{4} (H={5})",
                minX, maxX, maxX - minX + 1, minY, maxY, maxY - minY + 1));

            // Обрезаем по объекту
            int objW = maxX - minX + 1;
            int objH = maxY - minY + 1;

            Bitmap objBmp = new Bitmap(objW, objH, PixelFormat.Format32bppArgb);
            using (Graphics og = Graphics.FromImage(objBmp))
            {
                og.DrawImage(cleanBmp, 0, 0, new Rectangle(minX, minY, objW, objH), GraphicsUnit.Pixel);
            }
            cleanBmp.Dispose();

            // Создаем мастер 512x512
            int targetCanvas = 512;
            Bitmap finalBmp = new Bitmap(targetCanvas, targetCanvas, PixelFormat.Format32bppArgb);
            using (Graphics fg = Graphics.FromImage(finalBmp))
            {
                fg.SmoothingMode = SmoothingMode.AntiAlias;
                fg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                fg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                fg.Clear(Color.Transparent);

                // Во весь рост по высоте (отступ 4px сверху и снизу)
                int destH = targetCanvas - 8;
                int destW = (int)(destH * (objW / (double)objH));
                int destX = (targetCanvas - destW) / 2;
                int destY = 4;

                Console.WriteLine(string.Format("Drawing full height icon: X={0}, Y={1}, W={2}, H={3}", destX, destY, destW, destH));
                fg.DrawImage(objBmp, destX, destY, destW, destH);
            }
            objBmp.Dispose();

            // Сохраняем PNG
            finalBmp.Save(outPng, ImageFormat.Png);
            Console.WriteLine("Saved PNG: " + outPng);

            // Создаем ICO
            CreateIco(finalBmp, outIco);
            Console.WriteLine("Saved ICO: " + outIco);

            finalBmp.Dispose();
        }
    }

    private static void CreateIco(Bitmap srcBmp, string icoPath)
    {
        int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
        List<byte[]> dibList = new List<byte[]>();

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write((ushort)0); // reserved
            bw.Write((ushort)1); // icon type
            bw.Write((ushort)sizes.Length);

            foreach (int s in sizes)
            {
                using (Bitmap scaled = new Bitmap(s, s, PixelFormat.Format32bppArgb))
                {
                    using (Graphics sg = Graphics.FromImage(scaled))
                    {
                        sg.SmoothingMode = SmoothingMode.AntiAlias;
                        sg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        sg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        sg.Clear(Color.Transparent);
                        sg.DrawImage(srcBmp, 0, 0, s, s);
                    }

                    BitmapData bd = scaled.LockBits(new Rectangle(0, 0, s, s), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    int stride = Math.Abs(bd.Stride);
                    byte[] bytes = new byte[stride * s];
                    Marshal.Copy(bd.Scan0, bytes, 0, bytes.Length);
                    scaled.UnlockBits(bd);

                    // Flip bottom-up for ICO DIB
                    byte[] flipped = new byte[bytes.Length];
                    for (int row = 0; row < s; row++)
                    {
                        int srcOff = row * stride;
                        int dstOff = (s - 1 - row) * stride;
                        Array.Copy(bytes, srcOff, flipped, dstOff, stride);
                    }

                    // AND mask
                    int andMaskRowBytes = (int)Math.Ceiling(s / 32.0) * 4;
                    int andMaskSize = andMaskRowBytes * s;
                    byte[] andMask = new byte[andMaskSize];

                    using (MemoryStream dibMs = new MemoryStream())
                    using (BinaryWriter dibBw = new BinaryWriter(dibMs))
                    {
                        // BITMAPINFOHEADER (40 bytes)
                        dibBw.Write((uint)40);
                        dibBw.Write((int)s);
                        dibBw.Write((int)(s * 2));
                        dibBw.Write((ushort)1);
                        dibBw.Write((ushort)32);
                        dibBw.Write((uint)0);
                        dibBw.Write((uint)(flipped.Length + andMaskSize));
                        dibBw.Write((int)0);
                        dibBw.Write((int)0);
                        dibBw.Write((uint)0);
                        dibBw.Write((uint)0);

                        dibBw.Write(flipped);
                        dibBw.Write(andMask);
                        dibBw.Flush();

                        byte[] dibData = dibMs.ToArray();
                        dibList.Add(dibData);

                        byte dim = (byte)(s >= 256 ? 0 : s);
                        bw.Write(dim);
                        bw.Write(dim);
                        bw.Write((byte)0); // color count
                        bw.Write((byte)0); // reserved
                        bw.Write((ushort)1); // planes
                        bw.Write((ushort)32); // bpp
                        bw.Write((uint)dibData.Length);
                        bw.Write((uint)0); // offset placeholder
                    }
                }
            }

            int headerSize = 6 + 16 * sizes.Length;
            int offset = headerSize;
            for (int i = 0; i < sizes.Length; i++)
            {
                ms.Position = 6 + 16 * i + 12;
                bw.Write((uint)offset);
                offset += dibList[i].Length;
            }

            ms.Position = ms.Length;
            foreach (byte[] dib in dibList)
            {
                bw.Write(dib);
            }

            File.WriteAllBytes(icoPath, ms.ToArray());
        }
    }
}

﻿using System;
using System.Collections.Generic;
using Android.Graphics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Android.Hardware.Lights;
using static Android.Icu.Text.ListFormatter;

namespace Spectrogram {
    /// <summary>
    /// This class converts a collection of FFTs to a colormapped spectrogram image
    /// </summary>
    public class ImageMaker {
        /// <summary>
        /// Colormap used to translate intensity to pixel color
        /// </summary>
        public Colormap Colormap;

        /// <summary>
        /// Intensity is multiplied by this number before converting it to the pixel color according to the colormap
        /// </summary>
        public double Intensity = 1;

        /// <summary>
        /// If True, intensity will be log-scaled to represent Decibels
        /// </summary>
        public bool IsDecibel = false;

        /// <summary>
        /// If <see cref="IsDecibel"/> is enabled, intensity will be scaled by this value prior to log transformation
        /// </summary>
        public double DecibelScaleFactor = 1;

        /// <summary>
        /// If False, the spectrogram will proceed in time from left to right across the whole image.
        /// If True, the image will be broken and the newest FFTs will appear on the left and oldest on the right.
        /// </summary>
        public bool IsRoll = false;

        /// <summary>
        /// If <see cref="IsRoll"/> is enabled, this value indicates the pixel position of the break point.
        /// </summary>
        public int RollOffset = 0;

        /// <summary>
        /// If True, the spectrogram will flow top-down (oldest to newest) rather than left-right.
        /// </summary>
        public bool IsRotated = false;

        public ImageMaker() {

        }

        static int ddd = 0;
        public Bitmap GetBitmap(List<double[]> ffts) {
            if(ffts.Count == 0)
                throw new ArgumentException("Not enough data in FFTs to generate an image yet.");

            int Width = IsRotated ? ffts[0].Length : ffts.Count;
            int Height = IsRotated ? ffts.Count : ffts[0].Length;

            var bmp = Bitmap.CreateBitmap(Width, Height, Bitmap.Config.Argb8888);
            //Colormap.Apply(bmp);

            //Rectangle lockRect = new(0, 0, Width, Height);
            //BitmapData bitmapData = bmp.LockBits(lockRect, ImageLockMode.ReadOnly, bmp.PixelFormat);
            //int stride = bitmapData.Stride;

            //byte[] bytes = new byte[bitmapData.Stride * bmp.Height];
            //var bytes = bmp.GetPixels(int[] ? pixels, int offset, int stride, int x, int y, int width, int height);

            //System.Diagnostics.Debug.WriteLine($"---- GetBitmap 0: w:{Width}, h:{Height}");
            int stride = Width + 1;
            var pixels = new int[stride * bmp.Height];
            Parallel.For(0, Width, col => {
                int sourceCol = col;
                if(IsRoll) {
                    sourceCol += Width - RollOffset % Width;
                    if(sourceCol >= Width)
                        sourceCol -= Width;
                }

                for(int row = 0; row < Height; row++) {
                    double value = IsRotated
                        ? ffts[Height - row - 1][sourceCol]
                        : ffts[sourceCol][row];

                    if(IsDecibel)
                        value = 20 * Math.Log10(value * DecibelScaleFactor + 1);

                    value *= Intensity;
                    value = Math.Min(value, 255);
                    int bytePosition = (Height - 1 - row) * stride + col;
                    var b = (byte)value;
                    var alfa = (byte)0xFF;
                    //pixels[bytePosition] = (byte)bytePosition + (int)sss + (byte)(ddd / 1);
                    pixels[bytePosition] = (alfa << 24) + /*(b << 16)*/ + (b << 8) /*+ (b << 0)*/;
                }
            });
            ddd += 30;

            System.Diagnostics.Debug.WriteLine($"---- GetBitmap 1: w:{Width}, h:{Height}");
            bmp.SetPixels(pixels, 0, stride, 0, 0, Width, Height);

            //Marshal.Copy(bytes, 0, bitmapData.Scan0, bytes.Length);
            //bmp.UnlockBits(bitmapData);

            return bmp;
        }
    }
}

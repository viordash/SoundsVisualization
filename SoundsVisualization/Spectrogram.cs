﻿using System.Buffers;
using Android.Drm;
using Android.Graphics;
using Android.Hardware.Lights;
using FftSharp;
using static Android.Icu.Text.ListFormatter;
using static Android.InputMethodServices.Keyboard;

namespace SoundsVisualization {
    internal class Spectrogram {
        readonly double[] hanningWindow;
        public readonly List<short> PcmData;
        readonly int fftIndexMinFreq;
        readonly int fftIndexMaxFreq;
        readonly List<double[]> FFTs;

        readonly double intensity;
        readonly int height;
        readonly int width;
        readonly int fftSize;
        readonly int stepSize;
        readonly double horzScale;

        public Spectrogram(int sampleRate, double minFreq, double maxFreq, int fftSize, int stepSize, int width, double intensity) {
            this.fftSize = fftSize;
            this.stepSize = stepSize;
            this.width = width;
            this.intensity = intensity;

            var window = new FftSharp.Windows.Hanning();
            hanningWindow = window.Create(fftSize);
            PcmData = new List<short>(fftSize * 4);

            var freqNyquist = sampleRate / 2;
            var hzPerPixel = (double)sampleRate / fftSize;
            fftIndexMinFreq = (minFreq == 0) ? 0 : (int)(minFreq / hzPerPixel);
            fftIndexMaxFreq = (maxFreq >= freqNyquist) ? fftSize / 2 : (int)(maxFreq / hzPerPixel);
            height = fftIndexMaxFreq - fftIndexMinFreq;

            FFTs = new List<double[]>();
        }


        public Bitmap? Process() {
            var fftsToProcess = (PcmData.Count - fftSize) / stepSize;

            if(fftsToProcess < 1) {
                return null;
            }

            var newFfts = new double[fftsToProcess][];

            Parallel.For(0, fftsToProcess, newFftIndex => {
                var samples = new System.Numerics.Complex[fftSize];

                int sourceIndex = newFftIndex * stepSize;
                for(int i = 0; i < fftSize; i++) {
                    samples[i] = PcmData[sourceIndex + i] * hanningWindow[i];
                }

                FftSharp.FFT.Forward(samples);

                var samplesWindow = samples.AsSpan(fftIndexMinFreq);
                newFfts[newFftIndex] = new double[height];
                for(int i = 0; i < height; i++) {
                    newFfts[newFftIndex][i] = samplesWindow[i].Magnitude / fftSize;
                }
            });

            foreach(var newFft in newFfts) {
                FFTs.Add(newFft);
            }

            PcmData.RemoveRange(0, fftsToProcess * stepSize);

            PadOrTrimForFixedWidth();
            return GetBitmap();
        }

        void PadOrTrimForFixedWidth() {
            var overhang = FFTs.Count - width;
            if(overhang > 0) {
                FFTs.RemoveRange(0, overhang);
            }
            while(FFTs.Count < width) {
                FFTs.Insert(0, new double[height]);
            }
        }

        Bitmap GetBitmap() {
            int w = FFTs[0].Length;
            int h = FFTs.Count;

            var bmp = Bitmap.CreateBitmap(w, h, Bitmap.Config.Argb8888!);
            if(bmp == null) {
                throw new ArgumentNullException(nameof(bmp));
            }

            int stride = w;
            var pixels = new int[bmp.Width * bmp.Height];

            bmp.GetPixels(pixels, 0, stride, 0, 0, w, 1);

            Parallel.For(0, w, col => {
                for(int row = 0; row < h; row++) {

                    var value = FFTs[h - row - 1][col];

                    value *= intensity;
                    value = Math.Min(value, 255);
                    var bytePosition = (h - 1 - row) * stride + col;
                    var b = (byte)value;
                    var alfa = (byte)0xFF;
                    pixels[bytePosition] = (alfa << 24) + /*(b << 16)*/ +(b << 8) /*+ (b << 0)*/;
                }
            });

            System.Diagnostics.Debug.WriteLine($"---- GetBitmap 1: w:{w}, h:{h}");
            bmp.SetPixels(pixels, 0, stride, 0, 0, w, 1);

            return bmp;
        }

    }
}
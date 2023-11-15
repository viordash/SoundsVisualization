using System.Buffers;
using Android.Graphics;

namespace SoundsVisualization {
    internal class Spectrogram {
        readonly double[] hanningWindow;
        public readonly List<short> PcmData;
        readonly int fftIndexMinFreq;
        readonly int fftIndexMaxFreq;

        readonly double intensity;
        readonly int height;
        readonly int width;
        readonly int fftSize;
        readonly int stepSize;
        readonly int[] pixels;
        readonly Bitmap? bitmap;
        int yPos;
        readonly ArrayPool<System.Numerics.Complex> arrPoolComplex;
        readonly double[] cutoffArr;
        readonly int cutoffBand;
        int cutoffY;

        public Spectrogram(int sampleRate, double minFreq, double maxFreq, int fftSize, int stepSize, int height, double intensity) {
            this.fftSize = fftSize;
            this.stepSize = stepSize;
            this.height = height;
            this.intensity = intensity;

            var window = new FftSharp.Windows.Hanning();
            hanningWindow = window.Create(fftSize);
            PcmData = new List<short>(fftSize * 4);

            var freqNyquist = sampleRate / 2;
            var hzPerPixel = (double)sampleRate / fftSize;
            fftIndexMinFreq = (minFreq == 0) ? 0 : (int)(minFreq / hzPerPixel);
            fftIndexMaxFreq = (maxFreq >= freqNyquist) ? fftSize / 2 : (int)(maxFreq / hzPerPixel);
            width = fftIndexMaxFreq - fftIndexMinFreq;

            pixels = new int[width * height];
            cutoffBand = height / 8;
            cutoffArr = new double[width * cutoffBand];
            cutoffY = 0;

            bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!);
            if(bitmap == null) {
                throw new ArgumentNullException(nameof(bitmap));
            }
            yPos = 0;

            arrPoolComplex = ArrayPool<System.Numerics.Complex>.Create();
        }

        double GetAvgLineValue(int xLine) {
            double avgValue = 0;
            var start = Math.Max(xLine - 2, 0) * cutoffBand;
            var len = Math.Min(5 * cutoffBand, cutoffArr.Length - start);
            var values = cutoffArr.AsSpan(start, len);
            foreach(var val in values) {
                avgValue += val;
            }
            return avgValue / values.Length;
        }

        public void Process(Action<Bitmap> render) {
            var fftsToProcess = (PcmData.Count - fftSize) / stepSize;

            if(fftsToProcess < 1) {
                return;
            }

            Parallel.For(0, fftsToProcess, y => {
                //for(int y = 0; y < fftsToProcess; y++) {
                var samples = arrPoolComplex.Rent(fftSize);
                int sourceIndex = y * stepSize;
                for(int x = 0; x < fftSize; x++) {
                    samples[x] = PcmData[sourceIndex + x] * hanningWindow[x];
                }

                FftSharp.FFT.Forward(samples);

                var _yPos = yPos + y;
                if(_yPos >= height) {
                    _yPos = _yPos - height;
                }

                for(int x = 0; x < width; x++) {
                    var fftVal = samples[fftIndexMinFreq + x].Magnitude / fftSize;
                    fftVal *= intensity;

                    cutoffArr[x * cutoffBand + cutoffY] = fftVal;

                    if(fftVal > GetAvgLineValue(x)) {
                        fftVal = Math.Min(fftVal, 255);
                    } else {
                        fftVal = 0;
                    }

                    var b = (byte)fftVal;
                    var alfa = (byte)0xFF;
                    pixels[x + _yPos * width] = (alfa << 24) + (b << 8);
                }

                cutoffY++;
                if(cutoffY >= cutoffBand) {
                    cutoffY = 0;
                }

                arrPoolComplex.Return(samples);
                //}
            });

            yPos += fftsToProcess;
            if(yPos >= height) {
                yPos = yPos - height;
            }

            PcmData.RemoveRange(0, fftsToProcess * stepSize);

            bitmap!.SetPixels(pixels, 0, width, 0, 0, width, height);
            render(bitmap);
        }
    }
}

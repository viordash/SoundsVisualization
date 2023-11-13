using System.Buffers;
using System.Text;
using Android.Graphics;
using Java.Security.Cert;

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
        readonly double[,] cutoffArr;
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
            cutoffArr = new double[width, height / 16];
            cutoffY = 0;

            bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!);
            if(bitmap == null) {
                throw new ArgumentNullException(nameof(bitmap));
            }
            yPos = 0;

            arrPoolComplex = ArrayPool<System.Numerics.Complex>.Create();
        }

        double GetAvgLineValue(int xLine) {
            int lowerBound = Math.Max(xLine - 2, 0);
            int upperBound = Math.Min(xLine + 2, width);

            double value = 0;
            for(int y = 0; y < cutoffArr.GetLength(1); y++) {
                for(int x = lowerBound; x < upperBound; x++) {
                    value += cutoffArr[x, y];
                }
            }
            return value / (cutoffArr.GetLength(1) * (upperBound - lowerBound));
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

                    cutoffArr[x, cutoffY] = fftVal;

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
                if(cutoffY >= cutoffArr.GetLength(1)) {
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

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

            bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!);
            if(bitmap == null) {
                throw new ArgumentNullException(nameof(bitmap));
            }
            yPos = height - 1;

            arrPoolComplex = ArrayPool<System.Numerics.Complex>.Create();
        }

        public void Process(Action<Bitmap> render) {
            var fftsToProcess = (PcmData.Count - fftSize) / stepSize;

            if(fftsToProcess < 1) {
                return;
            }

            for(int y = 0; y < fftsToProcess; y++) {
                var samples = arrPoolComplex.Rent(fftSize);
                int sourceIndex = y * stepSize;
                for(int x = 0; x < fftSize; x++) {
                    samples[x] = PcmData[sourceIndex + x] * hanningWindow[x];
                }

                FftSharp.FFT.Forward(samples);

                var _yPos = yPos - y;
                if(_yPos < 0) {
                    _yPos = height + _yPos;
                }

                for(int x = 0; x < width; x++) {
                    var fftVal = samples[fftIndexMinFreq + x].Magnitude / fftSize;
                    fftVal *= intensity;

                    fftVal = Math.Min(fftVal, 255);

                    var b = (byte)fftVal;
                    var alfa = (byte)0xFF;
                    pixels[x + _yPos * width] = (alfa << 24) + (0x07 << 16) + (b << 8) + (0x17 << 0);
                }

                arrPoolComplex.Return(samples);
            }

            yPos -= fftsToProcess;
            if(yPos < 0) {
                yPos = height + yPos;
            }

            PcmData.RemoveRange(0, fftsToProcess * stepSize);

            bitmap!.SetPixels(pixels, 0, width, 0, 0, width, height);
            render(bitmap);
        }
    }
}

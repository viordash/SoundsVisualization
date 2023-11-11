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
        int xPos;


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

            pixels = new int[width * height];

            bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!);
            if(bitmap == null) {
                throw new ArgumentNullException(nameof(bitmap));
            }
            xPos = 0;
        }

        public void Process(Action<Bitmap> render) {
            var fftsToProcess = (PcmData.Count - fftSize) / stepSize;

            if(fftsToProcess < 1) {
                return;
            }

            System.Diagnostics.Debug.WriteLine($"---- Proces:{fftsToProcess}, step:{stepSize}");

            var cols = new int[fftsToProcess][];

            for(int x = 0; x < fftsToProcess; x++) {
                var samples = new System.Numerics.Complex[fftSize];

                int sourceIndex = x * stepSize;
                for(int y = 0; y < fftSize; y++) {
                    samples[y] = PcmData[sourceIndex + y] * hanningWindow[y];
                }

                FftSharp.FFT.Forward(samples);

                var samplesWindow = samples.AsSpan(fftIndexMinFreq);

                for(int y = 0; y < height; y++) {
                    var fftVal = samplesWindow[y].Magnitude / fftSize;

                    fftVal *= intensity;
                    fftVal = Math.Min(fftVal, 255);
                    var b = (byte)fftVal;
                    var alfa = (byte)0xFF;
                    pixels[xPos + y * width] = (alfa << 24) + (b << 8);
                }
                xPos++;
                if(xPos >= width) {
                    xPos = 0;
                }
                for(int y = 0; y < height; y++) {
                    pixels[xPos + y * width] = 0;
                }
                bitmap!.SetPixels(pixels, 0, width, 0, 0, width, height);
                render(bitmap);
            }

            PcmData.RemoveRange(0, fftsToProcess * stepSize);
        }
    }
}

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
        readonly List<int[]> pixels;
        readonly Bitmap? bitmap;


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

            pixels = new List<int[]>(width);
            while(pixels.Count < width) {
                pixels.Insert(0, new int[height]);
            }

            bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!);
            if(bitmap == null) {
                throw new ArgumentNullException(nameof(bitmap));
            }
        }


        public void Process(Action<Bitmap> render) {
            var fftsToProcess = (PcmData.Count - fftSize) / stepSize;

            if(fftsToProcess < 1) {
                return;
            }

            System.Diagnostics.Debug.WriteLine($"---- Proces:{fftsToProcess}, step:{stepSize}");

            var cols = new int[fftsToProcess][];

            Parallel.For(0, fftsToProcess, col => {
                var samples = new System.Numerics.Complex[fftSize];

                int sourceIndex = col * stepSize;
                for(int i = 0; i < fftSize; i++) {
                    samples[i] = PcmData[sourceIndex + i] * hanningWindow[i];
                }

                FftSharp.FFT.Forward(samples);

                var samplesWindow = samples.AsSpan(fftIndexMinFreq);
                cols[col] = new int[height];
                for(int i = 0; i < height; i++) {
                    var fftVal = samplesWindow[i].Magnitude / fftSize;

                    fftVal *= intensity;
                    fftVal = Math.Min(fftVal, 255);
                    var b = (byte)fftVal;
                    var alfa = (byte)0xFF;
                    cols[col][i] = (alfa << 24) + (b << 8);
                }
            });
            PcmData.RemoveRange(0, fftsToProcess * stepSize);


            pixels.RemoveRange(0, cols.Length);
            foreach(var col in cols) {
                pixels.Add(col);
            }


            var arr = pixels.SelectMany(x => x).ToArray();
            bitmap!.SetPixels(arr, 0, width, 0, 0, width, height);
            render(bitmap);
        }
    }
}

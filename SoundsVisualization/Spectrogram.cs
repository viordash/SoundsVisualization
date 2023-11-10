using Android.Graphics;
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
        readonly List<int[]> pixels;


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
            pixels = new List<int[]>(width);
            while(pixels.Count < width) {
                pixels.Insert(0, new int[height]);
            }
        }


        public void Process(Action<Bitmap> render) {
            var fftsToProcess = (PcmData.Count - fftSize) / stepSize;

            if(fftsToProcess < 1) {
                return;
            }

            System.Diagnostics.Debug.WriteLine($"---- Proces:{fftsToProcess}, step:{stepSize}");
            //var newFfts = new double[fftsToProcess][];

            var cols = new int[fftsToProcess][];

            Parallel.For(0, fftsToProcess, col => {
                var samples = new System.Numerics.Complex[fftSize];

                int sourceIndex = col * stepSize;
                for(int i = 0; i < fftSize; i++) {
                    samples[i] = PcmData[sourceIndex + i] * hanningWindow[i];
                }

                FftSharp.FFT.Forward(samples);

                var samplesWindow = samples.AsSpan(fftIndexMinFreq);
                //newFfts[col] = new double[height];
                cols[col] = new int[height];
                for(int i = 0; i < height; i++) {
                    var fftVal = samplesWindow[i].Magnitude / fftSize;
                    //newFfts[col][i] = fftVal;

                    fftVal *= intensity;
                    fftVal = Math.Min(fftVal, 255);
                    var b = (byte)fftVal;
                    var alfa = (byte)0xFF;
                    cols[col][i] = (alfa << 24) + (b << 8);
                }
            });
            PcmData.RemoveRange(0, fftsToProcess * stepSize);

            //foreach(var newFft in newFfts) {
            //    FFTs.Add(newFft);
            //}


            pixels.RemoveRange(0, cols.Length);
            foreach(var col in cols) {
                pixels.Add(col);
            }

            var bmp = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!);
            if(bmp == null) {
                throw new ArgumentNullException(nameof(bmp));
            }

            var arr = pixels.SelectMany(x => x).ToArray();
            bmp.SetPixels(arr, 0, width, 0, 0, width, height);
            render(bmp);

            //PadOrTrimForFixedWidth();
            //render(GetBitmap());
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
                    pixels[bytePosition] = (alfa << 24) + (b << 8);
                }
            });

            System.Diagnostics.Debug.WriteLine($"---- GetBitmap 1: w:{w}, h:{h}");
            bmp.SetPixels(pixels, 0, stride, 0, 0, w, h);

            return bmp;
        }

    }
}

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
        readonly ArrayPool<double> arrPoolDouble;
        readonly ArrayPool<bool> arrPoolBool;
        readonly double[] cutoffArr;
        readonly int cutoffAvgWindow;
        readonly int cutoffBand;

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
            cutoffAvgWindow = height / 8;
            cutoffBand = width / 8;
            cutoffArr = new double[width / cutoffBand];

            bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!);
            if(bitmap == null) {
                throw new ArgumentNullException(nameof(bitmap));
            }
            yPos = 0;

            arrPoolComplex = ArrayPool<System.Numerics.Complex>.Create();
            arrPoolDouble = ArrayPool<double>.Create();
            arrPoolBool = ArrayPool<bool>.Create();
        }

        public void Process(Action<Bitmap> render) {
            var fftsToProcess = (PcmData.Count - fftSize) / stepSize;

            if(fftsToProcess < 1) {
                return;
            }

            //System.Diagnostics.Debug.WriteLine($"---- Proces:{fftsToProcess}, step:{stepSize}");

            double val7 = 0;

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

                var values = arrPoolDouble.Rent(width);
                var cutoff = arrPoolBool.Rent(cutoffArr.Length);
                int cutoffBandInd = 0;
                double fftAvgVal = 0.0;
                for(int x = 0; x < width; x++) {
                    var fftVal = samples[fftIndexMinFreq + x].Magnitude / fftSize;
                    fftVal *= intensity;
                    values[x] = fftVal;

                    fftAvgVal += fftVal;
                    if(cutoffBandInd != (x + 1) / cutoffBand) {

                        fftAvgVal = fftAvgVal / cutoffBand;
                        cutoffArr[cutoffBandInd] = (cutoffArr[cutoffBandInd] * cutoffAvgWindow + fftAvgVal) / (cutoffAvgWindow + 1);
                        cutoff[cutoffBandInd] = fftAvgVal < cutoffArr[cutoffBandInd];
                        cutoffBandInd = (x + 1) / cutoffBand;
                    }
                }

                for(int x = 0; x < width; x++) {
                    var fftVal = values[x];
                    if(x == width / 2) {
                        val7 += fftVal;
                    }

                    cutoffBandInd = x / cutoffBand;
                    if(cutoff[cutoffBandInd]) {
                        if(/*cutoffBandInd == (width / cutoffBand) / 2 &&*/ fftVal > 1000) {
                            //System.Diagnostics.Debug.WriteLine($"----                {x}    cutoff:{fftVal.ToString("0.##")}");
                        }
                        fftVal = 0;
                    } else {
                        fftVal = Math.Min(fftVal, 255);
                    }

                    var b = (byte)fftVal;
                    var alfa = (byte)0xFF;
                    pixels[x + _yPos * width] = (alfa << 24) + (b << 8);
                }

                arrPoolDouble.Return(values);
                arrPoolComplex.Return(samples);
            //}
            });

            //var sb = new StringBuilder();
            //foreach(var s in cutoff) {
            //    sb.Append(s.ToString("0.##"));
            //    sb.Append(", ");
            //}
            //System.Diagnostics.Debug.WriteLine($"----cutoff:{sb.ToString()}");
            val7 = val7 / fftsToProcess;
            System.Diagnostics.Debug.WriteLine($"----val7:{val7.ToString("0000.00")}, cutoff:{cutoffArr[(width / cutoffBand) / 2].ToString("0.##")}");

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

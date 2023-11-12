using System.Numerics;
using Android.Media;

namespace SoundsVisualization {
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : Activity {

        AudioRecord? audioSource;
        CheckBox? cbPause;
        int bufferSize;
        ImageView? imgSpectrogram;
        Spectrogram? spectrogram = null;


        const int sampleRateInHz = 8000;


        protected override void OnCreate(Bundle? savedInstanceState) {
            base.OnCreate(savedInstanceState);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            imgSpectrogram = FindViewById<ImageView>(Resource.Id.imgSpectrogram);

            cbPause = FindViewById<CheckBox>(Resource.Id.cbPause);
            if(cbPause != null) {
                cbPause.Click += Pause_Click;
            }

            bufferSize = AudioRecord.GetMinBufferSize(sampleRateInHz, ChannelIn.Mono, Encoding.Pcm16bit);
            if(bufferSize < 0) {
                throw new Exception("Invalid buffer size calculated; audio settings used may not be supported on this device");
            }
            audioSource = new AudioRecord(
                AudioSource.Mic,
                sampleRateInHz,
                ChannelIn.Mono,
                Encoding.Pcm16bit,
                bufferSize);

            if(audioSource.State == Android.Media.State.Uninitialized) {
                throw new Exception("Unable to successfully initialize AudioStream; reporting State.Uninitialized.  If using an emulator, make sure it has access to the system microphone.");
            }
        }

        void Pause_Click(object? sender, EventArgs e) {
            if(cbPause!.Checked == false) {
                Start();
            } else {
                Stop();
            }
        }

        void Start() {
            if(audioSource?.RecordingState == RecordState.Stopped) {
                Android.OS.Process.SetThreadPriority(Android.OS.ThreadPriority.UrgentAudio);

                System.Diagnostics.Debug.WriteLine($"imgSpectrogram: w:{imgSpectrogram!.Width}, h:{imgSpectrogram!.Height}");

                imgSpectrogram!.SetScaleType(ImageView.ScaleType.FitXy);

                int fftSize = (int)BitOperations.RoundUpToPowerOf2((uint)bufferSize * 2);

                int stepSize = fftSize / 20;
                spectrogram = spectrogram ?? new Spectrogram(sampleRateInHz, minFreq: 400, maxFreq: 4000, fftSize: fftSize, stepSize: stepSize,
                        height: imgSpectrogram!.Height, intensity: 4);
                audioSource?.StartRecording();
                Task.Run(() => Record());
            }
        }

        void Stop() {
            if(audioSource?.RecordingState == RecordState.Recording) {
                audioSource?.Stop();
                //audioSource?.Release();
            }
        }

        void Record() {
            var pcm = new short[bufferSize];
            int readFailureCount = 0;
            int readResult = 0;

            System.Diagnostics.Debug.WriteLine("AudioStream.Record(): Starting background loop to read audio stream");

            while(audioSource?.RecordingState == RecordState.Recording) {
                try {
                    // not sure if this is even a good idea, but we'll try to allow a single bad read, and past that shut it down
                    if(readFailureCount > 1) {
                        System.Diagnostics.Debug.WriteLine("AudioStream.Record(): Multiple read failures detected, stopping stream");
                        Stop();
                        break;
                    }

                    readResult = audioSource.Read(pcm, 0, pcm.Length, 0); // this can block if there are no bytes to read

                    if(readResult > 0) {
                        readFailureCount = 0;
                        //System.Diagnostics.Debug.WriteLine($"---- readResult:{readResult}");
                        spectrogram!.PcmData.AddRange(pcm);
                        spectrogram!.Process(bmp => {
                            RunOnUiThread(() => {
                                //System.Diagnostics.Debug.WriteLine($"---- OnRenderTimer: {bmp}");
                                imgSpectrogram!.SetImageBitmap(bmp);
                            });
                        });
                    } else {
                        switch(readResult) {
                            case (int)TrackStatus.ErrorInvalidOperation:
                            case (int)TrackStatus.ErrorBadValue:
                            case (int)TrackStatus.ErrorDeadObject:
                                System.Diagnostics.Debug.WriteLine("AudioStream.Record(): readResult returned error code: {0}", readResult);
                                Stop();
                                break;
                            default:
                                readFailureCount++;
                                System.Diagnostics.Debug.WriteLine("AudioStream.Record(): readResult returned error code: {0}", readResult);
                                break;
                        }
                    }
                } catch(Exception ex) {
                    readFailureCount++;

                    System.Diagnostics.Debug.WriteLine("Error in Android AudioStream.Record(): {0}", ex.Message);

                    //OnException?.Invoke(this, ex);
                }
            }
        }
    }
}
using System.Formats.Tar;
using Android.Accounts;
using Android.Graphics;
using Android.Media;
using Android.Widget;
using Spectrogram;
using static Android.Bluetooth.BluetoothClass;
using static Android.Widget.GridLayout;

namespace SoundsVisualization {
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : Activity {

        AudioRecord? audioSource;
        CheckBox? cbPause;
        int bufferSize;
        SpectrogramGenerator? spectrogramGenerator;
        ImageView? imgSpectrogram;
        Timer? tmrRender;

        protected override void OnCreate(Bundle? savedInstanceState) {
            base.OnCreate(savedInstanceState);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            imgSpectrogram = FindViewById<ImageView>(Resource.Id.imgSpectrogram);

            cbPause = FindViewById<CheckBox>(Resource.Id.cbPause);
            if(cbPause != null) {
                cbPause.Click += Pause_Click;
            }

            const int sampleRateInHz = 8000;
            const int fftSize = 1024;
            bufferSize = AudioRecord.GetMinBufferSize(sampleRateInHz, ChannelIn.Mono, Encoding.Pcm16bit) / 4;

            if(bufferSize < 0) {
                throw new Exception("Invalid buffer size calculated; audio settings used may not be supported on this device");
            }
            audioSource = new AudioRecord(
                AudioSource.Mic,
                sampleRateInHz,
                ChannelIn.Mono,
                Encoding.PcmFloat,
                bufferSize);

            if(audioSource.State == Android.Media.State.Uninitialized) {
                throw new Exception("Unable to successfully initialize AudioStream; reporting State.Uninitialized.  If using an emulator, make sure it has access to the system microphone.");
            }

            spectrogramGenerator = new SpectrogramGenerator(sampleRateInHz, fftSize: fftSize, stepSize: fftSize / 20);
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
                audioSource?.StartRecording();
                //ImageView.SetImageResource(Resource.Drawable.Icon);
                Task.Run(() => Record());
            }
        }

        void Stop() {
            tmrRender?.Dispose();
            tmrRender = null;
            if(audioSource?.RecordingState == RecordState.Recording) {
                audioSource?.Stop();
                //audioSource?.Release();
            }
        }

        void Record() {
            var audio = new float[bufferSize];
            int readFailureCount = 0;
            int readResult = 0;

            System.Diagnostics.Debug.WriteLine("AudioStream.Record(): Starting background loop to read audio stream");

            //tmrRender = new Timer(OnRenderTimer, null, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200));
            while(audioSource?.RecordingState == RecordState.Recording) {
                try {
                    // not sure if this is even a good idea, but we'll try to allow a single bad read, and past that shut it down
                    if(readFailureCount > 1) {
                        System.Diagnostics.Debug.WriteLine("AudioStream.Record(): Multiple read failures detected, stopping stream");
                        Stop();
                        break;
                    }

                    readResult = audioSource.Read(audio, 0, bufferSize, 0); // this can block if there are no bytes to read

                    // readResult should == the # bytes read, except a few special cases
                    if(readResult > 0) {
                        readFailureCount = 0;
                        System.Diagnostics.Debug.WriteLine($"---- readResult:{readResult}");

                        var ddd = audio.Select(x => (double)x * 1000000.0).ToList();
                        spectrogramGenerator!.Add(ddd, false);

                        if(spectrogramGenerator!.FftsToProcess > 0) {
                            spectrogramGenerator!.Process();
                            spectrogramGenerator.SetFixedWidth(imgSpectrogram!.Width);
                            var bmp = spectrogramGenerator!.GetBitmap(intensity: 4, rotate: true);
                            System.Diagnostics.Debug.WriteLine($"---- OnRenderTimer: {bmp}");
                            RunOnUiThread(() => {
                                imgSpectrogram!.SetImageBitmap(bmp);
                            });
                        }


                    } else {
                        switch(readResult) {
                            case (int)TrackStatus.ErrorInvalidOperation:
                            case (int)TrackStatus.ErrorBadValue:
                            case (int)TrackStatus.ErrorDeadObject:
                                System.Diagnostics.Debug.WriteLine("AudioStream.Record(): readResult returned error code: {0}", readResult);
                                Stop();
                                break;
                            //case (int)TrackStatus.Error:
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

        public void OnRenderTimer(object? stateInfo) {
            //lock(this) {
            //    if(spectrogramGenerator!.FftsToProcess == 0) {
            //        return;
            //    }
            //    spectrogramGenerator!.Process();
            //    spectrogramGenerator.SetFixedWidth(imgSpectrogram!.Width);
            //    var bmp = spectrogramGenerator!.GetBitmap(intensity: 0.4);
            //    System.Diagnostics.Debug.WriteLine($"---- OnRenderTimer: {bmp}");
            //    RunOnUiThread(() => {
            //        imgSpectrogram!.SetImageBitmap(bmp);
            //    });
            //}

        }
    }
}
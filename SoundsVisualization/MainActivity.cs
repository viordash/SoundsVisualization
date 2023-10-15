namespace SoundsVisualization {
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : Activity {

        CheckBox? cbPause;

        protected override void OnCreate(Bundle? savedInstanceState) {
            base.OnCreate(savedInstanceState);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            cbPause = FindViewById<CheckBox>(Resource.Id.cbPause);
            if(cbPause != null) {
                cbPause.Click += Pause_Click;
            }
        }

        void Pause_Click(object? sender, EventArgs e) {
            //await RecordAudio();
        }
    }
}
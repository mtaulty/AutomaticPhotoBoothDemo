namespace AutomaticBooth
{
  using PhotoControlLibrary;
  using System;
  using System.Threading.Tasks;
  using Windows.Foundation;
  using Windows.Graphics.Imaging;
  using Windows.Media.SpeechRecognition;
  using Windows.UI.Xaml.Controls;

  public sealed partial class MainPage : Page, IPhotoControlHandler
  {
    #region ALREADY_SEEN_THIS_CODE
    public MainPage()
    {
      this.InitializeComponent();
      this.Loaded += OnLoaded;
    }
    async void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      // this won't work, it's just to get us to compile.
      await this.photoControl.InitialiseAsync(this);
    }

    public async Task OnOpeningPhotoAsync(Guid photo)
    {
      this.currentPhotoId = photo;
    }
    public async Task OnClosingPhotoAsync(Guid photo)
    {
    }
    public async Task<Rect?> ProcessCameraFrameAsync(SoftwareBitmap bitmap)
    {
      return (null);
    }
    public async Task<bool> AuthoriseUseAsync()
    {
      return (true);
    }
    Guid currentPhotoId;
    #endregion // ALREADY_SEEN_THIS_CODE
    public async Task OnModeChangedAsync(PhotoControlMode newMode)
    {
      switch (newMode)
      {
        case PhotoControlMode.Unauthorised:
          break;
        case PhotoControlMode.Grid:
          break;
        case PhotoControlMode.Capture:
          await this.StartListeningForCheeseAsync();
          break;
        default:
          break;
      }
    }

    async Task StartListeningForCheeseAsync()
    {
      await this.StartListeningForConstraintAsync(
        new SpeechRecognitionListConstraint(new string[] { "cheese" }));
    }
    async Task StartListeningForConstraintAsync(
      ISpeechRecognitionConstraint constraint)
    {
      if (this.speechRecognizer == null)
      {
        this.speechRecognizer = new SpeechRecognizer();

        this.speechRecognizer.ContinuousRecognitionSession.ResultGenerated 
          += OnSpeechResult;
      }
      else
      {
        await this.speechRecognizer.ContinuousRecognitionSession.StopAsync();
      }
      this.speechRecognizer.Constraints.Clear();

      this.speechRecognizer.Constraints.Add(constraint);

      await this.speechRecognizer.CompileConstraintsAsync();

      await this.speechRecognizer.ContinuousRecognitionSession.StartAsync();
    }

    async void OnSpeechResult(
      SpeechContinuousRecognitionSession sender,
     SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
      if ((args.Result.Confidence == SpeechRecognitionConfidence.High) ||
          (args.Result.Confidence == SpeechRecognitionConfidence.Medium))
      {
        if (args.Result.Text.ToLower() == "cheese")
        {
          await this.Dispatcher.RunAsync(
            Windows.UI.Core.CoreDispatcherPriority.Normal,
            async () =>
            {
              var photoResult = await this.photoControl.TakePhotoAsync();

              if (photoResult != null)
              {
              }
            }
          );
        }
      }
    }
    SpeechRecognizer speechRecognizer;   
  }
}

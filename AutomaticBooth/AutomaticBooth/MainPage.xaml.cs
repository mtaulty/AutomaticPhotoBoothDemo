namespace AutomaticBooth
{
  using Microsoft.ProjectOxford.Emotion;
  using Microsoft.ProjectOxford.Face;
  using PhotoControlLibrary;
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Reflection;
  using System.Threading.Tasks;
  using Windows.Foundation;
  using Windows.Graphics.Imaging;
  using Windows.Media.FaceAnalysis;
  using Windows.Media.SpeechRecognition;
  using Windows.Media.SpeechSynthesis;
  using Windows.Storage;
  using Windows.UI;
  using Windows.UI.Xaml.Controls;
  using Windows.UI.Xaml.Input;
  using Windows.UI.Xaml.Media;

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

      var filter = ((App)App.Current).WaitingFilter;

      if (!string.IsNullOrEmpty(filter))
      {
        await this.photoControl.ShowFilteredGridAsync(filter);
      }
    }
    public async Task<bool> AuthoriseUseAsync()
    {
      return (true);
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
    public async Task OnModeChangedAsync(PhotoControlMode newMode)
    {
      switch (newMode)
      {
        case PhotoControlMode.Unauthorised:
          break;
        case PhotoControlMode.Grid:
          await this.StartListeningForFiltersAsync();
          break;
        case PhotoControlMode.Capture:
          await this.StartListeningForCheeseAsync();
          break;
        default:
          break;
      }
    }
    async Task StartListeningForFiltersAsync()
    {
      var grammarFile =
        await StorageFile.GetFileFromApplicationUriAsync(
          new Uri("ms-appx:///grammar.xml"));

      await this.StartListeningForConstraintAsync(
        new SpeechRecognitionGrammarFileConstraint(grammarFile));
    }
    public async Task OnOpeningPhotoAsync(Guid photo)
    {
      this.currentPhotoId = photo;
      this.AddLayerForManipulations();
    }
    public async Task OnClosingPhotoAsync(Guid photo)
    {
      this.RemoveLayerForManipulations();
    }

    void AddLayerForManipulations()
    {
      this.overlayGrid = new Grid()
      {
        ManipulationMode =
          ManipulationModes.Rotate |
          ManipulationModes.Scale,
        Background = new SolidColorBrush(Colors.Transparent)
      };

      this.overlayGrid.ManipulationDelta += OnManipulationDelta;

      this.photoControl.AddOverlayToDisplayedPhoto(this.overlayGrid);
    }
    void RemoveLayerForManipulations()
    {
      this.overlayGrid.ManipulationDelta -= this.OnManipulationDelta;
      this.photoControl.RemoveOverlayFromDisplayedPhoto(this.overlayGrid);
      this.overlayGrid = null;
    }
    void OnManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
      this.photoControl.UpdatePhotoTransform(e.Delta);
    }
    async Task SpeakAsync(string text)
    {
      // Note, the assumption here is very much that we speak one piece of
      // text at a time rather than have multiple in flight - that needs
      // a different solution (with a queue).
      await Dispatcher.RunAsync(
        Windows.UI.Core.CoreDispatcherPriority.Normal,
        async () =>
        {
          // Create the synthesizer if we need to.
          if (this.speechSynthesizer == null)
          {
            // Easy create, just choosing first female voice.
            this.speechSynthesizer = new SpeechSynthesizer()
            {
              Voice = SpeechSynthesizer.AllVoices.Where(
                v => v.Gender == VoiceGender.Female).First()
            };

            // Make a media element to play the speech.
            this.mediaElementForSpeech = new MediaElement();

            // When the media ends, get rid of stream.
            this.mediaElementForSpeech.MediaEnded += (s, e) =>
            {
              this.speechMediaStream?.Dispose();
              this.speechMediaStream = null;
            };
          }
          // Now, turn the text into speech.
          this.speechMediaStream =
            await this.speechSynthesizer.SynthesizeTextToStreamAsync(text);

          this.mediaElementForSpeech.SetSource(this.speechMediaStream, string.Empty);

          // Speak it.
          this.mediaElementForSpeech.Play();
        }
      );
    }
    async Task AddEmotionBasedTagsToPhotoAsync(PhotoResult photoResult)
    {
      // The proxy that makes it easier to call the REST API.
      // Note - I'll be invalidating this key after I publish to github
      // so please get your own key from 
      // https://www.microsoft.com/cognitive-services/ 
      // to make this code work.
      EmotionServiceClient client = new EmotionServiceClient(
        "5fa514a9e129412f9848efef5ee1e444");

      // Open the photo file we just captured.
      using (var stream = await photoResult.PhotoFile.OpenStreamForReadAsync())
      {
        // Call the cloud looking for emotions.
        var results = await client.RecognizeAsync(stream);

        // We're only taking the first result here.
        var scores = results?.FirstOrDefault()?.Scores;

        if (scores != null)
        {
          // This object has properties called Sadness, Happiness,
          // Fear, etc. all with floating point values 0..1
          var publicProperties = scores.GetType().GetRuntimeProperties();

          // We'll have any property with a score > 0.5f.
          var automaticTags =
            publicProperties
              .Where(
                property => (float)property.GetValue(scores) > 0.5)
              .Select(
                property => property.Name)
              .ToList();

          if (automaticTags.Count > 0)
          {
            // Add them to our photo!
            await this.photoControl.AddTagsToPhotoAsync(
              photoResult.PhotoId,
              automaticTags);
          }
        }
      }
    }
    async Task AddFaceBasedTagsToPhotoAsync(PhotoResult photoResult)
    {
      // Note - I'll be invalidating this key after I publish to github
      // so please get your own key from 
      // https://www.microsoft.com/cognitive-services/ 
      // to make this code work.
      FaceServiceClient client = new FaceServiceClient(
        "76811b8d4dd64b9bb54366502b0615cc");

      using (var stream = await photoResult.PhotoFile.OpenStreamForReadAsync())
      {
        var attributes = new FaceAttributeType[]
        {
          FaceAttributeType.Age,
          FaceAttributeType.FacialHair,
          FaceAttributeType.Gender,
          FaceAttributeType.Glasses,
          FaceAttributeType.Smile
        };
        var results = await client.DetectAsync(stream, true, false, attributes);

        var firstFace = results?.FirstOrDefault();

        if (firstFace != null)
        {
          var automaticTags = new List<string>();
          automaticTags.Add($"age {firstFace.FaceAttributes.Age}");
          automaticTags.Add(firstFace.FaceAttributes.Gender.ToString());
          automaticTags.Add(firstFace.FaceAttributes.Glasses.ToString());

          Action<double, string> compareFunc =
            (double value, string name) =>
            {
              if (value > 0.5) automaticTags.Add(name);
            };

          compareFunc(firstFace.FaceAttributes.Smile, "smile");
          compareFunc(firstFace.FaceAttributes.FacialHair.Beard, "beard");
          compareFunc(firstFace.FaceAttributes.FacialHair.Moustache, "moustache");
          compareFunc(firstFace.FaceAttributes.FacialHair.Sideburns, "sideburns");

          await this.photoControl.AddTagsToPhotoAsync(
            photoResult.PhotoId, automaticTags);
        }
      }
    }

    async void OnSpeechResult(
      SpeechContinuousRecognitionSession sender,
      SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
      if ((args.Result.Confidence == SpeechRecognitionConfidence.High) ||
          (args.Result.Confidence == SpeechRecognitionConfidence.Medium))
      {
        if (args.Result?.RulePath?.FirstOrDefault() == "filter")
        {
          var filter =
            args.Result.SemanticInterpretation.Properties["emotion"].FirstOrDefault();

          if (!string.IsNullOrEmpty(filter))
          {
            await this.Dispatcher.RunAsync(
              Windows.UI.Core.CoreDispatcherPriority.Normal,
              async () =>
              {
                await this.photoControl.ShowFilteredGridAsync(filter);
              }
            );
          }
        }
        else if (args.Result.Text.ToLower() == "cheese")
        {
          await this.Dispatcher.RunAsync(
            Windows.UI.Core.CoreDispatcherPriority.Normal,
            async () =>
            {
              var photoResult = await this.photoControl.TakePhotoAsync();

              if (photoResult != null)
              {
                await this.AddFaceBasedTagsToPhotoAsync(photoResult);
                await this.AddEmotionBasedTagsToPhotoAsync(photoResult);
                await this.SpeakAsync("That's lovely, you look great!");
              }
            }
          );
        }
      }
    }
    SpeechSynthesisStream speechMediaStream;
    MediaElement mediaElementForSpeech;
    SpeechSynthesizer speechSynthesizer;
    Grid overlayGrid;
    SpeechRecognizer speechRecognizer;
    Guid currentPhotoId;
    #endregion // ALREADY_SEEN_THIS_CODE

    public async Task<Rect?> ProcessCameraFrameAsync(SoftwareBitmap bitmap)
    {
      if (this.faceDetector == null)
      {
        this.faceDetector = await FaceDetector.CreateAsync();
      }
      var result = await this.faceDetector.DetectFacesAsync(bitmap);

      this.photoControl.Switch(result?.Count > 0);

      Rect? returnValue = null;

      if (result?.Count > 0)
      {
        returnValue = new Rect(
          (double)result[0].FaceBox.X / bitmap.PixelWidth,
          (double)result[0].FaceBox.Y / bitmap.PixelHeight,
          (double)result[0].FaceBox.Width / bitmap.PixelWidth,
          (double)result[0].FaceBox.Height / bitmap.PixelHeight);
      }
      return (returnValue);
    }
    FaceDetector faceDetector;
  }
}

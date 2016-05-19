namespace AutomaticBooth
{
  using CognitiveAPIWrapper.Audio;
  using CognitiveAPIWrapper.SpeakerVerification;
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
  using Windows.UI.Input.Inking;
  using Windows.UI.Popups;
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
    void AddLayerForManipulations()
    {
      this.inkOverlay = new InkCanvas()
      {
        ManipulationMode =
          ManipulationModes.Rotate |
          ManipulationModes.Scale
      };

      this.inkOverlay.ManipulationDelta += OnManipulationDelta;

      var presentation = this.inkOverlay.InkPresenter.CopyDefaultDrawingAttributes();
      presentation.Color = Colors.Yellow;
      presentation.Size = new Size(2, 2);
      this.inkOverlay.InkPresenter.UpdateDefaultDrawingAttributes(presentation);

      this.inkOverlay.InkPresenter.StrokesCollected += OnStrokesCollected;

      this.photoControl.AddOverlayToDisplayedPhoto(this.inkOverlay);
    }

    async void OnStrokesCollected(
      InkPresenter sender,
      InkStrokesCollectedEventArgs args)
    {
      // create the ink recognizer if we haven't done already.
      if (this.inkRecognizer == null)
      {
        this.inkRecognizer = new InkRecognizerContainer();
      }
      // recognise the ink which has not already been recognised
      // (i.e. do incremental ink recognition).
      var results = await this.inkRecognizer.RecognizeAsync(
        sender.StrokeContainer,
        InkRecognitionTarget.Recent);

      // update the container so that it knows next time that this
      // ink is already recognised.
      sender.StrokeContainer.UpdateRecognitionResults(results);

      // we take all the top results that the recogniser gives us
      // back.
      var newTags = results.Select(
        result => result.GetTextCandidates().FirstOrDefault());

      // add the new tags to our photo.
      await this.photoControl.AddTagsToPhotoAsync(this.currentPhotoId, newTags);
    }
    void RemoveLayerForManipulations()
    {
      this.inkOverlay.ManipulationDelta -= this.OnManipulationDelta;
      this.photoControl.RemoveOverlayFromDisplayedPhoto(this.inkOverlay);
      this.inkOverlay = null;
    }
    async Task SaveInkToFileAsync()
    {
      if (this.inkOverlay.InkPresenter.StrokeContainer.GetStrokes().Count > 0)
      {
        var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
          this.InkStorageFileName, CreationCollisionOption.ReplaceExisting);

        using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
        {
          await this.inkOverlay.InkPresenter.StrokeContainer.SaveAsync(stream);
        }
      }
    }
    async Task LoadInkFromFileAsync()
    {
      try
      {
        var file = await ApplicationData.Current.LocalFolder.GetFileAsync(
          this.InkStorageFileName);

        using (var stream = await file.OpenReadAsync())
        {
          await this.inkOverlay.InkPresenter.StrokeContainer.LoadAsync(stream);
        }
      }
      catch (FileNotFoundException)
      {

      }
    }
    public async Task OnOpeningPhotoAsync(Guid photo)
    {
      this.currentPhotoId = photo;
      this.AddLayerForManipulations();
      await this.LoadInkFromFileAsync();
    }
    public async Task OnClosingPhotoAsync(Guid photo)
    {
      await this.SaveInkToFileAsync();
      this.RemoveLayerForManipulations();
    }
    string InkStorageFileName => $"{this.currentPhotoId}.ink";
    FaceDetector faceDetector;
    SpeechSynthesisStream speechMediaStream;
    MediaElement mediaElementForSpeech;
    SpeechSynthesizer speechSynthesizer;
    SpeechRecognizer speechRecognizer;
    Guid currentPhotoId;
    InkCanvas inkOverlay;
    InkRecognizerContainer inkRecognizer;
    #endregion // ALREADY_SEEN_THIS_CODE

    public async Task<bool> AuthoriseUseAsync()
    {
      // We ask the user to repeat a phrase.
      var dialog = new MessageDialog(
        "dismiss this dialog then repeat the verification phrase\n" +
        "'my voice is my passport, verify me'",
        "voice verification required");

      await dialog.ShowAsync();

      // Note - I'll be invalidating this key after I publish to github
      // so please get your own key from 
      // https://www.microsoft.com/cognitive-services/ 
      // to make this code work.
      VerificationClient verificationClient =
        new VerificationClient("8bfa384b6ca64ded9069a07f3c60510f");

      // Record me speaking for 5 seconds, using class from my own
      // library which sits on AudioGraph APIs.
      var speechFile =
        await CognitiveAudioGraphRecorder.RecordToTemporaryFileAsync(
          TimeSpan.FromSeconds(5));

      // I have already registered my voice speaking this phrase with
      // the cloud and I got back this GUID. I had to repeat it 3 
      // times.
      // You would need to do similar on your Cognitive Service 
      // account to set this up.
      // If you look in my Cognitive Wrapper project on github,
      // you'll find some simple test clients that help you do that.
      // https://github.com/mtaulty/CognitiveSpeechWrapper
      var verificationResult =
        await verificationClient.VerifyRecordedSpeechForProfileIdAsync(
          Guid.Parse("7cf0f0a8-c6a2-4fe0-b806-5272f89e00c8"),
          speechFile);

      // did it work?
      var authorised =
        (verificationResult.Result == VerificationStatus.Accept) &&
        (verificationResult.Confidence == Confidence.High);

      return (authorised);
    }
  }
}

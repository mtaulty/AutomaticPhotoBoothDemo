namespace AutomaticBooth
{
  using System;
  using Windows.ApplicationModel.Activation;
  using Windows.ApplicationModel.VoiceCommands;
  using Windows.Storage;
  using Windows.UI.Xaml;
  using Windows.UI.Xaml.Controls;

  sealed partial class App : Application
  {
    public App()
    {
      this.InitializeComponent();
    }

    protected async override void OnLaunched(LaunchActivatedEventArgs e)
    {
      var storageFile =
        await StorageFile.GetFileFromApplicationUriAsync(
          new Uri("ms-appx:///commands.xml"));

      await VoiceCommandDefinitionManager.InstallCommandDefinitionsFromStorageFileAsync(
        storageFile);

      EnsureUICreated(e.PrelaunchActivated);
    }
    private static void EnsureUICreated(bool prelaunchActivated)
    {
      Frame rootFrame = Window.Current.Content as Frame;

      // Do not repeat app initialization when the Window already has content,
      // just ensure that the window is active
      if (rootFrame == null)
      {
        // Create a Frame to act as the navigation context and navigate to the first page
        rootFrame = new Frame();

        // Place the frame in the current Window
        Window.Current.Content = rootFrame;
      }

      if (prelaunchActivated == false)
      {
        if (rootFrame.Content == null)
        {
          // When the navigation stack isn't restored navigate to the first page,
          // configuring the new page by passing required information as a navigation
          // parameter
          rootFrame.Navigate(typeof(MainPage), null);
        }
        // Ensure the current window is active
        Window.Current.Activate();
      }
    }

    protected override void OnActivated(IActivatedEventArgs args)
    {
      base.OnActivated(args);

      if (args.Kind == ActivationKind.VoiceCommand)
      {
        // NB: We are not really coping with the scenario where the app is already
        // on the screen (we should, it just takes a bit more code).
        VoiceCommandActivatedEventArgs voiceArgs = (VoiceCommandActivatedEventArgs)args;

        var properties = voiceArgs.Result?.SemanticInterpretation?.Properties;
        var dictation = properties?["dictatedSearchTerms"];

        if (dictation != null)
        {
          this.WaitingFilter = dictation[0];
        }
      }
      EnsureUICreated(false);
    }
    public string WaitingFilter { get; set; }
  }
}

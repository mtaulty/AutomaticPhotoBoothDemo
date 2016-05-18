﻿namespace AutomaticBooth
{
  using Windows.ApplicationModel.Activation;
  using Windows.UI.Xaml;
  using Windows.UI.Xaml.Controls;

  sealed partial class App : Application
  {
    public App()
    {
      this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs e)
    {
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
  }
}

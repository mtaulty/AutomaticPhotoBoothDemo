namespace AutomaticBooth
{
  using PhotoControlLibrary;
  using System;
  using System.Threading.Tasks;
  using Windows.Foundation;
  using Windows.Graphics.Imaging;
  using Windows.UI.Xaml.Controls;

  public sealed partial class MainPage : Page, IPhotoControlHandler
  {
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
    public async Task OnModeChangedAsync(PhotoControlMode newMode)
    {
      switch (newMode)
      {
        case PhotoControlMode.Unauthorised:
          break;
        case PhotoControlMode.Grid:
          break;
        case PhotoControlMode.Capture:
          break;
        default:
          break;
      }
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
  }
}

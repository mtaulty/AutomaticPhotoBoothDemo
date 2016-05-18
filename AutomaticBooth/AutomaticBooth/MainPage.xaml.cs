using Windows.UI.Xaml.Controls;

namespace AutomaticBooth
{
  public sealed partial class MainPage : Page
  {
    public MainPage()
    {
      this.InitializeComponent();
      this.Loaded += OnLoaded;
    }
    async void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      // this won't work, it's just to get us to compile.
      await this.photoControl.InitialiseAsync(null);
    }
  }
}

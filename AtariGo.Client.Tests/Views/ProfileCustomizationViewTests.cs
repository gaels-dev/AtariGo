using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using AtariGo.Client.Converters;
using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;
using AtariGo.Client.Views;

namespace AtariGo.Client.Tests.Views;

[TestClass]
[TestCategory("Unit")]
public sealed class ProfileCustomizationViewTests
{
    [STATestMethod]
    public void View_InitializesAndRendersAvatarChoices()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        using ProfileCustomizationViewModel model = new ProfileCustomizationViewModel(
            navigation, new StubProfileClient(), IgnoreName);
        ProfileCustomizationView view = new ProfileCustomizationView();
        view.DataContext = model;

        // Act
        view.Measure(new Size(1000, 700));
        view.Arrange(new Rect(0, 0, 1000, 700));
        view.UpdateLayout();

        // Assert
        Assert.IsTrue(view.IsInitialized);
        IReadOnlyList<Image> images = FindImages(view);
        Assert.HasCount(5, images);
        HashSet<string> loadedResources = new HashSet<string>();
        foreach (Image image in images)
        {
            Assert.IsInstanceOfType<BitmapImage>(image.Source);
            BitmapImage bitmap = (BitmapImage)image.Source;
            Assert.IsGreaterThan(0, bitmap.PixelWidth);
            Assert.IsGreaterThan(0, bitmap.PixelHeight);
            loadedResources.Add(bitmap.UriSource.AbsoluteUri);
        }
        foreach (int avatarId in model.AvailableAvatars)
        {
            Assert.IsTrue(loadedResources.Any(resource => resource.EndsWith(
                $"avatar_{avatarId}.png", StringComparison.OrdinalIgnoreCase)));
        }
    }

    [STATestMethod]
    public void Converter_InvalidAvatarId_LoadsDefaultImage()
    {
        // Arrange
        AvatarIdToImageConverter converter = new AvatarIdToImageConverter();

        // Act
        BitmapImage image = (BitmapImage)converter.Convert(99,
            typeof(BitmapImage), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsGreaterThan(0, image.PixelWidth);
        StringAssert.EndsWith(image.UriSource.AbsoluteUri, "avatar_1.png");
    }

    private static IReadOnlyList<Image> FindImages(DependencyObject parent)
    {
        List<Image> images = new List<Image>();
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is Image image)
            {
                images.Add(image);
            }
            images.AddRange(FindImages(child));
        }
        return images;
    }

    private static void IgnoreName(string playerName)
    {
    }
}

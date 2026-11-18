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
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Views;

[TestClass]
[TestCategory("Unit")]
public sealed class ProfileCustomizationViewTests
{
    [STATestMethod]
    public void View_DefaultProfile_RendersAvatarChoices()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        using ProfileCustomizationViewModel model = new ProfileCustomizationViewModel(
            navigation, new StubProfileClient(), IgnoreProfile);
        ProfileCustomizationView view = new ProfileCustomizationView();
        view.DataContext = model;

        // Act
        view.Measure(new Size(1000, 700));
        view.Arrange(new Rect(0, 0, 1000, 700));
        view.UpdateLayout();

        // Assert
        Assert.IsTrue(view.IsInitialized);
        IReadOnlyList<Image> images = FindImages(view)
            .Where(IsOutsideCollapsedOverlay)
            .ToArray();
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
        foreach (AvatarOptionViewModel option in model.FeaturedAvatars)
        {
            Assert.IsTrue(loadedResources.Any(resource => resource.EndsWith(
                $"avatar_{option.AvatarId}.png", StringComparison.OrdinalIgnoreCase)));
        }
    }

    [STATestMethod]
    public async Task View_ExpandedPicker_RendersAllAvatarResourcesAsync()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        using ProfileCustomizationViewModel model = new ProfileCustomizationViewModel(
            navigation, new StubProfileClient(), IgnoreProfile);
        ProfileCustomizationView view = new ProfileCustomizationView
        {
            DataContext = model
        };
        await CommandExecution.ExecuteAsync(model.LoadCommand, null);
        model.OpenAvatarPickerCommand.Execute(null);

        // Act
        view.Measure(new Size(1000, 700));
        view.Arrange(new Rect(0, 0, 1000, 700));
        view.UpdateLayout();
        IReadOnlyList<Image> images = FindImages(view);

        // Assert
        Assert.IsTrue(model.IsAvatarPickerOpen);
        HashSet<string> loadedResources = images
            .OfType<Image>()
            .Select(image => image.Source)
            .OfType<BitmapImage>()
            .Select(image => image.UriSource.AbsoluteUri)
            .ToHashSet();
        foreach (AvatarOptionViewModel option in model.AvailableAvatars)
        {
            Assert.IsTrue(loadedResources.Any(resource => resource.EndsWith(
                $"avatar_{option.AvatarId}.png", StringComparison.OrdinalIgnoreCase)));
        }
    }

    [STATestMethod]
    public void View_ActionButtons_UseSharedRoundedStyle()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        using ProfileCustomizationViewModel model = new ProfileCustomizationViewModel(
            navigation, new StubProfileClient(), IgnoreProfile);
        ProfileCustomizationView view = new ProfileCustomizationView
        {
            DataContext = model
        };
        Style roundedStyle = new Style(typeof(Button));
        view.Resources["RoundedButtonStyle"] = roundedStyle;

        // Act
        view.Measure(new Size(1000, 700));
        view.Arrange(new Rect(0, 0, 1000, 700));
        view.UpdateLayout();
        IReadOnlyList<Button> buttons = FindButtons(view);

        // Assert
        Assert.IsGreaterThanOrEqualTo(7, buttons.Count);
        foreach (Button button in buttons)
        {
            Assert.AreSame(roundedStyle, button.Style);
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

    private static IReadOnlyList<Button> FindButtons(DependencyObject parent)
    {
        List<Button> buttons = new List<Button>();
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is Button button)
            {
                buttons.Add(button);
            }
            buttons.AddRange(FindButtons(child));
        }
        return buttons;
    }

    private static bool IsOutsideCollapsedOverlay(Image image)
    {
        DependencyObject? current = image;
        while (current is not null)
        {
            if (current is UIElement element && element.Visibility == Visibility.Collapsed)
            {
                return false;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return true;
    }

    private static void IgnoreProfile(UserProfile profile)
    {
    }
}

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace AtariGo.Client.Converters;

public sealed class AvatarIdToImageConverter : IValueConverter
{
    public const int DefaultAvatarId = 1;
    public const int MaximumAvatarId = 20;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int avatarId = DefaultAvatarId;
        if (value is int requestedId
            && requestedId >= DefaultAvatarId
            && requestedId <= MaximumAvatarId)
        {
            avatarId = requestedId;
        }
        BitmapImage? selectedImage = TryLoadAvatar(avatarId);
        if (selectedImage is not null)
        {
            return selectedImage;
        }
        if (avatarId != DefaultAvatarId)
        {
            BitmapImage? defaultImage = TryLoadAvatar(DefaultAvatarId);
            if (defaultImage is not null)
            {
                return defaultImage;
            }
        }
        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static BitmapImage? TryLoadAvatar(int avatarId)
    {
        Uri imageUri = new Uri(
            $"pack://application:,,,/AtariGo.Client;component/Assets/Avatars/avatar_{avatarId}.png",
            UriKind.Absolute);
        try
        {
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = imageUri;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (IOException exception)
        {
            LogAvatarFailure(avatarId, exception);
        }
        catch (FormatException exception)
        {
            LogAvatarFailure(avatarId, exception);
        }
        catch (NotSupportedException exception)
        {
            LogAvatarFailure(avatarId, exception);
        }
        return null;
    }

    private static void LogAvatarFailure(int avatarId, Exception exception)
    {
        Trace.TraceWarning("Avatar {0} could not be loaded: {1}.",
            avatarId, exception.GetType().Name);
    }
}

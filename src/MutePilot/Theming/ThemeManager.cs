using System.Windows;
using MutePilot.Settings;

namespace MutePilot.Theming;

public static class ThemeManager
{
    private const string ThemeResourcePrefix = "Themes/Theme.";

    public static void Apply(AppTheme theme)
    {
        var resources = Application.Current.Resources.MergedDictionaries;
        var currentTheme = resources.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains(
                ThemeResourcePrefix,
                StringComparison.OrdinalIgnoreCase) == true);

        if (currentTheme is not null)
        {
            resources.Remove(currentTheme);
        }

        resources.Add(new ResourceDictionary
        {
            Source = new Uri($"Themes/Theme.{theme}.xaml", UriKind.Relative)
        });
    }
}

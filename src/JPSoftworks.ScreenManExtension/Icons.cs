namespace JPSoftworks.ScreenManExtension.Helpers;

internal static class Icons
{
    internal static IconInfo Main { get; } = IconHelpers.FromRelativePath("Assets\\MainIcon.png");
    internal static IconInfo Picture { get; } = new("\uEB9F");
    internal static IconInfo Video { get; } = new("\uE714");
    internal static IconInfo Copy { get; } = new("\uE8C8");
    internal static IconInfo Delete { get; } = new("\uE74D");
    internal static IconInfo Edit { get; } = new("\uE70F");
    internal static IconInfo FolderOpen { get; } = new("\uE838");
    internal static IconInfo Tag { get; } = new("\uE8EC");
    internal static IconInfo Calendar { get; } = new("\uE787");
    internal static IconInfo All { get; } = new("\uF571");
    internal static IconInfo Favorite { get; } = new("\uE734");
    internal static IconInfo FavoriteFilled { get; } = new("\uE735");
}

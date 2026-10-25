namespace ImageForge.App;

internal static class WorkspaceNavigation
{
    public static bool IsEditingPage(int page) => page is 1 or 2 or 3 or 4 or 6 or >= 10 and <= 14;
    public static int ResolvePage(int requestedPage, bool hasLoadedTarget) =>
        requestedPage is < 0 or > 14 || IsEditingPage(requestedPage) && !hasLoadedTarget ? 0 : requestedPage;
}

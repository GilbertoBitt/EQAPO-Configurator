using EQAPO_Configurator.Services;

namespace EQAPO_Configurator.Models;

public enum HeadphoneProfileAction
{
    Download,
    Generate
}

public sealed class UnifiedHeadphoneSearchResult
{
    public string Name { get; init; } = "";
    public string Detail { get; init; } = "";
    public string ActionLabel { get; init; } = "";
    public HeadphoneProfileAction Action { get; init; }
    public HeadphoneSearchResult? DownloadResult { get; init; }
}

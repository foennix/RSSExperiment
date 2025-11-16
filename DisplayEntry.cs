using System;
using Avalonia.Media.Imaging;

namespace HelloWorldApp;

public sealed class DisplayEntry
{
    public string Title { get; init; } = string.Empty;
    public DateTimeOffset PublishDate { get; init; }
    public string Content { get; init; } = string.Empty;
    public string Link { get; init; } = string.Empty;
    public Bitmap? Image { get; init; }

    public string PublishDateDisplay => PublishDate.LocalDateTime.ToString("f");
}

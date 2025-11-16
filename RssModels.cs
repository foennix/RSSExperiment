using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace HelloWorldApp.Models;

[XmlRoot("RssFeedData")]
public class RssFeedData
{
    public string Title { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public DateTimeOffset RetrievedAt { get; set; } = DateTimeOffset.UtcNow;

    [XmlArray("Entries")]
    [XmlArrayItem("Entry")]
    public List<RssEntry> Entries { get; set; } = new();
}

public class RssEntry
{
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset PublishDate { get; set; } = DateTimeOffset.UtcNow;
    public string Content { get; set; } = string.Empty;
    public string InlineContent { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string? ImageBase64 { get; set; }
    public string? ImageMimeType { get; set; }
}

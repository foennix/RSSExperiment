using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using HelloWorldApp.Models;

namespace HelloWorldApp;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DisplayEntry> _entries = new();
    private readonly HttpClient _httpClient = new();
    private RssFeedData? _currentFeed;

    private TextBox? _feedUrlTextBox;
    private NumericUpDown? _entryCountNumeric;
    private ItemsControl? _feedItemsControl;
    private TextBlock? _feedTitleTextBlock;
    private TextBlock? _statusTextBlock;

    public MainWindow()
    {
        InitializeComponent();

        _feedUrlTextBox = this.FindControl<TextBox>("FeedUrlTextBox");
        _entryCountNumeric = this.FindControl<NumericUpDown>("EntryCountNumeric");
        _feedItemsControl = this.FindControl<ItemsControl>("FeedItemsControl");
        _feedTitleTextBlock = this.FindControl<TextBlock>("FeedTitleTextBlock");
        _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");

        if (_feedItemsControl is not null)
        {
            _feedItemsControl.ItemsSource = _entries;
        }

        if (_feedUrlTextBox is not null)
        {
            _feedUrlTextBox.Text = "https://feeds.bbci.co.uk/news/world/rss.xml";
        }

        SetStatus("Enter an RSS feed URL and select Download Feed to begin.");
    }

    private async void OnDownloadFeedClick(object? sender, RoutedEventArgs e)
    {
        if (_feedUrlTextBox is null || _entryCountNumeric is null)
        {
            return;
        }

        var url = _feedUrlTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            SetStatus("Please enter a valid RSS feed URL.");
            return;
        }

        var entryCount = Math.Max(1, (int)Math.Round(_entryCountNumeric.Value ?? 5));
        SetStatus("Downloading feed...");

        try
        {
            var feedData = await FetchFeedAsync(url, entryCount);
            _currentFeed = feedData;
            DisplayFeed(feedData);
            SetStatus($"Loaded {feedData.Entries.Count} entries from {feedData.Title}.");
        }
        catch (Exception ex)
        {
            SetStatus($"Unable to download feed: {ex.Message}");
        }
    }

    private async void OnSaveFeedClick(object? sender, RoutedEventArgs e)
    {
        if (_currentFeed is null)
        {
            SetStatus("Download or load a feed before saving.");
            return;
        }

        if (StorageProvider is null)
        {
            SetStatus("Saving is unavailable on this platform.");
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = $"{_currentFeed.Title}-feed.xml",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("XML file") { Patterns = new[] {"*.xml"} }
            }
        });

        if (file is null)
        {
            SetStatus("Save canceled.");
            return;
        }

        try
        {
            await using var stream = await file.OpenWriteAsync();
            var serializer = new XmlSerializer(typeof(RssFeedData));
            serializer.Serialize(stream, _currentFeed);
            SetStatus($"Feed saved to {file.Path}. You can open it even when offline.");
        }
        catch (Exception ex)
        {
            SetStatus($"Unable to save feed: {ex.Message}");
        }
    }

    private async void OnOpenFeedClick(object? sender, RoutedEventArgs e)
    {
        if (StorageProvider is null)
        {
            SetStatus("Opening files is unavailable on this platform.");
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("XML file") { Patterns = new[] {"*.xml"} }
            }
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            SetStatus("Open canceled.");
            return;
        }

        try
        {
            await using var stream = await file.OpenReadAsync();
            var serializer = new XmlSerializer(typeof(RssFeedData));
            if (serializer.Deserialize(stream) is RssFeedData feedData)
            {
                _currentFeed = feedData;
                DisplayFeed(feedData);
                SetStatus($"Loaded feed '{feedData.Title}' from disk.");
            }
            else
            {
                SetStatus("The selected file did not contain a valid feed.");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Unable to open feed: {ex.Message}");
        }
    }

    private void DisplayFeed(RssFeedData feedData)
    {
        _entries.Clear();
        foreach (var entry in feedData.Entries)
        {
            _entries.Add(new DisplayEntry
            {
                Title = entry.Title,
                PublishDate = entry.PublishDate,
                Content = entry.Content,
                Link = entry.Link,
                Image = CreateBitmap(entry.ImageBase64)
            });
        }

        if (_feedTitleTextBlock is not null)
        {
            _feedTitleTextBlock.Text = string.IsNullOrWhiteSpace(feedData.Title)
                ? "Feed"
                : feedData.Title;
        }
    }

    private async Task<RssFeedData> FetchFeedAsync(string url, int entryCount)
    {
        using var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var xmlReader = XmlReader.Create(stream);
        var feed = System.ServiceModel.Syndication.SyndicationFeed.Load(xmlReader)
                   ?? throw new InvalidOperationException("Unable to parse RSS feed.");

        var feedData = new RssFeedData
        {
            Title = feed.Title?.Text ?? "Feed",
            SourceUrl = url,
            RetrievedAt = DateTimeOffset.UtcNow
        };

        var items = feed.Items.Take(entryCount).ToList();
        foreach (var item in items)
        {
            var rawContent = GetRawContent(item);
            var imageUrl = ExtractFirstImageUrl(rawContent);
            (string base64, string mimeType)? imageData = null;
            if (!string.IsNullOrEmpty(imageUrl))
            {
                imageData = await DownloadImageAsync(imageUrl);
            }

            feedData.Entries.Add(new RssEntry
            {
                Title = WebUtility.HtmlDecode(item.Title?.Text ?? "Untitled"),
                PublishDate = item.PublishDate != DateTimeOffset.MinValue ? item.PublishDate : DateTimeOffset.UtcNow,
                Content = CleanText(rawContent),
                Link = item.Links.FirstOrDefault()?.Uri?.ToString() ?? string.Empty,
                ImageBase64 = imageData?.base64,
                ImageMimeType = imageData?.mimeType
            });
        }

        return feedData;
    }

    private static string GetRawContent(System.ServiceModel.Syndication.SyndicationItem item)
    {
        if (item.Content is System.ServiceModel.Syndication.TextSyndicationContent textContent)
        {
            return textContent.Text;
        }

        if (item.Summary is not null)
        {
            return item.Summary.Text;
        }

        return string.Empty;
    }

    private async Task<(string base64, string mimeType)?> DownloadImageAsync(string url)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var mediaType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            return (Convert.ToBase64String(bytes), mediaType);
        }
        catch
        {
            return null;
        }
    }

    private static string CleanText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(html);
        var withoutScripts = Regex.Replace(decoded, "<script.*?</script>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var withBreaks = Regex.Replace(withoutScripts, "<(br|p|/p)[^>]*>", "\n", RegexOptions.IgnoreCase);
        var stripped = Regex.Replace(withBreaks, "<.*?>", string.Empty).Trim();
        return Regex.Replace(stripped, "\n{3,}", "\n\n").Trim();
    }

    private static string? ExtractFirstImageUrl(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var match = Regex.Match(html, "<img[^>]+src=\"(?<url>[^\"]+)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["url"].Value : null;
    }

    private static Bitmap? CreateBitmap(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(base64);
            using var memoryStream = new MemoryStream(bytes);
            return new Bitmap(memoryStream);
        }
        catch
        {
            return null;
        }
    }

    private void SetStatus(string message)
    {
        if (_statusTextBlock is not null)
        {
            _statusTextBlock.Text = message;
        }
    }

    private sealed class DisplayEntry
    {
        public string Title { get; init; } = string.Empty;
        public DateTimeOffset PublishDate { get; init; }
        public string Content { get; init; } = string.Empty;
        public string Link { get; init; } = string.Empty;
        public Bitmap? Image { get; init; }

        public string PublishDateDisplay => PublishDate.LocalDateTime.ToString("f");
    }
}

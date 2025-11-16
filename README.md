# Avalonia RSS Reader

A desktop RSS reader built with C# and Avalonia. It downloads the latest entries from any RSS/Atom feed, formats them for easy reading, and can save or load feeds as standalone XML files so you can revisit stories offline.

## Features

- Download the most recent entries from any RSS or Atom feed (choose how many to fetch).
- Inline display of article titles, publish dates, summaries, and preview images.
- Save the currently downloaded feed (including text and downloaded images) to an XML file for offline viewing.
- Load a previously saved feed without requiring an internet connection.

## Requirements

- .NET 9 SDK (install via `brew install dotnet-sdk` or from [dotnet.microsoft.com](https://dotnet.microsoft.com/)).
- macOS 12 Monterey or newer (Avalonia Desktop targets macOS, Windows, and Linux, but these instructions focus on macOS).

## Building on macOS

```bash
# Clone the repository
 git clone <your fork url>
 cd RSSExperiment

# Restore packages and build
 dotnet restore
 dotnet build
```

The project file is `HelloWorldApp.csproj`. Building pulls down all Avalonia and RSS parsing dependencies automatically.

## Running the application

```bash
# From the repository root
 dotnet run --project HelloWorldApp.csproj
```

When the window opens:

1. Enter an RSS/Atom feed URL (e.g., `https://feeds.bbci.co.uk/news/world/rss.xml`).
2. Choose how many entries to download using the number control.
3. Click **Download Feed** to fetch and display the stories.
4. Use **Save Feed** to store the current feed (stories + images) to an XML file.
5. Use **Open Saved Feed** to load a previously saved file—even when offline.

The saved XML files contain everything required to rehydrate the reader view, including article text and any downloaded preview images.

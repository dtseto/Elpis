<img width="448" height="615" alt="image" src="https://github.com/user-attachments/assets/8376da92-3bd7-4c67-940f-b39f8a731fb2" />

Elpis 1.6.0 running on Windows with dot net installed.

Download the release package and install dot net 4.8. Enter your pandora email and pw.

https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48

Bugs: There are known occasional race conditions and glitches when changing stations or skipping tracks. Just close and restart the program.

## Elpis not dead
Elpis is sadly no longer developed by its author, Adam Haile, as you can read in the readme of the base repository.

My fork contains minimal changes required to run Elpis and scrobble to last.fm as of February 2024.

Feel free to report issues, suggest new features or open pull requests in this repository.

To run the app:  
1. Register at [bass.radio42.com/bass_register.html](http://bass.radio42.com/bass_register.html)
1. Create API account at [last.fm/api/account/create](https://www.last.fm/api/account/create) (to enable scrobbling)
1. Create file `Elpis/ReleaseData/ReleaseData.cs`:
```csharp
namespace Elpis
{
    public class ReleaseData
    {
        public const string BassRegEmail = "<bass.net-license-email>";
        public const string BassRegKey = "<bass.net-license-key>";
        public const string UpdateBaseUrl = @"";
        public const string UpdateConfigFile = "";
        public const string AnalyticsPostURL = @"";
        public const string LastFMApiKey = "<api-key>";
        public const string LastFMApiSecret = "<api-secret>";
        public const string AmazonTag = "";
    }
}
```
Then build `AppRelease | Mixed Platforms` in your IDE and run Elpis.exe.

## Elpis

Elpis is native Windows client for the Pandora Radio music service, implemented in C# and WPF.  

It includes a C# implementation of the Pandora web API, PandoraSharp, which is roughly a port of the API library used in [Pithos](http://kevinmehall.net/p/pithos/), a Linux Pandora Client.

## Features
 * View, Sort and Select Stations
 * Play, Pause, Skip Song
 * Cover and Artist Art
 * Thumbs Up, Thumbs Down, Tired of Song
 * Save user credentials and automatic login
 * Automatically play last station at launch
 * System tray notification with song info
 * Minimize/Close to system tray
 * Pause on System Lock
 * Launch pandora.com info page for song, artist, album and station
 * Purchase songs from Amazon
 * Automatically reconnects on session timeout (no more "Are you still listening...")
 * Creating stations
 * Custom Media Key support (Global and Application level)
 * Automatic update check and download within the client
 * Last.FM Scrobbling
 * HTTP Api allowing a user to control Elpis remotely. Here's a remote control called [ElpisRemote](https://github.com/seliver/ElpisRemote)
 * Beta testing (testing the newest commit done on Elpis without having to compile anything). For that, just check the option "Check for Beta Updates" on the Settings page.
 * Ability to switch audio output device

## Requirements

Elpis will run anywhere that the Microsoft .NET 4.0 Framework will run. In other words, Windows XP SP3, Vista, Windows 7, Windows 8 and some users mentioned that it works on Windows 10. The actual hardware requirements are practically negligible, it actually uses less memory than the HTML5 web version running in a modern browser like Chrome.

Afraid you might not have .NET 4.0 or don’t know what it is? No worries, if you are running the Windows 7 SP1 or above and have Windows Update enabled, you’ve already got it. If not and the installer finds that it’s not installed, it will automatically download and install it for you. It only takes a few minutes.

## Download

To download the latest version of Elpis, click here: [Elpis Releases](https://github.com/adammhaile/Elpis/releases)

## Other Links
 * [Pithos](http://kevinmehall.net/p/pithos/) (Linux Gnome client)
 * [Pianobar](http://6xq.net/projects/pianobar/) (Linux command line client)
 * [LPFM](http://lpfm.codeplex.com/) - Elpis uses (and contributes to) LPFM, an open source .NET API for Last.FM
 * [ElpisRemote](https://github.com/seliver/ElpisRemote) - Android Remote Control for Elpis, built with Java on Eclipse.


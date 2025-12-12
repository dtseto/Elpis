using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json.Linq;
using PandoraSharp;
using PandoraSharpPlayer;
using Xunit;

namespace Elpis.Pipeline.Tests.Audio
{
    public class PlaylistTests
    {
        [Fact]
        public void NextSong_enqueues_played_songs_and_trims_history()
        {
            var playlist = new Playlist(maxPlayed: 1, lowCount: 0);

            Song queuedSong = null;
            Song dequeuedSong = null;

            playlist.PlayedSongQueued += (_, song) => queuedSong = song;
            playlist.PlayedSongDequeued += (_, song) => dequeuedSong = song;

            var first = CreateSong("track-1");
            var second = CreateSong("track-2");
            var third = CreateSong("track-3");

            playlist.AddSongs(new List<Song> { first, second, third });

            playlist.NextSong();

            playlist.Current.Played = true;
            playlist.NextSong();

            Assert.Same(first, queuedSong);
            Assert.Null(dequeuedSong);

            playlist.Current.Played = true;
            playlist.NextSong();

            Assert.Same(second, queuedSong);
            Assert.Same(first, dequeuedSong);
        }

        [Fact]
        public void NextSong_reloads_playlist_when_song_invalid()
        {
            var playlist = new Playlist(maxPlayed: 2, lowCount: 1);
            var reloadTriggered = new ManualResetEventSlim(false);

            playlist.PlaylistLow += (_, __) =>
            {
                playlist.AddSongs(new List<Song> { CreateSong("replacement") });
                reloadTriggered.Set();
            };

            playlist.AddSongs(new List<Song> { CreateSong("stale", isValid: false) });

            var first = playlist.NextSong();
            Assert.False(first.IsStillValid);
            Assert.True(reloadTriggered.Wait(TimeSpan.FromSeconds(1)), "Playlist reload was never triggered.");

            var second = playlist.NextSong();
            Assert.True(second.IsStillValid);
            Assert.Equal("replacement", second.TrackToken);
        }

        private static Song CreateSong(string trackToken, bool isValid = true)
        {
            var payload = new JObject
            {
                ["trackToken"] = trackToken,
                ["artistName"] = "Test Artist",
                ["albumName"] = "Test Album",
                ["amazonAlbumDigitalAsin"] = "",
                ["amazonSongDigitalAsin"] = "",
                ["amazonAlbumUrl"] = "",
                ["audioUrlMap"] = new JObject
                {
                    ["highQuality"] = new JObject
                    {
                        ["audioUrl"] = "http://audio.example.com/aac"
                    }
                },
                ["additionalAudioUrl"] = new JArray("http://audio.example.com/mp3"),
                ["songRating"] = 0,
                ["stationId"] = "station-1",
                ["songName"] = $"Song {trackToken}",
                ["songDetailUrl"] = "",
                ["artistDetailUrl"] = "",
                ["albumDetailUrl"] = "",
                ["albumArtUrl"] = "",
                ["trackGain"] = "0"
            };

            var song = new Song(new Pandora(), payload);

            if (!isValid)
            {
                var prop = typeof(Song).GetProperty("PlaylistTime", BindingFlags.Instance | BindingFlags.Public);
                var setter = prop?.GetSetMethod(true);
                setter?.Invoke(song, new object[] { Time.Unix() - 7200 });
            }

            return song;
        }
    }
}

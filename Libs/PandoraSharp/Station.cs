/*
 * Copyright 2012 - Adam Haile
 * http://adamhaile.net
 *
 * This file is part of PandoraSharp.
 * PandoraSharp is free software: you can redistribute it and/or modify 
 * it under the terms of the GNU General Public License as published by 
 * the Free Software Foundation, either version 3 of the License, or 
 * (at your option) any later version.
 * 
 * PandoraSharp is distributed in the hope that it will be useful, 
 * but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the 
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License 
 * along with PandoraSharp. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows.Shell;
using IWshRuntimeLibrary;
using Util;
using PandoraSharp.Exceptions;
using Newtonsoft.Json.Linq;
using File = System.IO.File;
using System.Xml.Serialization;
using System.Web.Script.Serialization;
using System.Threading.Tasks;


namespace PandoraSharp
{
    public class Station : INotifyPropertyChanged
    {
        private readonly object _playlistLock = new object(); // Add lock
        private bool _gettingPlaylist = false;

        private readonly object _artLock = new object();
        private readonly Pandora _pandora;
        private byte[] _artImage;

        public Station(Pandora p, JToken d)
        {
            SkipLimitReached = false;
            SkipLimitTime = DateTime.MinValue;

            _pandora = p;
            
            ID = d["stationId"].ToString();
            IdToken = d["stationToken"].ToString();
            IsCreator = !d["isShared"].ToObject<bool>();
            IsQuickMix = d["isQuickMix"].ToObject<bool>();
            Name = WebUtility.HtmlDecode(d["stationName"].ToString());
            InfoUrl = (string)d["stationDetailUrl"];

            if (IsQuickMix)
            {
                Name = "Quick Mix";
                _pandora.QuickMixStationIDs.Clear();
                var qmIDs = d["quickMixStationIds"].ToObject<string[]>();
                foreach(var qmid in qmIDs)
                    _pandora.QuickMixStationIDs.Add((string) qmid);
            }

            bool downloadArt = true;
            if (!_pandora.ImageCachePath.Equals("") && File.Exists(ArtCacheFile))
            {
                try
                {
                    ArtImage = File.ReadAllBytes(ArtCacheFile);
                }
                catch (Exception)
                {
                    Log.O("Error retrieving image cache file: " + ArtCacheFile);
                    downloadArt = true;
                }

                downloadArt = false;
            }

            /* if (downloadArt)
            {
                var value = d.SelectToken("artUrl");
                if (value != null)
                {
                    ArtUrl = value.ToString();

                    if (ArtUrl != String.Empty)
                    {
                        try
                        {
                            ArtImage = PRequest.ByteRequest(ArtUrl);
                            if (ArtImage.Length > 0)
                                File.WriteAllBytes(ArtCacheFile, ArtImage);
                        }
                        catch (Exception)
                        {
                            Log.O("Error saving image cache file: " + ArtCacheFile);
                        }
                    }
                }
                //}
                
            }
            */

        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        public string ID { get; private set; }
        public string IdToken { get; private set; }
        public bool IsCreator { get; private set; }

        [DefaultValue(false)]
        public bool IsQuickMix { get; private set; }

        public string Name { get; private set; }

        private bool _useQuickMix = false;
        public bool UseQuickMix
        {
            get { return _useQuickMix; }
            set
            {
                if (value != _useQuickMix)
                {
                    _useQuickMix = value;
                    Notify("UseQuickMix");
                }
            }
        }

        public string ArtUrl { get; private set; }
        [XmlIgnore]
        [ScriptIgnore]
        public byte[] ArtImage
        {
            get
            {
                lock (_artLock)
                {
                    return _artImage;
                }
            }
            private set
            {
                lock (_artLock)
                {
                    _artImage = value;
                }
            }
        }

        public string InfoUrl
        {
            get;
            set;
        }

        public int ThumbsUp { get; set; }
        public int ThumbsDown { get; set; }

        public string ArtCacheFile
        {
            get { return Path.Combine(_pandora.ImageCachePath, "Station_" + IdToken); }
        }

        public bool SkipLimitReached { get; set; }
        public DateTime SkipLimitTime { get; set; }

        private void StationArtDownloadHandler(object sender, DownloadDataCompletedEventArgs e)
        {
            if (e.Result.Length == 0)
                return;

            ArtImage = e.Result;
        }

        public void TransformIfShared()
        {
            if (!IsCreator)
            {
                Log.O("Pandora: Transforming Station");
                _pandora.CallRPC("station.transformSharedStation", "stationToken", IdToken);
                IsCreator = true;
            }
        }

        //private bool _gettingPlaylist = false;
        public async Task<List<Song>> GetPlaylistAsync()
        {
            // Take/inspect the gate under lock, then release before awaiting.
            lock (_playlistLock)
            {
                if (_gettingPlaylist)
                    return new List<Song>();
                _gettingPlaylist = true;
            }

            Log.O("GetPlaylist");

            try
            {
                if (_pandora == null)
                    throw new InvalidOperationException("_pandora is null in GetPlaylistAsync.");

                if (string.IsNullOrWhiteSpace(IdToken))
                    throw new InvalidOperationException("Station IdToken is null/empty in GetPlaylistAsync.");

                var req = new JObject
                {
                    ["stationToken"] = IdToken
                };

                if (_pandora.AudioFormat != PAudioFormat.AACPlus)
                    req["additionalAudioUrl"] = "HTTP_128_MP3,HTTP_192_MP3";

                // MUST use SSL
                var playlist = await _pandora.CallRPC("station.getPlaylist", req, isAuth: false, useSSL: true)
                                             .ConfigureAwait(false);

                // Be defensive about the JSON shape
                var resultToken = playlist?.Result;
                if (resultToken == null)
                {
                    Log.O("Playlist RPC returned null Result.");
                    return new List<Song>();
                }

                var items = resultToken["items"] as JArray;
                if (items == null || items.Count == 0)
                {
                    Log.O("Playlist has no items.");
                    return new List<Song>();
                }

                var results = new List<Song>(items.Count);
                foreach (var jt in items)
                {
                    if (jt?["songName"] == null) continue;
                    try
                    {
                        results.Add(new Song(_pandora, jt));
                    }
                    catch (PandoraException ex)
                    {
                        Log.O("Song Add Error: " + ex.FaultMessage);
                    }
                    catch (Exception ex)
                    {
                        Log.O("Song Add Error: " + ex);
                    }
                }

                return results;
            }
            catch (PandoraException ex)
            {
                if (ex.Message == "PLAYLIST_END")
                {
                    SkipLimitReached = true;
                    SkipLimitTime = DateTime.Now;
                    Log.O("Playlist end reached.");
                    return new List<Song>();
                }
                if (ex.Message == "DAILY_SKIP_LIMIT_REACHED")
                {
                    // preserve your previous behavior: rethrow
                    throw;
                }

                Log.O("Error getting playlist, will try again next time: " + Errors.GetErrorMessage(ex.Fault));
                return new List<Song>();
            }
            finally
            {
                lock (_playlistLock)
                {
                    _gettingPlaylist = false;
                }
            }
        }

        public List<Song> GetPlaylist()
        {
            return GetPlaylistAsync().GetAwaiter().GetResult(); // blocking shim
        }


        public void AddVariety(SearchResult item)
        {
            Log.O("Pandora: Adding {0} to {1}", item.DisplayName, this.Name);

            try
            {
                _pandora.CallRPC("station.addMusic", "stationToken", IdToken, "musicToken", item.MusicToken);
            }
            catch{} // eventually do something with this
        }

        public void Rename(string newName)
        {
            try
            {
                TransformIfShared();
                if (newName == Name)
                    return;
                Log.O("Pandora: Renaming Station");
                _pandora.CallRPC("station.renameStation", "stationToken", IdToken, "stationName", newName);

                Name = newName;
            }
            catch (Exception ex)
            {
                Log.O(ex.ToString());
            }
        }

        public void Delete()
        {
            Log.O("Pandora: Deleting Station");
            _pandora.CallRPC("station.deleteStation", "stationToken", IdToken);
            if (File.Exists(ArtCacheFile))
            {
                try
                {
                    File.Delete(ArtCacheFile);
                }
                catch (Exception)
                {
                }
            }
        }

        public void CreateShortcut()
        {
            WshShellClass wsh = new WshShellClass();

            string targetPathWithoutExtension = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\Elpis - " + Name;
            for (int i = 1; File.Exists(targetPathWithoutExtension+".lnk"); i++ )
            {
                targetPathWithoutExtension = targetPathWithoutExtension + i;
            }
            IWshShortcut shortcut = (IWshShortcut)wsh.CreateShortcut(targetPathWithoutExtension + ".lnk");
            if(shortcut != null)
            {
                shortcut.Arguments = String.Format("--station={0}", this.ID);
                shortcut.TargetPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Elpis.exe";
                // not sure about what this is for
                shortcut.WindowStyle = 1;
                shortcut.Description = String.Format("Start Elpis tuned to {0}", this.Name);
                shortcut.WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                
                //Get the assembly.
                Assembly currAssembly = Assembly.LoadFrom(shortcut.TargetPath);

                //Gets the image from the exe resources
                Stream stream = currAssembly.GetManifestResourceStream("main_icon.ico");
                if (null != stream)
                {
                    string temp = Path.GetTempFileName();
                    Image.FromStream(stream).Save(temp);
                    shortcut.IconLocation = temp;
                }

                shortcut.Save();
            }

        }

        public JumpTask asJumpTask()
        {
            var task = new JumpTask();
            task.Title = Name;
            task.Description = "Play station " + Name;
            task.ApplicationPath = Assembly.GetEntryAssembly().Location;
            task.Arguments = "--station=" + ID;
            return task;
        }
    }
}
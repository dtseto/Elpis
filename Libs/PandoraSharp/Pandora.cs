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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PandoraSharp.Exceptions;
using Util;
using Newtonsoft.Json.Linq;

namespace PandoraSharp
{
    public class Pandora
    {
        #region Delegates

        public delegate void ConnectionEventHandler(object sender, bool state, ErrorCodes code);

        public delegate void FeedbackUpdateEventHandler(object sender, Song song, bool success);

        public delegate void LoginStatusEventHandler(object sender, string status);

        public delegate void PandoraErrorEventHandler(object sender, string errorCode, string msg);

        public delegate void StationsUpdatedEventHandler(object sender);

        public delegate void StationsUpdatingEventHandler(object sender);

        public delegate void QuickMixSavedEventHandler(object sender);

        #endregion

        #region SortOrder enum

        public enum SortOrder
        {
            DateAsc,
            DateDesc,
            AlphaAsc,
            AlphaDesc,
            RatingAsc,
            RatingDesc
        }

        #endregion

        private readonly object _stationListLock = new object(); // Add lock

        private readonly object _authTokenLock = new object();
        private readonly object _partnerIDLock = new object();
        private readonly object _userIDLock = new object();
        private readonly object _rpcCountLock = new object();

        protected internal List<string> QuickMixStationIDs = new List<string>();
        private string _audioFormat = PAudioFormat.MP3;

        private string _authToken;
        private string _partnerID;
        private string _userID;

        private bool _authorizing;
        private bool _connected;
        private bool _firstAuthComplete = false;
        private string _imageCachePath = "";
        private string _password = "";
        private string _rid;
        private int _rpcCount;
        private long _syncTime;
        private long _timeSynced;
        private bool _metaDataUpToDate;

        private string _user = "";
        private string listenerId;
        //private string webAuthToken;

        public Pandora()
        {
            QuickMixStationIDs = new List<string>();
            StationSortOrder = SortOrder.DateDesc;
            HasSubscription = true;
            //this.set_proxy(null);
        }

        private string AuthToken
        {
            get
            {
                lock (_authTokenLock)
                {
                    return _authToken;
                }
            }
            set
            {
                lock (_authTokenLock)
                {
                    _authToken = value;
                }
            }
        }

        private string PartnerID
        {
            get
            {
                lock (_partnerIDLock)
                {
                    return _partnerID;
                }
            }
            set
            {
                lock (_partnerIDLock)
                {
                    _partnerID = value;
                }
            }
        }

        private string UserID
        {
            get
            {
                lock (_userIDLock)
                {
                    return _userID;
                }
            }
            set
            {
                lock (_userIDLock)
                {
                    _userID = value;
                }
            }
        }

        public List<Station> Stations { get; private set; }

        public string ImageCachePath
        {
            get { return _imageCachePath; }
            set { _imageCachePath = value; }
        }

        [DefaultValue(true)]
        public bool HasSubscription { get; private set; }

        public string AudioFormat
        {
            get { return _audioFormat; }
            set { SetAudioFormat(value); }
        }

        private bool _forceSSL = false;
        public bool ForceSSL
        {
            get { return _forceSSL; }
            set { _forceSSL = value; }
        }

        public SortOrder StationSortOrder { get; set; }
        public event ConnectionEventHandler ConnectionEvent;
        public event StationsUpdatedEventHandler StationUpdateEvent;
        public event StationsUpdatingEventHandler StationsUpdatingEvent;
        public event FeedbackUpdateEventHandler FeedbackUpdateEvent;
        public event LoginStatusEventHandler LoginStatusEvent;
        public event QuickMixSavedEventHandler QuickMixSavedEvent;

        protected internal async Task<string> RPCRequest(string url, string data)
        {
            try
            {
                return await PRequest.StringRequest(url, data);
            }
            catch (Exception e)
            {
                Log.O(e.ToString());
                throw new PandoraException(ErrorCodes.ERROR_RPC, e);
            }
        }

        //Checks for fault returns.  If it's an Auth fault (auth timed out)
        //return false, which signals that a re-auth and retry needs to be done
        //otherwise return true signalling all clear.
        //All other faults will be thrown
        protected internal bool HandleFaults(JSONResult result, bool secondTry)
        {
            if (result.Fault)
            {
                if (result.FaultCode == ErrorCodes.INVALID_AUTH_TOKEN)
                    if (!secondTry)
                        return false; //auth fault, signal a re-auth

                Log.O("Fault: " + result.FaultString);
                throw new PandoraException(result.FaultCode); //other, throw the exception
            }

            return true; //no fault
        }

        protected internal async Task<string> CallRPC_Internal(string method, JObject request,
            bool isAuth, bool useSSL = false)
        {
            int callID = 0;
            lock (_rpcCountLock)
            {
                callID = _rpcCount++;
            }

            string shortMethod = (method.Contains("&") ?
                method.Substring(0, method.IndexOf("&")) : method);

            string url = (useSSL || _forceSSL ? "https://" : "http://") + Const.RPC_URL + "?method=" + method;

            if (request == null) request = new JObject();

            if (AuthToken != null &&
                PartnerID != null)
            {
                //if (!url.EndsWith("?")) url += "?";
                url += ("&partner_id=" + PartnerID);
                url += ("&auth_token=" + Uri.EscapeDataString(AuthToken));

                if (UserID != null)
                {
                    url += ("&user_id=" + UserID);
                    request["userAuthToken"] = AuthToken;
                    request["syncTime"] = AdjustedSyncTime();
                }
            }

            string json = request.ToString();
            string data = string.Empty;
            if (method == "auth.partnerLogin")
                data = json;
            else
                data = Crypto.out_key.Encrypt(json);

            Log.O("[" + callID + ":url]: " + url);

            if (isAuth)
                Log.O("[" + callID + ":json]: " + json.SanitizeJSON().Replace(_password, "********").Replace(_user, "********"));
            else
                Log.O("[" + callID + ":json]: " + json.SanitizeJSON());

            //if reauthorizing, wait until it completes.
            if (!isAuth)
            {
                int waitCount = 30;
                while (_authorizing)
                {
                    waitCount--;
                    if (waitCount >= 0)
                        Thread.Sleep(1000);
                    else
                        break;
                }
            }

            string response = await RPCRequest(url, data);
            Log.O("[" + callID + ":response]: " + response.SanitizeJSON());
            return response;
        }

        protected internal virtual async Task<JSONResult> CallRPC(string method, JObject request = null, bool isAuth = false, bool useSSL = false)
        {
            try
            {
                string response = await CallRPC_Internal(method, request, isAuth, useSSL).ConfigureAwait(false);
                var result = new JSONResult(response);

                if (result.Fault && !HandleFaults(result, false))
                {
                    Log.O("Reauth Required");
                    if (!await AuthenticateUser().ConfigureAwait(false))
                    {
                        HandleFaults(result, true);
                    }
                    else
                    {
                        response = await CallRPC_Internal(method, request, isAuth, useSSL).ConfigureAwait(false);
                        HandleFaults(result, true);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.O("CallRPC Error: " + ex.ToString());
                throw;
            }
        }

        protected internal async Task<JSONResult> CallRPC(string method, params object[] args)
        {
            JObject req = new JObject();
            if (args.Length % 2 != 0)
            {
                Log.O("CallRPC: Called with an uneven number of arguments!");
                return null;
            }

            for (int i = 0; i < args.Length; i += 2)
            {
                if (args[i].GetType() != typeof(string) || args[i].GetType() != typeof(String))
                {
                    Log.O("CallRPC: Called with an incorrect parameter type!");
                    return null;
                }
                req[(string)args[i]] = JToken.FromObject(args[i + 1]);
            }

            return await CallRPC(method, req);
        }

        protected internal object CallRPC(string method, object[] args, bool b_url_args = false,
                                          bool isAuth = false, bool useSSL = false, bool insertTime = true)
        {
            return null;
        }

        public async Task<List<Station>> RefreshStationsAsync()
        {
            Log.O("RefreshStations");
            StationsUpdatingEvent?.Invoke(this);

            JObject req = new JObject
            {
                ["includeStationArtUrl"] = true
            };

            var stationList = await CallRPC("user.getStationList", req);

            List<Station> quickMixes;
            List<Station> normalStations;

            lock (_stationListLock)
            {
                QuickMixStationIDs.Clear();

                var fetchedStations = new List<Station>();
                var stationsToken = stationList?.Result?["stations"] as JArray;

                if (stationsToken != null)
                {
                    foreach (JToken d in stationsToken)
                    {
                        fetchedStations.Add(new Station(this, d));
                    }
                }

                if (QuickMixStationIDs.Count > 0)
                {
                    foreach (Station s in fetchedStations)
                    {
                        if (QuickMixStationIDs.Contains(s.ID))
                            s.UseQuickMix = true;
                    }
                }

                quickMixes = fetchedStations.Where(x => x.IsQuickMix).ToList();
                normalStations = fetchedStations.Where(x => !x.IsQuickMix).ToList();
            }

            switch (StationSortOrder)
            {
                case SortOrder.DateDesc:
                    normalStations = normalStations.OrderByDescending(x => Convert.ToInt64(x.ID)).ToList();
                    break;
                case SortOrder.DateAsc:
                    normalStations = normalStations.OrderBy(x => Convert.ToInt64(x.ID)).ToList();
                    break;
                case SortOrder.AlphaDesc:
                    normalStations = normalStations.OrderByDescending(x => x.Name).ToList();
                    break;
                case SortOrder.AlphaAsc:
                    normalStations = normalStations.OrderBy(x => x.Name).ToList();
                    break;
                case SortOrder.RatingAsc:
                    await GetStationMetaData(normalStations);
                    normalStations = normalStations.OrderBy(x => x.ThumbsUp).ToList();
                    break;
                case SortOrder.RatingDesc:
                    await GetStationMetaData(normalStations);
                    normalStations = normalStations.OrderByDescending(x => x.ThumbsUp).ToList();
                    break;
            }

            var orderedStations = new List<Station>(quickMixes.Count + normalStations.Count);
            orderedStations.AddRange(quickMixes);
            orderedStations.AddRange(normalStations);

            lock (_stationListLock)
            {
                Stations = orderedStations;
            }

            StationUpdateEvent?.Invoke(this);
            return orderedStations;
        }

        //private string getSyncKey()
        //{
        //    string result = string.Empty;

        //    try
        //    {
        //        var keyArray = new Util.Downloader().DownloadString(Const.SYNC_KEY_URL);

        //        var vals = keyArray.Split('|');
        //        if (vals.Length < 3) return result;
        //        var len = 48;
        //        if (!Int32.TryParse(vals[1], out len)) return result;
        //        if (vals[2].Length != len) return result;

        //        Log.O("Sync Key Age (sec): " + vals[0]);
        //        Log.O("Sync Key Length: " + vals[1]);
        //        Log.O("Sync Key: " + vals[2]);

        //        result = vals[2];
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.O(ex.ToString());
        //    }

        //    return result;
        //}

        //private string getSyncTime()
        //{
        //    string result = string.Empty;

        //    try
        //    {
        //        result = new Util.Downloader().DownloadString(Const.SYNC_TIME_URL);
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.O(ex.ToString());
        //    }

        //    return result;
        //}

        public void Logout()
        {
            _firstAuthComplete = false;
        }

        public long AdjustedSyncTime()
        {
            return _syncTime + (Time.Unix() - _timeSynced);
        }

        public async Task<bool> AuthenticateUser()
        {
            _authorizing = true;

            Log.O("AuthUser");

            listenerId = null;
            //webAuthToken = null;
            AuthToken = null;
            PartnerID = null;
            UserID = null;

            JObject req = new JObject();
            req["username"] = "android";
            req["password"] = "AC7IBG09A3DTSYM4R41UJWL07VLN8JI7";
            req["deviceModel"] = "android-generic";

            req["version"] = "5";
            req["includeUrls"] = true;

            JSONResult ret;

            try
            {
                ret = await CallRPC("auth.partnerLogin", req, true, true);
                if (ret.Fault)
                {
                    Log.O("PartnerLogin Error: " + ret.FaultString);
                    return false;
                }
            }
            catch (Exception e)
            {
                Log.O(e.ToString());
                return false;
            }

            JToken result = ret["result"];

            _syncTime = Crypto.DecryptSyncTime(result["syncTime"].ToString());
            _timeSynced = Time.Unix();

            PartnerID = result["partnerId"].ToString();
            AuthToken = result["partnerAuthToken"].ToString();

            req = new JObject();

            req["loginType"] = "user";
            req["username"] = _user;
            req["password"] = _password;

            req["includePandoraOneInfo"] = true;
            req["includeAdAttributes"] = true;
            req["includeSubscriptionExpiration"] = true;
            //req["includeStationArtUrl"] = true;
            //req["returnStationList"] = true;

            req["partnerAuthToken"] = AuthToken;
            req["syncTime"] = _syncTime;// AdjustedSyncTime();

            ret = null;

            ret = await CallRPC("auth.userLogin", req, true, true);
            if (ret.Fault)
            {
                Log.O("UserLogin Error: " + ret.FaultString);
                return false;
            }

            result = ret["result"];
            AuthToken = result["userAuthToken"].ToString();
            UserID = result["userId"].ToString();
            HasSubscription = !result["hasAudioAds"].ToObject<bool>();

            _authorizing = false;
            return true;
        }

        private void SendLoginStatus(string status)
        {
            if (LoginStatusEvent != null)
                LoginStatusEvent(this, status);
        }

        public async Task Connect(string user, string password)
        {
            Log.O("Connect");
            ErrorCodes status = ErrorCodes.SUCCESS;
            _connected = false;

            _user = user;
            _password = password;

            try
            {
                SendLoginStatus("Authenticating user:\r\n" + user);
                _connected = await AuthenticateUser();

                if (_connected)
                {
                    SendLoginStatus("Loading station list...");
                    await RefreshStationsAsync();
                }
                else
                {
                    status = ErrorCodes.ERROR_RPC;
                }
            }
            catch (PandoraException ex)
            {
                status = ex.Fault;
                _connected = false;
            }
            catch (Exception ex)
            {
                status = ErrorCodes.UNKNOWN_ERROR;
                Log.O("Connection Error: " + ex.ToString());
                _connected = false;
            }


            if (ConnectionEvent != null)
                ConnectionEvent(this, _connected, status);
        }

        //public void SetProxy()
        //{

        //}

        public void SetAudioFormat(string fmt)
        {
            if ((fmt != PAudioFormat.AACPlus &&
                 fmt != PAudioFormat.MP3 &&
                 fmt != PAudioFormat.MP3_HIFI) ||
                (!HasSubscription && fmt == PAudioFormat.MP3_HIFI))
            {
                fmt = PAudioFormat.MP3;
            }

            _audioFormat = fmt;
        }

        public void SaveQuickMix()
        {
            var ids = new List<string>();
            foreach (Station s in Stations)
            {
                if (s.UseQuickMix)
                    ids.Add(s.ID);
            }

            JObject req = new JObject();
            req["quickMixStationIds"] = new JArray(ids.ToArray());

            CallRPC("user.setQuickMix", req);

            if (QuickMixSavedEvent != null)
                QuickMixSavedEvent(this);
        }

        public async Task<List<SearchResult>> Search(string query)
        {
            Log.O("Search: " + query);
            JObject req = new JObject();
            req["searchText"] = query;
            var search = await CallRPC("music.search", req);

            var list = new List<SearchResult>();
            var artists = search.Result["artists"];
            var songs = search.Result["songs"];
            foreach (JToken a in artists)
                list.Add(new SearchResult(SearchResultType.Artist, a));

            foreach (JToken s in songs)
                list.Add(new SearchResult(SearchResultType.Song, s));

            list = list.OrderByDescending((i) => i.Score).ToList();

            return list;
        }

        public async Task<Station> CreateStationFromSearch(string token)
        {
            JObject req = new JObject();
            req["musicToken"] = token;
            var result = await CallRPC("station.createStation", req);

            var station = new Station(this, result.Result);
            Stations.Add(station);

            return station;
        }

        private async Task<Station> CreateStation(Song song, string type)
        {
            JObject req = new JObject();
            req["trackToken"] = song.TrackToken;
            req["musicType"] = type;
            var result = await CallRPC("station.createStation", req);

            var station = new Station(this, result.Result);
            Stations.Add(station);

            return station;
        }

        private async Task GetStationMetaData(IEnumerable<Station> stationsToProcess)
        {
            Log.O("RetrieveStationMetaData");

            if (stationsToProcess == null)
                return;

            foreach (var station in stationsToProcess)
            {
                JObject req = new JObject
                {
                    ["stationToken"] = station.IdToken,
                    ["includeExtendedAttributes"] = true
                };

                var stationInfo = await CallRPC("station.getStation", req);
                var feedback = stationInfo.Result["feedback"];

                station.ThumbsUp = Convert.ToInt32(feedback["totalThumbsUp"].ToString());
                station.ThumbsDown = Convert.ToInt32(feedback["totalThumbsDown"].ToString());
            }
        }


        public async Task<Station> CreateStationFromSong(Song song)
        {
            return await CreateStation(song, "song");
        }

        public async Task<Station> CreateStationFromArtist(Song song)
        {
            return await CreateStation(song, "artist");
        }

        public async Task AddFeedback(string stationToken, string trackToken, SongRating rating)
        {
            Log.O("AddFeedback");

            bool rate = (rating == SongRating.love) ? true : false;

            JObject req = new JObject();
            req["stationToken"] = stationToken;
            req["trackToken"] = trackToken;
            req["isPositive"] = rate;

            // Await so exceptions bubble correctly
            await CallRPC("station.addFeedback", req);
        }

        public async Task DeleteFeedback(string feedbackID)
        {
            Log.O("DeleteFeedback");

            if (string.IsNullOrWhiteSpace(feedbackID))
                throw new ArgumentException("feedbackID is required.", nameof(feedbackID));

            var req = new JObject { ["feedbackId"] = feedbackID };

            // Await so exceptions bubble correctly
            await CallRPC("station.deleteFeedback", req).ConfigureAwait(false);
        }

        public void CallFeedbackUpdateEvent(Song song, bool success)
        {

            if (FeedbackUpdateEvent != null)
                FeedbackUpdateEvent(this, song, success);
        }

        public Station GetStationByID(string id)
        {
            foreach (Station s in Stations)
            {
                if (s.ID == id)
                    return s;
            }

            return null;
        }


        // NOTE: Despite its name, this ADDS positive feedback and returns the new feedbackId.
        public async Task<string> GetFeedbackID(string stationToken, string trackToken)
        {
            if (string.IsNullOrWhiteSpace(stationToken))
                throw new ArgumentException("stationToken is required.", nameof(stationToken));
            if (string.IsNullOrWhiteSpace(trackToken))
                throw new ArgumentException("trackToken is required.", nameof(trackToken));

            var req = new JObject
            {
                ["stationToken"] = stationToken,
                ["trackToken"] = trackToken,
                ["isPositive"] = true
            };

            var feedback = await CallRPC("station.addFeedback", req).ConfigureAwait(false);
            return (string)feedback.Result["feedbackId"];
        }


    }
}
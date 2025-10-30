using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using PandoraSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestProject1
{
    [TestClass]
    public class PandoraStationRefreshTests
    {
        private sealed class StubPandora : Pandora
        {
            private readonly Dictionary<string, Queue<Func<JObject, Task<JSONResult>>>> _handlers;
            private readonly object _handlerLock = new object();

            public StubPandora(Dictionary<string, Queue<Func<JObject, Task<JSONResult>>>> handlers)
            {
                _handlers = handlers;
            }

            protected internal override Task<JSONResult> CallRPC(string method, JObject request = null, bool isAuth = false, bool useSSL = false)
            {
                Func<JObject, Task<JSONResult>> handler = null;

                lock (_handlerLock)
                {
                    if (_handlers.TryGetValue(method, out var queue) && queue.Count > 0)
                    {
                        handler = queue.Dequeue();
                    }
                }

                if (handler == null)
                {
                    throw new InvalidOperationException($"No handler registered for method {method}.");
                }

                return handler(request ?? new JObject());
            }
        }

        private static JSONResult BuildStationListResponse(params JObject[] stations)
        {
            var payload = new JObject
            {
                ["stat"] = "ok",
                ["result"] = new JObject
                {
                    ["stations"] = new JArray(stations)
                }
            };

            return new JSONResult(payload.ToString());
        }

        private static JObject BuildStationPayload(string id, string name, bool isQuickMix = false, string[] quickMixStationIds = null)
        {
            var obj = new JObject
            {
                ["stationId"] = id,
                ["stationToken"] = $"token-{id}",
                ["isShared"] = false,
                ["isQuickMix"] = isQuickMix,
                ["stationName"] = name,
                ["stationDetailUrl"] = "http://station"
            };

            if (isQuickMix)
            {
                quickMixStationIds = quickMixStationIds ?? Array.Empty<string>();
                obj["quickMixStationIds"] = new JArray(quickMixStationIds);
            }

            return obj;
        }

        private static JSONResult BuildStationMetadataResponse(int thumbsUp, int thumbsDown)
        {
            var payload = new JObject
            {
                ["stat"] = "ok",
                ["result"] = new JObject
                {
                    ["feedback"] = new JObject
                    {
                        ["totalThumbsUp"] = thumbsUp,
                        ["totalThumbsDown"] = thumbsDown
                    }
                }
            };

            return new JSONResult(payload.ToString());
        }

        [TestMethod]
        public async Task RefreshStationsAsync_WaitsForMetadataBeforePublishing()
        {
            var quickMix = BuildStationPayload("1", "Quick Mix", true, new[] { "2", "3" });
            var betaStation = BuildStationPayload("2", "Beta Station");
            var alphaStation = BuildStationPayload("3", "Alpha Station");

            var metadataTcs = new TaskCompletionSource<JSONResult>();

            var handlers = new Dictionary<string, Queue<Func<JObject, Task<JSONResult>>>>
            {
                ["user.getStationList"] = new Queue<Func<JObject, Task<JSONResult>>>(new[]
                {
                    new Func<JObject, Task<JSONResult>>(async _ =>
                    {
                        await Task.Delay(10).ConfigureAwait(false);
                        return BuildStationListResponse(quickMix, betaStation, alphaStation);
                    })
                }),
                ["station.getStation"] = new Queue<Func<JObject, Task<JSONResult>>>(new[]
                {
                    new Func<JObject, Task<JSONResult>>(_ => metadataTcs.Task),
                    new Func<JObject, Task<JSONResult>>(_ => Task.FromResult(BuildStationMetadataResponse(1, 0)))
                })
            };

            var pandora = new StubPandora(handlers)
            {
                StationSortOrder = Pandora.SortOrder.RatingDesc
            };

            var updateEvents = 0;
            pandora.StationUpdateEvent += _ => Interlocked.Increment(ref updateEvents);

            var refreshTask = pandora.RefreshStationsAsync();

            await Task.Delay(50).ConfigureAwait(false);

            Assert.AreEqual(0, updateEvents, "Station updates should wait for metadata fetches to complete.");
            Assert.IsNull(pandora.Stations, "Stations must not be published before metadata resolves.");

            metadataTcs.SetResult(BuildStationMetadataResponse(3, 0));

            var stations = await refreshTask.ConfigureAwait(false);

            Assert.AreEqual(1, updateEvents, "Station update event should fire exactly once.");
            Assert.IsNotNull(stations, "Refresh should return a station list.");
            Assert.AreEqual(3, stations.Count, "Quick mix and normal stations should be included.");
            CollectionAssert.AreEqual(
                new[] { "Quick Mix", "Beta Station", "Alpha Station" },
                stations.Select(s => s.Name).ToArray(),
                "Stations should keep quick mix first and sort by rating thereafter.");
        }

        [TestMethod]
        public async Task RefreshStationsAsync_ReturnsAllStationsAfterAsyncDelay()
        {
            var quickMix = BuildStationPayload("10", "Quick Mix", true, new[] { "20", "30" });
            var firstStation = BuildStationPayload("20", "First Station");
            var secondStation = BuildStationPayload("30", "Second Station");

            var handlers = new Dictionary<string, Queue<Func<JObject, Task<JSONResult>>>>
            {
                ["user.getStationList"] = new Queue<Func<JObject, Task<JSONResult>>>(new[]
                {
                    new Func<JObject, Task<JSONResult>>(async _ =>
                    {
                        await Task.Delay(25).ConfigureAwait(false);
                        return BuildStationListResponse(quickMix, firstStation, secondStation);
                    })
                })
            };

            var pandora = new StubPandora(handlers)
            {
                StationSortOrder = Pandora.SortOrder.DateDesc
            };

            var stations = await pandora.RefreshStationsAsync().ConfigureAwait(false);

            Assert.AreEqual(3, stations.Count, "Refresh should publish every station returned by the API.");
            Assert.IsNotNull(pandora.Stations, "Stations property should be populated after refresh.");
            Assert.AreEqual(3, pandora.Stations.Count, "Stations property should hold the combined list.");
            Assert.IsTrue(stations.Any(s => s.IsQuickMix), "Quick mix entry should be present.");
            Assert.IsTrue(stations.Any(s => !s.IsQuickMix), "Non quick-mix stations should be present.");
            Assert.AreEqual("Quick Mix", stations.First().Name, "Quick mix station should remain at the top of the list.");
        }
    }
}

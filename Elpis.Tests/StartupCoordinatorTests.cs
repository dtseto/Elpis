using System;
using System.Collections.Generic;
using Elpis;
using Xunit;

namespace Elpis.Tests
{
    public class StartupCoordinatorTests
    {
        private class Harness
        {
            public bool InitComplete { get; set; }
            public bool FinalComplete { get; set; }
            public object CurrentPage { get; set; }
            public object LoadingPage { get; set; }
            public int QueueCount { get; private set; }

            public StartupCoordinator Build()
            {
                return new StartupCoordinator(
                    () => InitComplete,
                    () => FinalComplete,
                    () => CurrentPage,
                    () => LoadingPage,
                    () => QueueCount++);
            }
        }

        [Fact]
        public void Queues_when_loading_page_visible_and_init_complete()
        {
            var harness = new Harness
            {
                InitComplete = true,
                FinalComplete = false,
                LoadingPage = new object()
            };
            harness.CurrentPage = harness.LoadingPage;

            var coordinator = harness.Build();
            coordinator.EnsureFinalLoadQueued();

            Assert.Equal(1, harness.QueueCount);
        }

        [Fact]
        public void Does_not_queue_before_init_completes()
        {
            var harness = new Harness
            {
                InitComplete = false,
                FinalComplete = false,
                LoadingPage = new object()
            };
            harness.CurrentPage = harness.LoadingPage;

            var coordinator = harness.Build();
            coordinator.EnsureFinalLoadQueued();

            Assert.Equal(0, harness.QueueCount);
        }

        [Fact]
        public void Does_not_queue_after_final_completion()
        {
            var harness = new Harness
            {
                InitComplete = true,
                FinalComplete = true,
                LoadingPage = new object()
            };
            harness.CurrentPage = harness.LoadingPage;

            var coordinator = harness.Build();
            coordinator.EnsureFinalLoadQueued();

            Assert.Equal(0, harness.QueueCount);
        }

        [Fact]
        public void Only_queues_when_current_page_is_loading()
        {
            var harness = new Harness
            {
                InitComplete = true,
                FinalComplete = false,
                LoadingPage = new object(),
                CurrentPage = new object()
            };

            var coordinator = harness.Build();
            coordinator.EnsureFinalLoadQueued();
            Assert.Equal(0, harness.QueueCount);

            harness.CurrentPage = harness.LoadingPage;
            coordinator.EnsureFinalLoadQueued();
            Assert.Equal(1, harness.QueueCount);
        }

        [Fact]
        public void Does_not_queue_when_loading_page_is_null()
        {
            var harness = new Harness
            {
                InitComplete = true,
                FinalComplete = false,
                LoadingPage = null,
                CurrentPage = new object()
            };

            var coordinator = harness.Build();
            coordinator.EnsureFinalLoadQueued();

            Assert.Equal(0, harness.QueueCount);
        }

        [Theory]
        [MemberData(nameof(ConstructorGuardCases))]
        public void Constructor_validates_arguments(
            Func<bool> initAccessor,
            Func<bool> finalAccessor,
            Func<object> currentAccessor,
            Func<object> loadingAccessor,
            Action queueAction,
            string expectedParamName)
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new StartupCoordinator(
                    initAccessor,
                    finalAccessor,
                    currentAccessor,
                    loadingAccessor,
                    queueAction));

            Assert.Equal(expectedParamName, exception.ParamName);
        }

        public static IEnumerable<object[]> ConstructorGuardCases()
        {
            Func<bool> falseBool = () => false;
            Func<object> nullAccessor = () => null;
            Action noop = () => { };

            yield return new object[] { null, falseBool, nullAccessor, nullAccessor, noop, "initCompleteAccessor" };
            yield return new object[] { falseBool, null, nullAccessor, nullAccessor, noop, "finalCompleteAccessor" };
            yield return new object[] { falseBool, falseBool, null, nullAccessor, noop, "currentPageAccessor" };
            yield return new object[] { falseBool, falseBool, nullAccessor, null, noop, "loadingPageAccessor" };
            yield return new object[] { falseBool, falseBool, nullAccessor, nullAccessor, null, "queueFinalLoad" };
        }
    }
}

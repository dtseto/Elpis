using System;

namespace Elpis
{
    public sealed class StartupCoordinator
    {
        private readonly Func<bool> _initCompleteAccessor;
        private readonly Func<bool> _finalCompleteAccessor;
        private readonly Func<object> _currentPageAccessor;
        private readonly Func<object> _loadingPageAccessor;
        private readonly Action _queueFinalLoad;

        public StartupCoordinator(
            Func<bool> initCompleteAccessor,
            Func<bool> finalCompleteAccessor,
            Func<object> currentPageAccessor,
            Func<object> loadingPageAccessor,
            Action queueFinalLoad)
        {
            _initCompleteAccessor = initCompleteAccessor ?? throw new ArgumentNullException(nameof(initCompleteAccessor));
            _finalCompleteAccessor = finalCompleteAccessor ?? throw new ArgumentNullException(nameof(finalCompleteAccessor));
            _currentPageAccessor = currentPageAccessor ?? throw new ArgumentNullException(nameof(currentPageAccessor));
            _loadingPageAccessor = loadingPageAccessor ?? throw new ArgumentNullException(nameof(loadingPageAccessor));
            _queueFinalLoad = queueFinalLoad ?? throw new ArgumentNullException(nameof(queueFinalLoad));
        }

        public void EnsureFinalLoadQueued()
        {
            var loadingPage = _loadingPageAccessor();
            if (loadingPage == null)
                return;

            if (!_initCompleteAccessor())
                return;

            if (_finalCompleteAccessor())
                return;

            if (_currentPageAccessor() != loadingPage)
                return;

            _queueFinalLoad();
        }
    }
}

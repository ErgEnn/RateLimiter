using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace RateLimiter
{
    internal class ComposedAwaitableConstraint : IAwaitableConstraint
    {
        private readonly IAwaitableConstraint _AwaitableConstraint1;
        private readonly IAwaitableConstraint _AwaitableConstraint2;
        private readonly SemaphoreSlim _Semaphore = new SemaphoreSlim(1, 1);
        private readonly Subject<ThrottleInfo> _Subject = new Subject<ThrottleInfo>();

        internal ComposedAwaitableConstraint(IAwaitableConstraint awaitableConstraint1, IAwaitableConstraint awaitableConstraint2)
        {
            _AwaitableConstraint1 = awaitableConstraint1;
            _AwaitableConstraint2 = awaitableConstraint2;
        }

        public IAwaitableConstraint Clone()
        {
            return new ComposedAwaitableConstraint(_AwaitableConstraint1.Clone(), _AwaitableConstraint2.Clone());
        }

        public async Task<IDisposable> WaitForReadiness(CancellationToken cancellationToken)
        {
            await _Semaphore.WaitAsync(cancellationToken);
            IDisposable[] disposables = new IDisposable[4];
            try
            {
                ThrottleInfo throttleInfo = null;
                disposables[0] = _AwaitableConstraint1.Subscribe(info =>
                {
                    if (throttleInfo is null || throttleInfo.ThrottledForTimeSpan < info.ThrottledForTimeSpan)
                        throttleInfo = info;
                });
                disposables[1] = _AwaitableConstraint2.Subscribe(info =>
                {
                    if (throttleInfo is null || throttleInfo.ThrottledForTimeSpan < info.ThrottledForTimeSpan)
                        throttleInfo = info;
                });
                var task1 = _AwaitableConstraint1.WaitForReadiness(cancellationToken);
                var task2 = _AwaitableConstraint2.WaitForReadiness(cancellationToken);
                if(throttleInfo != null)
                    _Subject.OnNext(throttleInfo);
                await Task.WhenAll(task1, task2);
                disposables[2] = task1.Result;
                disposables[3] = task2.Result;
            }
            catch (Exception)
            {
                _Semaphore.Release();
                throw;
            }
            return new DisposeAction(() =>
            {
                foreach (var disposable in disposables)
                {
                    disposable.Dispose();
                }
                _Semaphore.Release();
            });
        }

        public IDisposable Subscribe(IObserver<ThrottleInfo> observer)
        {
            return _Subject.Subscribe(observer);
        }
    }
}

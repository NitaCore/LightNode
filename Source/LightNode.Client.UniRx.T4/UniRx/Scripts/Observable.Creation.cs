﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UniRx
{
    public static partial class Observable
    {
        /// <summary>
        /// Create anonymous observable. Observer is auto detach when error, completed.
        /// </summary>
        public static IObservable<T> Create<T>(Func<IObserver<T>, IDisposable> subscribe)
        {
            if (subscribe == null) throw new ArgumentNullException("subscribe");

            return new AnonymousObservable<T>(subscribe);
        }

        class AnonymousObservable<T> : IObservable<T>
        {
            readonly Func<IObserver<T>, IDisposable> subscribe;

            public AnonymousObservable(Func<IObserver<T>, IDisposable> subscribe)
            {
                this.subscribe = subscribe;
            }

            public IDisposable Subscribe(IObserver<T> observer)
            {
                var subscription = new SingleAssignmentDisposable();

                var safeObserver = Observer.Create<T>(observer.OnNext, observer.OnError, observer.OnCompleted, subscription);

                if (Scheduler.IsCurrentThreadSchedulerScheduleRequired)
                {
                    Scheduler.CurrentThread.Schedule(() => subscription.Disposable = subscribe(safeObserver));
                }
                else
                {
                    subscription.Disposable = subscribe(safeObserver);
                }

                return subscription;
            }
        }

        /// <summary>
        /// Empty Observable. Returns only OnCompleted on ImmediateScheduler.
        /// </summary>
        public static IObservable<T> Empty<T>()
        {
            return Empty<T>(Scheduler.Immediate);
        }

        /// <summary>
        /// Empty Observable. Returns only OnCompleted on specified scheduler.
        /// </summary>
        public static IObservable<T> Empty<T>(IScheduler scheduler)
        {
            return Observable.Create<T>(observer =>
            {
                return scheduler.Schedule(observer.OnCompleted);
            });
        }

        /// <summary>
        /// Empty Observable. Returns only OnCompleted on ImmediateScheduler. witness is for type inference.
        /// </summary>
        public static IObservable<T> Empty<T>(T witness)
        {
            return Empty<T>(Scheduler.Immediate);
        }

        /// <summary>
        /// Empty Observable. Returns only OnCompleted on specified scheduler. witness is for type inference.
        /// </summary>
        public static IObservable<T> Empty<T>(IScheduler scheduler, T witness)
        {
            return Empty<T>(scheduler);
        }

        /// <summary>
        /// Non-Terminating Observable. It's no returns, never finish.
        /// </summary>
        public static IObservable<T> Never<T>()
        {
            return Observable.Create<T>(observer => Disposable.Empty);
        }

        /// <summary>
        /// Non-Terminating Observable. It's no returns, never finish. witness is for type inference.
        /// </summary>
        public static IObservable<T> Never<T>(T witness)
        {
            return Never<T>();
        }

        /// <summary>
        /// Return single sequence on ImmediateScheduler.
        /// </summary>
        public static IObservable<T> Return<T>(T value)
        {
            return Return<T>(value, Scheduler.Immediate);
        }

        /// <summary>
        /// Return single sequence on specified scheduler.
        /// </summary>
        public static IObservable<T> Return<T>(T value, IScheduler scheduler)
        {
            return Observable.Create<T>(observer =>
            {
                return scheduler.Schedule(() =>
                {
                    observer.OnNext(value);
                    observer.OnCompleted();
                });
            });
        }

        /// <summary>
        /// Empty Observable. Returns only onError on ImmediateScheduler.
        /// </summary>
        public static IObservable<T> Throw<T>(Exception error)
        {
            return Throw<T>(error, Scheduler.Immediate);
        }

        /// <summary>
        /// Empty Observable. Returns only onError on ImmediateScheduler. witness if for Type inference.
        /// </summary>
        public static IObservable<T> Throw<T>(Exception error, T witness)
        {
            return Throw<T>(error, Scheduler.Immediate);
        }

        /// <summary>
        /// Empty Observable. Returns only onError on specified scheduler.
        /// </summary>
        public static IObservable<T> Throw<T>(Exception error, IScheduler scheduler)
        {
            return Observable.Create<T>(observer =>
            {
                return scheduler.Schedule(() => observer.OnError(error));
            });
        }

        /// <summary>
        /// Empty Observable. Returns only onError on specified scheduler. witness if for Type inference.
        /// </summary>
        public static IObservable<T> Throw<T>(Exception error, IScheduler scheduler, T witness)
        {
            return Throw<T>(error, scheduler);
        }

        public static IObservable<int> Range(int start, int count)
        {
            return Range(start, count, Scheduler.CurrentThread);
        }

        public static IObservable<int> Range(int start, int count, IScheduler scheduler)
        {
            return Observable.Create<int>(observer =>
            {
                return scheduler.Schedule(0, (int i, Action<int> self) =>
                {
                    if (i < count)
                    {
                        int v = start + i;
                        observer.OnNext(v);
                        self(i + 1);
                    }
                    else
                    {
                        observer.OnCompleted();
                    }
                });
            });
        }

        public static IObservable<T> Repeat<T>(T value)
        {
            return Repeat(value, Scheduler.CurrentThread);
        }

        public static IObservable<T> Repeat<T>(T value, IScheduler scheduler)
        {
            if (scheduler == null) throw new ArgumentNullException("scheduler");

            return Observable.Create<T>(observer =>
            {
                return scheduler.Schedule(self =>
                {
                    observer.OnNext(value);
                    self();
                });
            });
        }

        public static IObservable<T> Repeat<T>(T value, int repeatCount)
        {
            return Repeat(value, repeatCount, Scheduler.CurrentThread);
        }

        public static IObservable<T> Repeat<T>(T value, int repeatCount, IScheduler scheduler)
        {
            if (repeatCount < 0) throw new ArgumentOutOfRangeException("repeatCount");
            if (scheduler == null) throw new ArgumentNullException("scheduler");

            return Observable.Create<T>(observer =>
            {
                var currentCount = repeatCount;
                return scheduler.Schedule(self =>
                {
                    if (currentCount > 0)
                    {
                        observer.OnNext(value);
                        currentCount--;
                    }

                    if (currentCount == 0)
                    {
                        observer.OnCompleted();
                        return;
                    }

                    self();
                });
            });
        }

        public static IObservable<T> Repeat<T>(this IObservable<T> source)
        {
            return RepeatInfinite(source).Concat();
        }

        static IEnumerable<IObservable<T>> RepeatInfinite<T>(IObservable<T> source)
        {
            while (true)
            {
                yield return source;
            }
        }

        public static IObservable<T> Defer<T>(Func<IObservable<T>> observableFactory)
        {
            return Observable.Create<T>(observer =>
            {
                IObservable<T> source;
                try
                {
                    source = observableFactory();
                }
                catch (Exception ex)
                {
                    source = Throw<T>(ex);
                }

                return source.Subscribe(observer);
            });
        }

        public static IObservable<T> Start<T>(Func<T> function)
        {
            return Start(function, Scheduler.ThreadPool);
        }

        public static IObservable<T> Start<T>(Func<T> function, IScheduler scheduler)
        {
            return ToAsync(function, scheduler)();
        }

        public static IObservable<Unit> Start(Action action)
        {
            return Start(action, Scheduler.ThreadPool);
        }

        public static IObservable<Unit> Start(Action action, IScheduler scheduler)
        {
            return ToAsync(action, scheduler)();
        }

        public static Func<IObservable<T>> ToAsync<T>(Func<T> function)
        {
            return ToAsync(function, Scheduler.ThreadPool);
        }

        public static Func<IObservable<T>> ToAsync<T>(Func<T> function, IScheduler scheduler)
        {
            return () =>
            {
                var subject = new AsyncSubject<T>();

                scheduler.Schedule(() =>
                {
                    var result = default(T);
                    try
                    {
                        result = function();
                    }
                    catch (Exception exception)
                    {
                        subject.OnError(exception);
                        return;
                    }
                    subject.OnNext(result);
                    subject.OnCompleted();
                });

                return subject.AsObservable();
            };
        }

        public static Func<IObservable<Unit>> ToAsync(Action action)
        {
            return ToAsync(action, Scheduler.ThreadPool);
        }

        public static Func<IObservable<Unit>> ToAsync(Action action, IScheduler scheduler)
        {
            return () =>
            {
                var subject = new AsyncSubject<Unit>();

                scheduler.Schedule(() =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception exception)
                    {
                        subject.OnError(exception);
                        return;
                    }
                    subject.OnNext(Unit.Default);
                    subject.OnCompleted();
                });

                return subject.AsObservable();
            };
        }
    }
}
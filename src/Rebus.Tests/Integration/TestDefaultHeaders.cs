﻿using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Rebus.Configuration;
using Rebus.Shared;
using Rebus.Transports.Msmq;
using Shouldly;
using Rebus.Logging;

namespace Rebus.Tests.Integration
{
    [TestFixture, Category(TestCategories.Integration)]
    public class TestDefaultHeaders : FixtureBase, IDetermineMessageOwnership, IStoreSubscriptions
    {
        const string InputQueueName = "test.defaultheaders.input";
        readonly List<IDisposable> stuffToDispose = new List<IDisposable>();
        BuiltinContainerAdapter adapter;

        protected override void DoSetUp()
        {
            adapter = new BuiltinContainerAdapter();
            stuffToDispose.Add(adapter);

            Configure.With(adapter)
                     .Logging(l => l.None())
                     .MessageOwnership(m => m.Use(this))
                     .Subscriptions(s => s.Use(this))
                     .Transport(t => t.UseMsmq(InputQueueName, "error"))
                     .CreateBus()
                     .Start();
        }

        protected override void DoTearDown()
        {
            stuffToDispose.ForEach(d => d.Dispose());
        }

        [Test]
        public void AddsDefaultHeadersToLocallySentMessage()
        {
            RunTest(() => adapter.Bus.SendLocal("hey!"));
        }

        [Test]
        public void AddsDefaultHeadersToSentMessage()
        {
            RunTest(() => adapter.Bus.Send("hey!"));
        }

        [Test]
        public void AddsDefaultHeadersToPublishedMessage()
        {
            RunTest(() => adapter.Bus.Publish("hey!"));
        }

        [Test]
        public void AddsDefaultHeadersToRoutedSentMessage()
        {
            RunTest(() => adapter.Bus.Advanced.Routing.Send(InputQueueName, "hey!"));
        }

        void RunTest(Action sendAction)
        {
            var my34thBirthday = new DateTime(2013, 03, 19, 0, 0, 0, DateTimeKind.Utc);
            TimeMachine.FixTo(my34thBirthday);

            var resetEvent = new ManualResetEvent(false);
            var timeout = 2.Seconds();
            IDictionary<string, object> headers = null;
            adapter.Handle<string>(str =>
                {
                    headers = MessageContext.GetCurrent()
                                            .Headers;
                    resetEvent.Set();
                });

            sendAction();
            Assert.That(resetEvent.WaitOne(timeout), Is.True, "Did not receive message within {0} timeout", timeout);

            headers.ShouldNotBe(null);

            headers.ShouldContainKeyAndValue(Headers.SendTime, my34thBirthday);
        }

        public string GetEndpointFor(Type messageType)
        {
            return InputQueueName;
        }

        public void Store(Type eventType, string subscriberInputQueue)
        {
        }

        public void Remove(Type eventType, string subscriberInputQueue)
        {
        }

        public string[] GetSubscribers(Type eventType)
        {
            return new[] {InputQueueName};
        }
    }
}
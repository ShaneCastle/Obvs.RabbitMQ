﻿using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using NUnit.Framework;
using Obvs.RabbitMQ.Tests.Messages;
using Obvs.Serialization;
using Obvs.Serialization.Json;
using Obvs.Serialization.ProtoBuf;
using Obvs.Types;
using RabbitMQ.Client;

namespace Obvs.RabbitMQ.Tests
{
    [TestFixture]
    public class TestSendingAndReceiving
    {
        [Test, Explicit]
        [TestCase("Json")]
        [TestCase("ProtoBuf")]
        public void TestSendReceiveAs(string format)
        {
            string exchange = GetType().Name;
            const string routingKeyPrefix = "Messages";

            IConnectionFactory connectionFactory = new ConnectionFactory { HostName = "localhost" };
            IMessageSource<IMessage> messageSource = CreateMessageSource(format, connectionFactory, exchange, routingKeyPrefix);
            IMessagePublisher<IMessage> messagePublisher = CreateMessagePublisher(format, connectionFactory, exchange, routingKeyPrefix);

            List<TestMessage> receivedMessages1 = new List<TestMessage>();
            messageSource.Messages
                         .OfType<TestMessage>()
                         .Subscribe(msg => { Console.WriteLine(msg); receivedMessages1.Add(msg); }, Console.WriteLine, () => Console.WriteLine("Completed!"));

            List<TestMessage> receivedMessages2 = new List<TestMessage>();
            messageSource.Messages
                         .OfType<TestMessage>()
                         .Subscribe(msg => { Console.WriteLine(msg); receivedMessages2.Add(msg); }, Console.WriteLine, () => Console.WriteLine("Completed!"));

            List<TestMessage> messages = new List<TestMessage>
            {
                new TestMessage {Data = "Hello"},
                new TestMessage {Data = "World!"},
                new TestMessage {Data = "..."},
                new TestMessage {Data = "Good"},
                new TestMessage {Data = "Bye"},
                new TestMessage {Data = "Cruel"},
                new TestMessage {Data = "World"}
            };

            foreach (TestMessage message in messages)
            {
                messagePublisher.PublishAsync(message);
            }

            Thread.Sleep(TimeSpan.FromSeconds(1));

            Assert.That(receivedMessages1.Count, Is.EqualTo(7), "Incorrect number of messages received for first subscription");
            Assert.That(receivedMessages2.Count, Is.EqualTo(7), "Incorrect number of messages received for second subscription");

            for (int index = 0; index < messages.Count; index++)
            {
                TestMessage message = messages[index];
                TestMessage received1 = receivedMessages1[index];
                TestMessage received2 = receivedMessages2[index];
                Assert.That(received1.Data == message.Data && received1.Timestamp == message.Timestamp, string.Format("Incorrect message1: {0}", received1));
                Assert.That(received2.Data == message.Data && received2.Timestamp == message.Timestamp, string.Format("Incorrect message2: {0}", received2));
            }
        }

        private static IMessagePublisher<IMessage> CreateMessagePublisher(string format, IConnectionFactory connectionFactory,
            string exchange, string routingKeyPrefix)
        {
            return new MessagePublisher<IMessage>(connectionFactory,
                format == "Json"
                    ? (IMessageSerializer) new JsonMessageSerializer()
                    : new ProtoBufMessageSerializer(),
                exchange, routingKeyPrefix);
        }

        private static IMessageSource<IMessage> CreateMessageSource(string format, IConnectionFactory connectionFactory, string exchange,
            string routingKeyPrefix)
        {
            return new MessageSource<IMessage>(connectionFactory,
                new List<IMessageDeserializer<TestMessage>>
                {
                    format == "Json"
                        ? (IMessageDeserializer<TestMessage>) new JsonMessageDeserializer<TestMessage>()
                        : new ProtoBufMessageDeserializer<TestMessage>()
                },
                exchange, routingKeyPrefix);
        }
    }
}

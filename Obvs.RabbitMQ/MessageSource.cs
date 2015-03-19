﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using Obvs.Types;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Obvs.RabbitMQ
{
    public class MessageSource<TMessage> : IMessageSource<TMessage> where TMessage : IMessage
    {
        private readonly string _exchange;
        private readonly string _routingKeyPrefix;
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public MessageSource(IConnectionFactory connectionFactory, IEnumerable<IMessageDeserializer<TMessage>> deserializers, string exchange, string routingKeyPrefix)
        {
            _exchange = exchange;
            _routingKeyPrefix = routingKeyPrefix;
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());

            if (!_deserializers.Any())
            {
                throw new Exception("You must supply at least one deserializer");
            }

            _connection = connectionFactory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(_exchange, RabbitExchangeTypes.Topic);
        }

        public void Dispose()
        {
            _channel.Close();
            _channel.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        public IObservable<TMessage> Messages
        {
            get
            {
                return Observable.Create<TMessage>(observer =>
                {
                    var consumer = CreateConsumer();

                    IDisposable subscribe = Observable.Defer(() => Observable.StartAsync(consumer.DequeueAsync))
                        .Repeat()
                        .Select(Deserialize)
                        .Subscribe(observer);

                    return Disposable.Create(() =>
                    {
                        consumer.Queue.Close();
                        subscribe.Dispose();
                    });
                });
            }
        }

        private QueueingBasicConsumer CreateConsumer()
        {
            var queue = _channel.QueueDeclare();

            _channel.QueueBind(queue, _exchange, string.Format("{0}.*", _routingKeyPrefix));

            var consumer = new QueueingBasicConsumer(_channel);
            _channel.BasicConsume(queue, true, consumer);
            return consumer;
        }


        private TMessage Deserialize(BasicDeliverEventArgs deliverEventArgs)
        {
            var deserializer = GetDeserializer(deliverEventArgs);

            byte[] body = deliverEventArgs.Body;
            string contentType = deliverEventArgs.BasicProperties.ContentType;

            return contentType == "text" ? deserializer.Deserialize(Encoding.UTF8.GetString(body)) : 
                                           deserializer.Deserialize(body);
        }

        private IMessageDeserializer<TMessage> GetDeserializer(BasicDeliverEventArgs deliverEventArgs)
        {
            string typeName = deliverEventArgs.RoutingKey.Split('.').LastOrDefault();

            return _deserializers.ContainsKey(typeName)
                ? _deserializers[typeName]
                : _deserializers.Values.Single();
        }
    }
}
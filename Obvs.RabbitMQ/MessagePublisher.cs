﻿using System.Text;
using System.Threading.Tasks;
using Obvs.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing;

namespace Obvs.RabbitMQ
{
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : class
    {
        private readonly IMessageSerializer _serializer;
        private readonly string _exchange;
        private readonly string _routingKeyPrefix;
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public MessagePublisher(IConnectionFactory connectionFactory, IMessageSerializer serializer, string exchange, string routingKeyPrefix)
        {
            _serializer = serializer;
            _exchange = exchange;
            _routingKeyPrefix = routingKeyPrefix;
            _connection = connectionFactory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(_exchange, ExchangeType.Topic);
        }

        public Task PublishAsync(TMessage message)
        {
            Publish(message);
            return Task.FromResult(true);
        }

        private void Publish(TMessage message)
        {
            byte[] body = _serializer.Serialize(message);
            _channel.BasicPublish(_exchange, RoutingKey(message), new BasicProperties { ContentType = "bytes" }, body);
        }

        private string RoutingKey(TMessage message)
        {
            return string.Format("{0}.{1}", _routingKeyPrefix, message.GetType().Name);
        }

        public void Dispose()
        {
            if (_channel != null)
            {
                _channel.Close();
                _channel.Dispose();
                _connection.Close();
                _connection.Dispose();
            }
        }
    }
}
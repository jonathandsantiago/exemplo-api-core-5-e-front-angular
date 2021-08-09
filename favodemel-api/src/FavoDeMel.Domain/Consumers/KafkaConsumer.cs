﻿using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FavoDeMel.Domain.Events;
using FavoDeMel.Domain.Models.Settings;
using Microsoft.AspNetCore.Http;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FavoDeMel.Domain.Consumers
{
    public class KafkaConsumer
    {
        private readonly KafkaSettings _kafkaSettings;
        private readonly ConsumerConfig _consumerConfig;

        public KafkaConsumer(KafkaSettings kafkaSettings)
        {
            _kafkaSettings = kafkaSettings;
            _consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _kafkaSettings.BootstrapServers,
                GroupId = Guid.NewGuid().ToString(),
                AutoOffsetReset = AutoOffsetReset.Earliest,
                AllowAutoCreateTopics = true
            };
        }

        public async Task SubscribeConsume(HttpContext context)
        {
            using var consumer = new ConsumerBuilder<Ignore, string>(_consumerConfig).Build();
            try
            {
                MensageriaEvents mensageriaEvent = (MensageriaEvents)context.RequestServices.GetService(typeof(MensageriaEvents));

                await ConfigurarTopic(TopicEvento.FilaPedido);
                consumer.Subscribe(TopicEvento.FilaPedido);
                while (true)
                {
                    var cr = consumer.Consume(CancellationToken.None);
                    if (mensageriaEvent != null)
                    {
                        mensageriaEvent.EnviarMensagem(cr.Message.Value);
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                consumer.Close();
                Console.WriteLine($"Cancelada a execução do Consumer pelo seguinte motivo: {ex.Message}");
            }
        }

        public async Task ConnectWebSocket(HttpContext context, WebSocket webSocket)
        {
            MensageriaEvents mensageriaEvent = (MensageriaEvents)context.RequestServices.GetService(typeof(MensageriaEvents));
            try
            {
                if (mensageriaEvent != null)
                {
                    mensageriaEvent.Mensagem += async (mensagem) =>
                    {
                        await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(mensagem), 0, mensagem.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                    };
                }

                var buffer = new byte[1024 * 4];
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                while (!result.CloseStatus.HasValue)
                {
                    string msg = Encoding.UTF8.GetString(new ArraySegment<byte>(buffer, 0, result.Count));
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                }

                await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error websocket: {ex.Message}");
            }
        }

        private async Task ConfigurarTopic(string topic)
        {
            try
            {
                using var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = _kafkaSettings.BootstrapServers }).Build();
                Metadata metadata = adminClient.GetMetadata(topic, TimeSpan.FromSeconds(120));
                if (metadata == null)
                {
                    await adminClient.CreateTopicsAsync(new TopicSpecification[] { new TopicSpecification { Name = topic, ReplicationFactor = 1, NumPartitions = 1 } });
                }
            }
            catch (CreateTopicsException e)
            {
                Console.WriteLine($"An error occured creating topic {e.Results[0].Topic}: {e.Results[0].Error.Reason}");
            }
        }
    }
}
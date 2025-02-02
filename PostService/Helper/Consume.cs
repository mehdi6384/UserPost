﻿using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using PostService.Data;
using PostService.Entities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace PostService.Helper;


public static class Consume
{
    public static void ListenForIntegrationEvents()
    {
        var factory = new ConnectionFactory();
        var connection = factory.CreateConnection();
        var channel = connection.CreateModel();
        var consumer = new EventingBasicConsumer(channel);

        consumer.Received += (model, ea) =>
        {
            var contextOptions = new DbContextOptionsBuilder<PostServiceContext>()
                .UseInMemoryDatabase("post-db")
                .Options;
            var dbContext = new PostServiceContext(contextOptions);

            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            Console.WriteLine(" [x] Received {0}", message);

            var data = JObject.Parse(message);
            var type = ea.RoutingKey;
            if (type == "user.add")
            {
                if (dbContext.User.Any(a => a.ID == data["id"].Value<int>()))
                {
                    Console.WriteLine("Ignoring old/duplicate entity.");
                }
                else
                {
                    dbContext.User.Add(new User()
                    {
                        ID = data["id"].Value<int>(),
                        Name = data["name"].Value<string>(),
                        Version = data["version"].Value<int>(),
                    });
                    dbContext.SaveChanges();
                }
            }
            else if (type == "user.update")
            {
                int newVersion = data["version"].Value<int>();
                var user = dbContext.User.First(a => a.ID == data["id"].Value<int>());
                if (user.Version >= newVersion)
                {
                    Console.WriteLine("Ignoring old/duplicate entity.");
                }
                else
                {
                    user.Name = data["newname"].Value<string>();
                    user.Version = newVersion;
                    dbContext.SaveChanges();
                }
            }
            channel.BasicAck(ea.DeliveryTag, false);
        };
        channel
            .BasicConsume(
                queue: "user.postservice",
                autoAck: false,
                consumer: consumer
            );
    }
}

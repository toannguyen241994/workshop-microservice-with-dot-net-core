﻿using System;
using System.Diagnostics;
using System.Text;
using EmployeeConsumer.Repositories;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EmployeeConsumer.Infrastructures
{
    public class QueueProcessor
    {
        private readonly QueueConfig _config;
        private ConnectionFactory _connectionFactory;
        private IConnection _connection;
        private IModel _model;
        private EmployeeRepository _employeeRepository;

        public QueueProcessor(QueueConfig config)
        {
            _config = config;

            _connectionFactory = new ConnectionFactory
            {
                HostName = config.HostName,
                UserName = config.UserName,
                Password = config.Password
            };

            _connection = _connectionFactory.CreateConnection("QueueProcessor Connection");
            _model = _connection.CreateModel();
            _model.BasicQos(0, 1, false);

            _model.QueueDeclare(config.QueueName, false, false, true, null);

            _employeeRepository = new EmployeeRepository();
        }

        public void Start()
        {
            var consumer = new EventingBasicConsumer(_model);

            consumer.Received += (model, ea) =>
            {
                var props = ea.BasicProperties;
                var replyProps = _model.CreateBasicProperties();

                var body = ea.Body;
                replyProps.CorrelationId = props.CorrelationId;

                var message = Encoding.UTF8.GetString(body);

                var result = string.Empty;

                Console.WriteLine("*** Processing Request ***");
                Console.WriteLine($"*** Process ID {Process.GetCurrentProcess().Id} ***");
                switch (message)
                {
                    case "employees":
                        Console.WriteLine("Retrieving Employees");
                        result = JsonConvert.SerializeObject(_employeeRepository.Employees);
                        break;
                    default:
                        Console.WriteLine($"Could Not Process: {message}");
                        break;
                }

                var resultBytes = Encoding.UTF8.GetBytes(result);
                _model.BasicPublish("", props.ReplyTo, replyProps, resultBytes);
                _model.BasicAck(ea.DeliveryTag, false);
            };

            _model.BasicConsume(_config.QueueName, false, consumer);

            Console.WriteLine("Press [enter] to exit.");
            Console.ReadLine();

            _model.Dispose();
            _connection.Dispose();
        }
    }
}

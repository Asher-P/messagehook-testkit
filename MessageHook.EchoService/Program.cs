using MessageHook.EchoService.Middlewares;
using MessageHook.EchoService.Serializers;
using KafkaFlow;
using KafkaFlow.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole();

var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
var serializer = new RawBytesSerializer();

builder.Services.AddKafkaFlowHostedService(kafka =>
    kafka.AddCluster(cluster =>
        cluster
            .WithBrokers([bootstrapServers])
            .WithSecurityInformation(info => info.SecurityProtocol = KafkaFlow.Configuration.SecurityProtocol.Plaintext)
            .AddProducer("echo-producer", producer =>
                producer
                    .DefaultTopic("B")
                    .AddMiddlewares(m => m.AddSerializer(resolver => serializer))
            )
            .AddConsumer(consumer =>
                consumer
                    .Topic("A")
                    .WithGroupId("MessageHook-echo-service")
                    .WithBufferSize(100)
                    .WithWorkersCount(2)
                    .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                    .AddMiddlewares(m =>
                    {
                        m.AddSingleTypeDeserializer(resolver => serializer, typeof(byte[]));
                        m.Add<EchoMiddleware>();
                    })
            )
    )
);

var host = builder.Build();
host.Run();

using Garnet;
using Garnet.server;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace StateStore;

public class Program
{
    public static async Task Main(string[] args)
    {
        var garnetPort = 6677;

        _ = Task.Run(async () =>
        {
            var storageDir = Path.Combine(AppContext.BaseDirectory, "storage");
            var garnetServer = new GarnetServer(new GarnetServerOptions
            {
                LogDir = Path.Combine(storageDir, "logs"),
                CheckpointDir = Path.Combine(storageDir, "checkpoints"),
                CheckpointThrottleFlushDelayMs = -1,
                EnableStorageTier = true,
                EnableAOF = true,
                Recover = true,
                Port = garnetPort
            });

            garnetServer.Start();

            await Task.Delay(Timeout.Infinite);
        });

        // wait few seconds for Garnet start
        await Task.Delay(TimeSpan.FromSeconds(2));

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton(await ConnectionMultiplexer.ConnectAsync($"localhost:{garnetPort}"));

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.MapPost("/items", (Item item, [FromServices] ConnectionMultiplexer redis) =>
        {
            var database = redis.GetDatabase();
            database.StringSet(item.Id.ToString(), JsonConvert.SerializeObject(item));
            return Results.Ok(item);
        });

        app.MapGet("/items/{id:int}", (int id, [FromServices] ConnectionMultiplexer redis) =>
        {
            var database = redis.GetDatabase();
            string? value = database.StringGet(id.ToString());
            if (value == null) return Results.NotFound();

            var item = JsonConvert.DeserializeObject<Item>(value);

            return Results.Ok(item);
        });

        await app.RunAsync();
    }
}

public class Item
{
    public required int Id { get; init; }
    public required string Name { get; set; }
}
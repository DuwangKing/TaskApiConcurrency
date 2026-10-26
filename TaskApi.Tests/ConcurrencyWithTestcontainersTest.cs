using Testcontainers.PostgreSql;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

public class ConcurrencyWithTestContainersTest : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine") 
        .WithDatabase("testdb")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private WebApplicationFactory<Program>? _factory;
    //HACK: Опции для корректного Json чтения enum
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        var connectionString = _postgresContainer.GetConnectionString();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<TaskDbContext>>();
                    services.RemoveAll<IRabbitMqPublisher>();

                    services.AddDbContext<TaskDbContext>(opt =>
                        opt.UseNpgsql(connectionString));

                    services.AddSingleton<IRabbitMqPublisher>(new NoOpRabbitMqPublisher());
                });
            });
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task ConcurrentComplete_ShouldReturn409ForSecond()
    {
        var client = _factory!.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/tasks",
            new { title = "Race condition test", priority = "High" });
        
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskItem>(_jsonOptions);
        Assert.NotNull(createdTask);

        var task1 = client.PutAsync($"/tasks/{createdTask!.Id}/complete", null);
        var task2 = client.PutAsync($"/tasks/{createdTask.Id}/complete", null);

        var response1 = await task1;
        var response2 = await task2;

        var statuses = new[] { response1.StatusCode, response2.StatusCode };
        
        Assert.Contains(HttpStatusCode.NoContent, statuses);
        Assert.Contains(HttpStatusCode.Conflict, statuses);
    }
}

public class NoOpRabbitMqPublisher : IRabbitMqPublisher
{
    public void PublishTaskCompleted(Guid taskId, string title, DateTimeOffset completedAt, Priority priority)
    {
        
    }
}
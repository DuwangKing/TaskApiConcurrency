using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TaskDbContext>(opt => 
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
    db.Database.EnsureCreated();
}


app.MapPost("/tasks", async (CreateTaskRequest req, TaskDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Length > 200)
        return Results.BadRequest(new { error = "Title must be 1-200 characters" });

    var task = new TaskItem
    {
        Title = req.Title.Trim(),
        Priority = req.Priority ?? Priority.Medium,
        IsCompleted = false,
        CreatedAt = DateTimeOffset.UtcNow
    };

    db.Tasks.Add(task);
    await db.SaveChangesAsync();
    return Results.Created($"/tasks/{task.Id}", task);
});

app.MapGet("/tasks", async (TaskDbContext db) =>
{
    // HACK: добавить пагинацию, иначе БД умрет + IQueriable
    return Results.Ok(await db.Tasks.ToListAsync());
});

app.MapDelete("/tasks/{id:guid}", async (Guid id, TaskDbContext db) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task is null) return Results.NotFound();

    db.Tasks.Remove(task);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapPut("/tasks/{id:guid}/complete", async (
    Guid id, 
    TaskDbContext db, 
    IRabbitMqPublisher publisher) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task is null) return Results.NotFound();

    if (task.IsCompleted) return Results.Conflict(new { error = "Task already completed" });

    task.IsCompleted = true;
    task.CompletedAt = DateTimeOffset.UtcNow;

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Conflict(new { error = "Concurrency conflict" });
    }

    publisher.PublishTaskCompleted(task.Id, task.Title, task.CompletedAt.Value, task.Priority);

    return Results.NoContent();
});

public record CreateTaskRequest(string Title, Priority? Priority = null);
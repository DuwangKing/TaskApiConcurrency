using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TaskDbContext>(opt => 
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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
    // HACK: добавить пагинацию, иначе БД умрет
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

public record CreateTaskRequest(string Title, Priority? Priority = null);
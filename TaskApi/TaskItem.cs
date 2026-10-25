public enum Priority { Low, Medium, High }

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; } 
    public Priority Priority { get; set; } = Priority.Medium;
    public byte[] RowVersion { get; set; } = null!; 
}
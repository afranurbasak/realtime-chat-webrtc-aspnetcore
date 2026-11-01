using Microsoft.EntityFrameworkCore;
using RealtimeChat.Models;

namespace RealtimeChat.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Message> Messages => Set<Message>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}

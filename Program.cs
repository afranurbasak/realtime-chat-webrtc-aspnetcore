using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RealtimeChat.Data;
using RealtimeChat.Models;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite("Data Source=chat.db");
});

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "uploads"));

    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN ProfileImage TEXT NOT NULL DEFAULT ''");
    }
    catch
    {
        // Kolon zaten varsa hata vermesin diye boş bırakıyoruz.
    }

    //db.Messages.RemoveRange(db.Messages);//silme butonu vs
    //db.SaveChanges();//silme butonu vs

    if (!db.Rooms.Any())
    {
        db.Rooms.AddRange(
            new Room { Name = "#genel" },
            new Room { Name = "#ders" },
            new Room { Name = "#yardim" },
            new Room { Name = "#proje" }
        );

        db.SaveChanges();
    }
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSession();
app.UseWebSockets();

var passwordHasher = new PasswordHasher<User>();

var sockets = new List<ClientSocket>();
var socketsLock = new object();

app.MapPost("/api/register", async (RegisterRequest request, AppDbContext db) =>
{
    var username = request.Username.Trim();

    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Kullanıcı adı ve şifre boş olamaz." });
    }

    var exists = await db.Users.AnyAsync(u => u.Username == username);

    if (exists)
    {
        return Results.BadRequest(new { message = "Bu kullanıcı adı zaten kullanılıyor." });
    }

    var user = new User
    {
        Username = username,
        CreatedAt = DateTime.Now,
        IsOnline = false
    };

    user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Kayıt başarılı.", username = user.Username });
});

app.MapPost("/api/login", async (LoginRequest request, AppDbContext db, HttpContext http) =>
{
    var username = request.Username.Trim();

    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);

    if (user == null)
    {
        return Results.BadRequest(new { message = "Kullanıcı bulunamadı." });
    }

    var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

    if (result == PasswordVerificationResult.Failed)
    {
        return Results.BadRequest(new { message = "Şifre hatalı." });
    }

    user.IsOnline = true;
    await db.SaveChangesAsync();

    http.Session.SetInt32("UserId", user.Id);
    http.Session.SetString("Username", user.Username);

    return Results.Ok(new { message = "Giriş başarılı.", username = user.Username });
});

app.MapGet("/api/me", (HttpContext http) =>
{
    var username = http.Session.GetString("Username");

    if (string.IsNullOrEmpty(username))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new { username });
});

app.MapPost("/api/logout", async (HttpContext http, AppDbContext db) =>
{
    var userId = http.Session.GetInt32("UserId");

    if (userId != null)
    {
        var user = await db.Users.FindAsync(userId.Value);

        if (user != null)
        {
            user.IsOnline = false;
            await db.SaveChangesAsync();
        }
    }

    http.Session.Clear();

    return Results.Ok(new { message = "Çıkış yapıldı." });
});

app.MapGet("/api/messages/{room}", async (string room, AppDbContext db, HttpContext http) =>
{
    var username = http.Session.GetString("Username");

    if (string.IsNullOrEmpty(username))
    {
        return Results.Unauthorized();
    }

    room = "#" + room;

    var messages = await db.Messages
        .Where(m => m.RoomName == room)
        .OrderBy(m => m.SentAt)
        .Take(100)
        .Select(m => new
        {
            username = m.SenderUsername,
            text = m.Text,
            time = m.SentAt.ToString("HH:mm")
        })
        .ToListAsync();

    return Results.Ok(messages);
});

app.MapPost("/api/upload", async (HttpRequest request, HttpContext http, IWebHostEnvironment env) =>
{
    var username = http.Session.GetString("Username");

    if (string.IsNullOrEmpty(username))
    {
        return Results.Unauthorized();
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();

    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new { message = "Dosya seçilmedi." });
    }

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!AllowedUploadExtensions.Contains(ext) || file.Length > MaxUploadBytes)
    {
        return Results.BadRequest(new { message = "Desteklenmeyen dosya türü veya boyutu." });
    }

    var uploadsPath = Path.Combine(env.WebRootPath, "uploads");
    Directory.CreateDirectory(uploadsPath);

    var fileName = Guid.NewGuid().ToString() + ext;
    var filePath = Path.Combine(uploadsPath, fileName);

    using (var stream = new FileStream(filePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    return Results.Ok(new
    {
        fileName = file.FileName,
        url = "/uploads/" + fileName
    });
});

app.MapPost("/api/profile-image", async (HttpRequest request, HttpContext http, AppDbContext db, IWebHostEnvironment env) =>
{
    var username = http.Session.GetString("Username");

    if (string.IsNullOrEmpty(username))
    {
        return Results.Unauthorized();
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();

    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new { message = "Profil resmi seçilmedi." });
    }

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!AllowedUploadExtensions.Contains(ext) || file.Length > MaxUploadBytes)
    {
        return Results.BadRequest(new { message = "Desteklenmeyen dosya türü veya boyutu." });
    }

    var uploadsPath = Path.Combine(env.WebRootPath, "uploads");
    Directory.CreateDirectory(uploadsPath);

    var fileName = "profile_" + Guid.NewGuid().ToString() + ext;
    var filePath = Path.Combine(uploadsPath, fileName);

    using (var stream = new FileStream(filePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);

    if (user == null)
    {
        return Results.NotFound();
    }

    user.ProfileImage = "/uploads/" + fileName;
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        message = "Profil resmi güncellendi.",
        url = user.ProfileImage
    });
});

app.MapGet("/api/rooms", async (AppDbContext db, HttpContext http) =>
{
    var username = http.Session.GetString("Username");

    if (string.IsNullOrEmpty(username))
    {
        return Results.Unauthorized();
    }

    var rooms = await db.Rooms
        .OrderBy(r => r.Name)
        .Select(r => r.Name)
        .ToListAsync();

    return Results.Ok(rooms);
});

app.MapPost("/api/rooms", async (CreateRoomRequest request, AppDbContext db, HttpContext http) =>
{
    var username = http.Session.GetString("Username");

    if (string.IsNullOrEmpty(username))
    {
        return Results.Unauthorized();
    }

    var roomName = request.Name.Trim();

    if (string.IsNullOrWhiteSpace(roomName))
    {
        return Results.BadRequest(new { message = "Oda adı boş olamaz." });
    }

    if (!roomName.StartsWith("#"))
    {
        roomName = "#" + roomName;
    }

    var exists = await db.Rooms.AnyAsync(r => r.Name == roomName);

    if (exists)
    {
        return Results.BadRequest(new { message = "Bu oda zaten var." });
    }

    db.Rooms.Add(new Room
    {
        Name = roomName,
        CreatedAt = DateTime.Now
    });

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        message = "Oda oluşturuldu.",
        room = roomName
    });
});

app.MapGet("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var username = context.Session.GetString("Username");

    if (string.IsNullOrEmpty(username))
    {
        context.Response.StatusCode = 401;
        return;
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();

    var client = new ClientSocket
    {
        Username = username,
        Room = "#genel",
        Socket = socket
    };

    lock (socketsLock)
    {
        sockets.Add(client);
    }

    Console.WriteLine($"{username} WebSocket ile bağlandı.");
    await BroadcastOnlineUsers();

    try
    {
        while (socket.State == WebSocketState.Open)
        {
            var json = await ReceiveFullMessage(socket);

            if (json == null)
            {
                break;
            }

            Console.WriteLine("Gelen WS mesajı: " + json[..Math.Min(json.Length, 80)]);

            var message = JsonSerializer.Deserialize<ClientMessage>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );

            if (message == null) continue;

            if (message.Type == "join-room")
            {
                client.Room = message.Room;
                await BroadcastOnlineUsers();
            }
            else if (message.Type == "chat")
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var newMessage = new Message
                {
                    RoomName = client.Room,
                    SenderUsername = client.Username,
                    Text = message.Text,
                    SentAt = DateTime.Now
                };

                db.Messages.Add(newMessage);
                await db.SaveChangesAsync();

                var outgoing = new
                {
                    type = "chat",
                    room = client.Room,
                    username = client.Username,
                    text = message.Text,
                    time = newMessage.SentAt.ToString("HH:mm")
                };

                await BroadcastToRoom(client.Room, outgoing);
            }

            else if (message.Type == "private-chat")
            {
                var outgoing = new
                {
                    type = "private-chat",
                    from = client.Username,
                    to = message.To,
                    text = message.Text,
                    time = DateTime.Now.ToString("HH:mm")
                };

                await SendToUser(message.To, outgoing);
                await SendToUser(client.Username, outgoing);
            }

            else if (
                message.Type == "invite" ||
                message.Type == "invite-accepted" ||
                message.Type == "invite-rejected" ||
                message.Type == "offer" ||
                message.Type == "answer" ||
                message.Type == "candidate" ||
                message.Type == "call-ended")
            {
                var outgoing = new
                {
                    type = message.Type,
                    username = client.Username,
                    offer = message.Offer,
                    answer = message.Answer,
                    candidate = message.Candidate
                };

                await BroadcastToRoomExceptSender(client.Room, client, outgoing);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("WebSocket hata: " + ex.Message);
    }
    finally
    {
        lock (socketsLock)
        {
            sockets.Remove(client);
        }

        Console.WriteLine($"{username} WebSocket bağlantısından ayrıldı.");
        await BroadcastOnlineUsers();

        if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
        {
            await socket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Bağlantı kapandı",
                CancellationToken.None
            );
        }
    }
});

async Task<string?> ReceiveFullMessage(WebSocket socket)
{
    var buffer = new byte[8192];

    using var ms = new MemoryStream();

    while (true)
    {
        var result = await socket.ReceiveAsync(
            new ArraySegment<byte>(buffer),
            CancellationToken.None
        );

        if (result.MessageType == WebSocketMessageType.Close)
        {
            return null;
        }

        ms.Write(buffer, 0, result.Count);

        if (result.EndOfMessage)
        {
            break;
        }
    }

    return Encoding.UTF8.GetString(ms.ToArray());
}

async Task BroadcastToRoom(string room, object data)
{
    var json = JsonSerializer.Serialize(data);
    var bytes = Encoding.UTF8.GetBytes(json);

    List<ClientSocket> targets;

    lock (socketsLock)
    {
        targets = sockets
            .Where(s => s.Room == room && s.Socket.State == WebSocketState.Open)
            .ToList();
    }

    foreach (var target in targets)
    {
        await target.Socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }
}

async Task BroadcastToRoomExceptSender(string room, ClientSocket sender, object data)
{
    var json = JsonSerializer.Serialize(data);
    var bytes = Encoding.UTF8.GetBytes(json);

    List<ClientSocket> targets;

    lock (socketsLock)
    {
        targets = sockets
            .Where(s => s.Room == room && s != sender && s.Socket.State == WebSocketState.Open)
            .ToList();
    }

    foreach (var target in targets)
    {
        await target.Socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }
}

async Task SendToUser(string username, object data)
{
    var json = JsonSerializer.Serialize(data);
    var bytes = Encoding.UTF8.GetBytes(json);

    List<ClientSocket> targets;

    lock (socketsLock)
    {
        targets = sockets
            .Where(s => s.Username == username && s.Socket.State == WebSocketState.Open)
            .ToList();
    }

    foreach (var target in targets)
    {
        await target.Socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }
}

async Task BroadcastOnlineUsers()
{
    List<ClientSocket> targets;

    lock (socketsLock)
    {
        targets = sockets
            .Where(s => s.Socket.State == WebSocketState.Open)
            .ToList();
    }

    var users = targets
        .Select(s => s.Username)
        .Distinct()
        .ToList();

    var data = new
    {
        type = "online-users",
        users
    };

    var json = JsonSerializer.Serialize(data);
    var bytes = Encoding.UTF8.GetBytes(json);

    foreach (var target in targets)
    {
        await target.Socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }
}

app.Run();

partial class Program
{
    static readonly HashSet<string> AllowedUploadExtensions = new() { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".pdf", ".txt" };
    const long MaxUploadBytes = 10 * 1024 * 1024;
}

record RegisterRequest(string Username, string Password);
record LoginRequest(string Username, string Password);
record CreateRoomRequest(string Name);

class ClientSocket
{
    public string Username { get; set; } = "";
    public string Room { get; set; } = "#genel";
    public WebSocket Socket { get; set; } = default!;
}

class ClientMessage
{
    public string Type { get; set; } = "";
    public string Room { get; set; } = "#genel";
    public string Text { get; set; } = "";

    public string To { get; set; } = "";

    public JsonElement? Offer { get; set; }
    public JsonElement? Answer { get; set; }
    public JsonElement? Candidate { get; set; }
}
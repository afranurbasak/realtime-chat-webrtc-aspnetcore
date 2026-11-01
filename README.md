# Real-Time Chat & Video Calls (ASP.NET Core + WebRTC)

IRC-style web chat platform: users register and sign in, join or create rooms, send real-time and private messages, and start peer-to-peer video calls with WebRTC — all served from a single ASP.NET Core 9 application.

## Features

- **Accounts:** registration, login, logout, session-based auth, unique usernames, passwords hashed with ASP.NET Core Identity `PasswordHasher`
- **Rooms:** default rooms, room creation, joining/leaving, per-room online user list
- **Messaging:** real-time room messages over WebSockets, private messages, timestamps, emoji support, message history persisted in SQLite
- **Video calls:** camera/microphone access, call invitations (accept / reject), end call — WebRTC with the WebSocket server used for signaling

## Architecture

```
Browser (HTML/CSS/JS) ──HTTP──▶ Minimal API (/api/register, /api/login, /api/rooms, /api/messages/{room} …)
        │                                   │
        └──WebSocket /ws──▶ chat + presence + WebRTC signaling ──▶ EF Core ──▶ SQLite
        └──────── WebRTC media (peer-to-peer) ────────┘
```

## Tech stack

C# · .NET 9 · ASP.NET Core Minimal APIs · Entity Framework Core (SQLite) · WebSockets · WebRTC · JavaScript

## Getting started

```bash
dotnet restore
dotnet run
```

Open the URL printed in the terminal (e.g. `http://localhost:5041`). The SQLite database (`chat.db`) is created automatically on first run. To test a call, open the app in two browsers (e.g. Chrome and Firefox) and sign in with two accounts.

## Project structure

```
Program.cs          # endpoints, WebSocket handling, signaling
Data/AppDbContext.cs
Models/             # User, Room, Message
wwwroot/            # client (index.html, app.js, style.css)
```

## Notes

- Uploads are restricted to common image/document types and 10 MB.
- Calls use a public STUN server only; calls across restrictive NATs would need a TURN server.

UI text is in Turkish.

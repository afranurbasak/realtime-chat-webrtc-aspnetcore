# Real-Time Chat & Video Calls

## 🇹🇷 Türkçe

### Proje Hakkında
IRC mantığına benzer şekilde çalışan, web tarayıcısı üzerinden kullanılan bir sohbet ve görüntülü görüşme platformudur. Kullanıcılar kayıt olup giriş yapar, sohbet odalarına katılır veya yeni oda oluşturur, gerçek zamanlı ve özel mesajlaşır, çevrim içi kullanıcılarla WebRTC üzerinden eşler arası (peer-to-peer) görüntülü görüşme başlatır. Uygulamanın tamamı tek bir ASP.NET Core 9 projesinden sunulur.

Çalışma mantığı:
- Kayıt, giriş, oda ve mesaj geçmişi işlemleri Minimal API uç noktaları ile yapılır; veriler Entity Framework Core aracılığıyla SQLite veritabanında tutulur.
- Gerçek zamanlı mesajlar, çevrim içi durum bilgisi ve WebRTC sinyalleşmesi (`offer`, `answer`, `candidate`, davet mesajları) tek bir `/ws` WebSocket bağlantısı üzerinden taşınır.
- Görüntü ve ses, bağlantı kurulduktan sonra doğrudan tarayıcılar arasında aktarılır.

### Özellikler
- **Kullanıcı yönetimi:** kayıt, giriş, çıkış, oturum (session) yönetimi, benzersiz kullanıcı adı, ASP.NET Core Identity `PasswordHasher` ile şifre hashleme, çevrim içi durum bilgisi
- **Odalar:** varsayılan odalar (`#genel`, `#ders`, `#yardim`, `#proje`), yeni oda oluşturma, odaya katılma ve ayrılma, oda içi kullanıcı listesi
- **Mesajlaşma:** gerçek zamanlı oda mesajları, özel mesajlar, mesaj zamanı ve gönderen bilgisi, emoji desteği, mesaj geçmişinin veritabanında saklanması
- **Görüntülü görüşme:** kamera ve mikrofon erişimi, görüşme daveti gönderme, daveti kabul etme veya reddetme, görüşmeyi sonlandırma

### Kullanılan Teknolojiler
C#, .NET 9, ASP.NET Core Minimal API, Entity Framework Core (SQLite), WebSocket, WebRTC, HTML, CSS, JavaScript

### Kurulum
Gereksinim: [.NET 9 SDK](https://dotnet.microsoft.com/download)
```bash
git clone https://github.com/afranurbasak/realtime-chat-webrtc-aspnetcore.git
cd realtime-chat-webrtc-aspnetcore
dotnet restore
```

### Kullanım
```bash
dotnet run
```
Terminalde görünen adresi (ör. `http://localhost:5041`) tarayıcıda açın. SQLite veritabanı (`chat.db`) ilk çalıştırmada otomatik oluşturulur. Görüntülü görüşmeyi denemek için uygulamayı iki farklı tarayıcıda (ör. Chrome ve Firefox) açıp iki ayrı hesapla giriş yapın.

### Proje Yapısı
```
Program.cs            # API uç noktaları, WebSocket ve sinyalleşme
Data/AppDbContext.cs  # EF Core veritabanı bağlamı
Models/               # User, Room, Message
wwwroot/              # istemci (index.html, app.js, style.css)
```

### Notlar
- Sunucuda dosya ve profil fotoğrafı yükleme uç noktaları da bulunur; bunlar yalnızca yaygın görsel/belge türlerini ve en fazla 10 MB'ı kabul eder.
- Bağlantılar yalnızca herkese açık bir STUN sunucusu kullanır; kısıtlayıcı ağlarda TURN sunucusu gerekebilir.
- Arayüz metinleri Türkçedir.

## 🇬🇧 English

### About the Project
A browser-based chat and video-calling platform that works in a similar way to IRC. Users register and sign in, join or create chat rooms, exchange real-time and private messages, and start peer-to-peer video calls with online users over WebRTC. The whole application is served from a single ASP.NET Core 9 project.

How it works:
- Registration, login, rooms and message history are handled by Minimal API endpoints; data is stored in SQLite through Entity Framework Core.
- Real-time messages, presence updates and WebRTC signalling (`offer`, `answer`, `candidate` and invitation messages) travel over a single `/ws` WebSocket connection.
- Once connected, audio and video flow directly between the browsers.

### Features
- **User management:** registration, login, logout, session management, unique usernames, password hashing with ASP.NET Core Identity `PasswordHasher`, online status
- **Rooms:** default rooms (`#genel`, `#ders`, `#yardim`, `#proje`), room creation, joining and leaving rooms, per-room user list
- **Messaging:** real-time room messages, private messages, timestamps and sender information, emoji support, message history stored in the database
- **Video calls:** camera and microphone access, sending call invitations, accepting or rejecting invitations, ending calls

### Technologies
C#, .NET 9, ASP.NET Core Minimal API, Entity Framework Core (SQLite), WebSocket, WebRTC, HTML, CSS, JavaScript

### Installation
Requirement: [.NET 9 SDK](https://dotnet.microsoft.com/download)
```bash
git clone https://github.com/afranurbasak/realtime-chat-webrtc-aspnetcore.git
cd realtime-chat-webrtc-aspnetcore
dotnet restore
```

### Usage
```bash
dotnet run
```
Open the address printed in the terminal (e.g. `http://localhost:5041`). The SQLite database (`chat.db`) is created automatically on first run. To try a video call, open the app in two different browsers (e.g. Chrome and Firefox) and sign in with two accounts.

### Project Structure
```
Program.cs            # API endpoints, WebSocket and signalling
Data/AppDbContext.cs  # EF Core database context
Models/               # User, Room, Message
wwwroot/              # client (index.html, app.js, style.css)
```

### Notes
- The server also exposes file and profile-picture upload endpoints; they accept only common image/document types up to 10 MB.
- Calls use a public STUN server only; restrictive networks may require a TURN server.
- The user interface is in Turkish.

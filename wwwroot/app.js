let aktifKullanici = "";
let aktifOda = "#genel";
let socket = null;
let ozelMesajHedef = "";

window.onload = async function () {
    await oturumKontrolEt();
};

async function kayitOl() {
    const username = document.getElementById("kullaniciAdi").value;
    const password = document.getElementById("sifre").value;

    const response = await fetch("/api/register", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({ username, password })
    });

    const data = await response.json();
    document.getElementById("sonuc").innerText = data.message;
}

async function girisYap() {
    const username = document.getElementById("kullaniciAdi").value;
    const password = document.getElementById("sifre").value;

    const response = await fetch("/api/login", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({ username, password })
    });

    const data = await response.json();

    if (response.ok) {
        aktifKullanici = data.username;
        sohbetEkraniniAc();
    } else {
        document.getElementById("sonuc").innerText = data.message;
    }
}

async function oturumKontrolEt() {
    const response = await fetch("/api/me");

    if (response.ok) {
        const data = await response.json();
        aktifKullanici = data.username;
        sohbetEkraniniAc();
    }
}

function sohbetEkraniniAc() {
    document.getElementById("authEkrani").classList.add("gizli");
    document.getElementById("sohbetEkrani").classList.remove("gizli");

    document.getElementById("aktifKullanici").innerText = aktifKullanici;

    odalariYukle();
    odaSec("#genel");
    websocketBaslat();
}

function websocketBaslat() {
    return new Promise((resolve, reject) => {
        if (socket && socket.readyState === WebSocket.OPEN) {
            resolve();
            return;
        }

        const protocol = location.protocol === "https:" ? "wss" : "ws";
        socket = new WebSocket(`${protocol}://${location.host}/ws`);

        socket.onopen = function () {
            socket.send(JSON.stringify({
                type: "join-room",
                room: aktifOda
            }));

            resolve();
        };

        socket.onmessage = async function (event) {
            const data = JSON.parse(event.data);

            if (data.type === "chat") {
                mesajEkle(data.username, data.text, data.time);
            }

            if (data.type === "online-users") {
                onlineKullanicilariGoster(data.users);
            }

            if (data.type === "private-chat") {
                mesajAlani = document.getElementById("mesajAlani");

                mesajAlani.innerHTML += `
                    <div class="mesaj ozel-mesaj">
                        <strong>Özel Mesaj - ${data.from} → ${data.to}</strong>
                        <span class="saat">${data.time}</span>
                        <p>${data.text}</p>
                    </div>
                `;

                mesajAlani.scrollTop = mesajAlani.scrollHeight;
            }

            if (data.type === "offer") {
                await gelenOfferIsle(data.offer);
            }

            if (data.type === "answer") {
                await gelenAnswerIsle(data.answer);
            }

            if (data.type === "candidate") {
                await gelenCandidateIsle(data.candidate);
            }

            if (data.type === "call-ended") {
                if (peer) {
                    peer.close();
                    peer = null;
                }

                if (document.getElementById("uzakVideo")) {
                    document.getElementById("uzakVideo").srcObject = null;
                }

                gorusmeDurumuYaz("Karşı taraf görüşmeyi sonlandırdı");
            }

            if (data.type === "invite")
            {
                const kabul = confirm(
                    `${data.username} sizi görüntülü görüşmeye davet ediyor.\n\nKabul ediyor musunuz?`
                );

                if (kabul)
                {
                    gorusmeDurumuYaz("Görüşme daveti kabul edildi");

                    socket.send(JSON.stringify({
                        type: "invite-accepted"
                    }));
                }

                else
                {
                    socket.send(JSON.stringify({
                        type: "invite-rejected"
                    }));
                }
            }

            if (data.type === "invite-accepted")
            {
                gorusmeDurumuYaz("Karşı taraf daveti kabul etti, bağlantı kuruluyor");

                alert("Karşı taraf daveti kabul etti.");

                await peerOlustur();

                const offer = await peer.createOffer();

                await peer.setLocalDescription(offer);

                socket.send(JSON.stringify({
                    type: "offer",
                    offer: offer
                }));

                gorusmeDurumuYaz("Görüşme daveti kabul edildi");
            }

            if (data.type === "invite-rejected")
            {
                alert("Karşı taraf daveti reddetti.");

                gorusmeDurumuYaz("Görüşme daveti reddedildi");
            }
        };

        socket.onclose = function () {
            console.log("WebSocket bağlantısı kapandı");

            const mesajAlani = document.getElementById("mesajAlani");

            if (mesajAlani) {
                mesajAlani.innerHTML += `
                    <div class="sistem-mesaji">
                        Sunucu bağlantısı kapandı. Sayfayı yenileyerek tekrar bağlanabilirsiniz.
                    </div>
                `;
            }
        };

        socket.onerror = function (error) {
            console.error("WebSocket hatası:", error);
            reject(error);
        };
    });
}

async function odaSec(odaAdi) {
    aktifOda = odaAdi;

    document.getElementById("aktifOdaBaslik").innerText = odaAdi;

    document.getElementById("mesajAlani").innerHTML =
        `<div class="sistem-mesaji">${odaAdi} odasına katıldınız.</div>`;

    await mesajGecmisiniYukle(odaAdi);

    if (socket && socket.readyState === WebSocket.OPEN) {
        socket.send(JSON.stringify({
            type: "join-room",
            room: aktifOda
        }));
    }
}

function mesajGonder() {
    const mesajKutusu = document.getElementById("mesajKutusu");
    const mesaj = mesajKutusu.value.trim();

    if (mesaj === "") return;

    if (!socket || socket.readyState !== WebSocket.OPEN) {
        alert("Sunucu bağlantısı hazır değil.");
        return;
    }

    socket.send(JSON.stringify({
        type: "chat",
        room: aktifOda,
        text: mesaj
    }));

    mesajKutusu.value = "";
}

function mesajEkle(kullanici, mesaj, saat) {
    const mesajAlani = document.getElementById("mesajAlani");

    mesajAlani.innerHTML += `
        <div class="mesaj">
            <strong>${kullanici}</strong>
            <span class="saat">${saat}</span>
            <p>${mesaj}</p>
        </div>
    `;

    mesajAlani.scrollTop = mesajAlani.scrollHeight;
}

function onlineKullanicilariGoster(users) {
    const liste = document.getElementById("kullaniciListesi");

    liste.innerHTML = "";

    users.forEach(user => {
        const li = document.createElement("li");
        li.innerText = "● " + user;

        if (user !== aktifKullanici) {
            li.style.cursor = "pointer";

            li.onclick = function () {
                ozelMesajHedef = user;
                document.getElementById("ozelMesajHedefi").innerText = user;
            };
        }

        liste.appendChild(li);
    });
}

async function cikisYap() {
    gorusmeBitir();
    
    if (socket) {
        socket.close();
    }

    await fetch("/api/logout", {
        method: "POST"
    });

    aktifKullanici = "";

    document.getElementById("sohbetEkrani").classList.add("gizli");
    document.getElementById("authEkrani").classList.remove("gizli");

    document.getElementById("sonuc").innerText = "Çıkış yapıldı.";
}

let yerelAkis = null;
let peer = null;
let bekleyenAdaylar = [];
let mikrofonKapali = false;

async function kamerayiAc() {
    if (yerelAkis) return yerelAkis;

    try {
        yerelAkis = await navigator.mediaDevices.getUserMedia({
            video: true,
            audio: true
        });

        document.getElementById("kendiVideo").srcObject = yerelAkis;
        gorusmeDurumuYaz("Kamera ve mikrofon açıldı");
        return yerelAkis;
    } catch (error) {
        alert("Kamera veya mikrofon açılamadı.");
        console.error(error);
    }
}

async function peerOlustur() {
    if (peer) return peer;

    peer = new RTCPeerConnection({
        iceServers: [
            { urls: "stun:stun.l.google.com:19302" }
        ]
    });

    peer.ontrack = function (event) {
        document.getElementById("uzakVideo").srcObject = event.streams[0];
        gorusmeDurumuYaz("Karşı taraf görüntüsü alındı");
    };

    peer.onicecandidate = function (event) {
        if (event.candidate) {
            socket.send(JSON.stringify({
                type: "candidate",
                candidate: event.candidate
            }));
        }
    };

    const akis = await kamerayiAc();

    akis.getTracks().forEach(track => {
        peer.addTrack(track, akis);
    });

    return peer;
}

async function gorusmeBaslat()
{
    if (!socket || socket.readyState !== WebSocket.OPEN)
    {
        alert("Bağlantı hazır değil.");
        return;
    }

    socket.send(JSON.stringify({
        type: "invite"
    }));

    gorusmeDurumuYaz("Görüşme daveti gönderildi");

    alert("Görüşme daveti gönderildi.");
}

async function gelenOfferIsle(offer) {
    gorusmeDurumuYaz("Görüşme isteği alındı, bağlantı kuruluyor");

    await peerOlustur();

    await peer.setRemoteDescription(new RTCSessionDescription(offer));

    const answer = await peer.createAnswer();
    await peer.setLocalDescription(answer);

    socket.send(JSON.stringify({
        type: "answer",
        answer: answer
    }));

    await bekleyenAdaylariEkle();
}

async function gelenAnswerIsle(answer) {
    if (!peer) return;

    await peer.setRemoteDescription(new RTCSessionDescription(answer));

    await bekleyenAdaylariEkle();
}

async function gelenCandidateIsle(candidate) {
    if (peer && peer.remoteDescription) {
        try {
            await peer.addIceCandidate(new RTCIceCandidate(candidate));
        } catch (error) {
            console.error("ICE candidate eklenemedi:", error);
        }
    } else {
        bekleyenAdaylar.push(candidate);
    }
}

async function bekleyenAdaylariEkle() {
    while (bekleyenAdaylar.length > 0) {
        const candidate = bekleyenAdaylar.shift();

        try {
            await peer.addIceCandidate(new RTCIceCandidate(candidate));
        } catch (error) {
            console.error("Bekleyen ICE candidate eklenemedi:", error);
        }
    }
}

function gorusmeBitir() {
    if (peer) {
        peer.close();
        peer = null;
    }

    if (document.getElementById("uzakVideo")) {
        document.getElementById("uzakVideo").srcObject = null;
    }

    if (yerelAkis) {
        yerelAkis.getTracks().forEach(track => {
            track.stop();
        });

        yerelAkis = null;
    }

    if (document.getElementById("kendiVideo")) {
        document.getElementById("kendiVideo").srcObject = null;
    }

    gorusmeDurumuYaz("Görüşme sonlandırıldı, kamera ve mikrofon kapatıldı");

    if (socket && socket.readyState === WebSocket.OPEN) {
        socket.send(JSON.stringify({
            type: "call-ended"
        }));
    }
}

async function mesajGecmisiniYukle(odaAdi) {
    const temizOdaAdi = odaAdi.replace("#", "");

    const response = await fetch(`/api/messages/${temizOdaAdi}`);

    if (!response.ok) {
        return;
    }

    const messages = await response.json();

    messages.forEach(m => {
        mesajEkle(m.username, m.text, m.time);
    });
}

function gorusmeDurumuYaz(metin) {
    const alan = document.getElementById("gorusmeDurumu");

    if (alan) {
        alan.innerText = "Görüşme durumu: " + metin;
    }
}

function ozelMesajGonder() {
    const kutu = document.getElementById("ozelMesajKutusu");
    const mesaj = kutu.value.trim();

    if (!ozelMesajHedef) {
        alert("Önce sağdan bir kullanıcı seç.");
        return;
    }

    if (mesaj === "") {
        return;
    }

    if (!socket || socket.readyState !== WebSocket.OPEN) {
        alert("Sunucu bağlantısı hazır değil.");
        return;
    }

    socket.send(JSON.stringify({
        type: "private-chat",
        to: ozelMesajHedef,
        text: mesaj
    }));

    kutu.value = "";
}

async function odalariYukle() {
    const response = await fetch("/api/rooms");

    if (!response.ok) {
        return;
    }

    const rooms = await response.json();
    const liste = document.getElementById("odaListesi");

    liste.innerHTML = "";

    rooms.forEach(room => {
        const li = document.createElement("li");
        li.innerText = room;
        li.onclick = function () {
            odaSec(room);
        };

        liste.appendChild(li);
    });
}

async function odaOlustur() {
    const input = document.getElementById("yeniOdaAdi");
    const name = input.value.trim();

    if (name === "") {
        alert("Oda adı boş olamaz.");
        return;
    }

    const response = await fetch("/api/rooms", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({ name })
    });

    const data = await response.json();

    if (!response.ok) {
        alert(data.message);
        return;
    }

    input.value = "";

    await odalariYukle();
    odaSec(data.room);

    alert("Yeni oda oluşturuldu: " + data.room);
}

function odadanAyril() {
    const eskiOda = aktifOda;

    aktifOda = "";

    document.getElementById("aktifOdaBaslik").innerText = "Oda seçilmedi";

    document.getElementById("mesajAlani").innerHTML =
        `<div class="sistem-mesaji">${eskiOda} odasından ayrıldınız. Soldan yeni bir oda seçebilirsiniz.</div>`;

    if (socket && socket.readyState === WebSocket.OPEN) {
        socket.send(JSON.stringify({
            type: "join-room",
            room: ""
        }));
    }
}

document.addEventListener("keydown", function (event) {
    const aktifElement = document.activeElement;

    if (event.key === "Enter" && aktifElement.id === "mesajKutusu") {
        event.preventDefault();
        mesajGonder();
    }

    if (event.key === "Enter" && aktifElement.id === "ozelMesajKutusu") {
        event.preventDefault();
        ozelMesajGonder();
    }
});

function emojiEkle(emoji) {
    const kutu = document.getElementById("mesajKutusu");
    kutu.value += emoji;
    kutu.focus();
}

function emojiEkleOzel(emoji) {
    const kutu = document.getElementById("ozelMesajKutusu");
    kutu.value += emoji;
    kutu.focus();
}

function mikrofonAcKapat() {
    if (!yerelAkis) {
        alert("Önce kamerayı ve mikrofonu açmalısın.");
        return;
    }

    const audioTrack = yerelAkis.getAudioTracks()[0];

    if (!audioTrack) {
        alert("Mikrofon bulunamadı.");
        return;
    }

    mikrofonKapali = !mikrofonKapali;
    audioTrack.enabled = !mikrofonKapali;

    const btn = document.getElementById("mikrofonBtn");

    if (mikrofonKapali) {
        btn.innerText = "Mikrofonu Aç";
        gorusmeDurumuYaz("Mikrofon kapatıldı");
    } else {
        btn.innerText = "Mikrofonu Kapat";
        gorusmeDurumuYaz("Mikrofon açıldı");
    }
}

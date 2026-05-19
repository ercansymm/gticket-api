# GBilet — Deployment Rehberi

Bu doküman GBilet (atabilet.com) projesinin canlıya alınma sürecini ve ortam yönetimini anlatır.

---

## Ortam Haritası (Branch ↔ Environment)

| Branch | Nerede | `ASPNETCORE_ENVIRONMENT` | BiletBank Endpoint | Veritabanı |
|--------|--------|--------------------------|--------------------|------------|
| `local` | Geliştirici makinesi | `Development` (launchSettings.json) | `https://apitest.biletbank.com/TrevooWS.svc` | Lokal PostgreSQL |
| `devtest` | Canlı öncesi staging sunucusu | `Staging` veya `Development` | `https://apitest.biletbank.com/TrevooWS.svc` | Staging PostgreSQL |
| `master` | **Canlı** (atabilet.com) | `Production` | `https://api.biletbank.com/TrevooWS.svc` | `/var/www/gbilet` PostgreSQL |

**Önemli kural:** Hangi branch'te olduğun değil, **deploy edildiğin sunucunun `ASPNETCORE_ENVIRONMENT`** değişkeni belirleyici. `appsettings.Production.json` yalnızca `Production` ortamında yüklenir; bu sayede canlı BiletBank URL'si sadece master sunucusunda aktif olur.

---

## ASP.NET Core Configuration Yükleme Sırası

ASP.NET Core dosyaları şu sırayla merge eder (sonraki, önceki üzerine yazılır):

1. `appsettings.json` (base — test BiletBank URL'si dahil tüm ortak konfig)
2. `appsettings.{ASPNETCORE_ENVIRONMENT}.json` (ortama özel override)
3. Environment variables (`BiletBank__Url` gibi — `:` yerine `__` kullanılır)

**Örnek:** Production sunucusunda `BiletBank` bölümü:
- `appsettings.json` → `Url: apitest.biletbank.com`, `Username: GTRAVELAPI`, ...
- `appsettings.Production.json` → `Url: api.biletbank.com` ✓ (override)
- Sonuç: URL canlı, geri kalan alanlar base'den

---

## Canlıya Geçiş Adımları (Production)

### 1) Lokalde yapılan değişiklikler

- `GBILET/GBILET.Api/appsettings.Production.json` → `BiletBank.Url` eklendi
- `GBILET/GBILET.Api/appsettings.Production.example.json` → template referansı

Lokal `appsettings.Production.json` `.gitignore`'da olduğu için commit edilmez; bu sadece sunucudaki dosya için referans.

### 2) Sunucuda yapılacaklar (SSH ile)

```bash
# Sunucuya bağlan
ssh kullanici@37.148.212.253

# 2.1) appsettings.Production.json yedek al
sudo cp /var/www/gbilet/appsettings.Production.json /var/www/gbilet/appsettings.Production.json.bak

# 2.2) Dosyayı düzenle
sudo nano /var/www/gbilet/appsettings.Production.json
```

Aşağıdaki içerikle güncelle (BiletBank bölümü ekleniyor):

```json
{
  "DbProvider": "PostgreSQL",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=gticketdb;Username=gbilet;Password=Gbilet2026!"
  },
  "BiletBank": {
    "Url": "https://api.biletbank.com/TrevooWS.svc"
  },
  "Jwt": {
    "PrivateKeyPath": "/var/www/gbilet/keys/jwt-private.pem",
    "PublicKeyPath": "/var/www/gbilet/keys/jwt-public.pem"
  }
}
```

### 3) systemd unit'inde environment kontrolü

```bash
sudo systemctl cat gbilet.service | grep ASPNETCORE_ENVIRONMENT
```

Aşağıdaki satırı görmen lazım:
```ini
Environment=ASPNETCORE_ENVIRONMENT=Production
```

Eğer eksikse veya `Development` yazıyorsa:
```bash
sudo systemctl edit gbilet.service --full
# [Service] bölümünün altına Environment satırını ekle/düzelt
sudo systemctl daemon-reload
```

### 4) Servisi yeniden başlat ve doğrula

```bash
# Servisi restart et
sudo systemctl restart gbilet.service

# Durum kontrolü
sudo systemctl status gbilet.service

# Logları izle (yeni terminalde)
sudo journalctl -u gbilet.service -n 200 -f
```

### 5) Endpoint doğrulaması

```bash
# Backend sağlığı
curl https://atabilet.com/api/health      # 200 OK

# Canlı BiletBank erişimi (sunucudan)
curl -v https://api.biletbank.com/TrevooWS.svc?singleWsdl
# 200 OK — IP whitelist sorunu varsa burada anlaşılır
```

Loglarda BiletBank istek URL'sinin `api.biletbank.com` (test değil!) olduğunu doğrula:
```bash
sudo journalctl -u gbilet.service -n 100 | grep -i "biletbank"
```

### 6) UI üzerinden canlı test

- atabilet.com'da düşük talep saatinde **güvenli rota** (ADB-SAW) ile arama yap
- Sonuçların geldiğini gözle

> **DİKKAT:** Canlı endpoint'te yapılan satın alma işlemleri **gerçek kart işlemi** üretir. Test PNR satın almak istiyorsan düşük ücretli bir uçuş seç ve iadeyi planla.

---

## Geri Dönüş (Rollback)

Sorun çıkarsa hızlıca test endpoint'e dön:

```bash
sudo cp /var/www/gbilet/appsettings.Production.json.bak /var/www/gbilet/appsettings.Production.json
sudo systemctl restart gbilet.service
```

Veya `BiletBank.Url` satırını manuel olarak `https://apitest.biletbank.com/TrevooWS.svc` yap, restart et.

---

## Olası Sorunlar

| Sorun | Belirti | Çözüm |
|-------|---------|-------|
| Sunucu IP whitelist'te değil | BiletBank Login `Forbidden` | Ceyda Öztürk'e (`api.support@biletbank.com`) `37.148.212.253` IP'sini canlı whitelist'e ekletmesini iste |
| Yanlış endpoint | İstek timeout | URL'de `apitest` → `api` doğru yapıldı mı? |
| Config yüklenmemiş | Loglarda hâlâ `apitest` | systemd unit `ASPNETCORE_ENVIRONMENT=Production` mı? `daemon-reload` + `restart` yapıldı mı? |
| Credentials reddedildi | `Login` SOAP fault | Ceyda'ya credential teyit ettir |
| Frontend cache eski session | BFF session cache hâlâ test endpoint'in sessionId'sini döndürüyor | Next.js process restart et (`sudo systemctl restart gticket-front` veya pm2 restart) |

---

## Konfigürasyon Dosyaları Özeti

| Dosya | Konum | Git | Amaç |
|-------|-------|-----|------|
| `appsettings.json` | Lokal + sunucu | İgnore | Base config (test BiletBank URL'si) |
| `appsettings.Development.json` | Lokal | İgnore | Lokal dev override |
| `appsettings.Production.json` | Sunucu (`/var/www/gbilet/`) | İgnore | Production override (BiletBank URL, DB, JWT path) |
| `appsettings.example.json` | Repo | Tracked | Base config template (yeni developer) |
| `appsettings.Production.example.json` | Repo | Tracked | Production override template |

Hassas bilgiler (BiletBank credentials, DB password, mail password) hiçbir zaman repo'ya commit edilmez — yalnızca sunucudaki `.json` dosyalarında ve `.example.json` template'lerde placeholder olarak yer alır.

---

## Frontend Tarafı

Frontend (`gticket-front` ve `gticket-admin`) zaten canlıda çalışıyor ve **backend BiletBank URL'sinden bağımsız**. Frontend BFF route'ları kendi backend'imize bağlanıyor; BiletBank URL'si yalnızca backend'in sorumluluğunda.

Bu yüzden BiletBank canlı geçişi sırasında frontend'i deploy etmen gerekmez. Sadece backend servisini restart etmek yeterli.

---

## Hızlı Komut Referansı

```bash
# Servis yönetimi
sudo systemctl status gbilet.service
sudo systemctl restart gbilet.service
sudo systemctl reload gbilet.service

# Log izleme
sudo journalctl -u gbilet.service -f
sudo journalctl -u gbilet.service -n 200

# Konfigürasyon kontrolü
sudo systemctl cat gbilet.service
sudo cat /var/www/gbilet/appsettings.Production.json

# Health check
curl https://atabilet.com/api/health
```

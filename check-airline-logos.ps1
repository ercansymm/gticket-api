# Airline Logo Kontrol Script'i
# wwwroot/images/airlines/ klasöründeki .png dosyalarını
# veritabanındaki Airlines tablosuyla karşılaştırır.
#
# Kullanım: GBILET/ klasöründen çalıştırın
#   cd GBILET
#   pwsh ./check-airline-logos.ps1

$wwwrootPath = Join-Path $PSScriptRoot "GBILET.Api" "wwwroot" "images" "airlines"

Write-Host "`n===== AIRLINE LOGO KONTROL =====" -ForegroundColor Cyan

# 1. Lokal .png dosyalarını listele
Write-Host "`n[1] Lokal logo dosyalari ($wwwrootPath):" -ForegroundColor Yellow

$localLogos = @()
if (Test-Path $wwwrootPath) {
    $localLogos = Get-ChildItem -Path $wwwrootPath -Filter "*.png" | ForEach-Object { $_.BaseName.ToUpperInvariant() }
    if ($localLogos.Count -eq 0) {
        Write-Host "  (bos - hic logo dosyasi yok)" -ForegroundColor Red
    } else {
        $localLogos | ForEach-Object { Write-Host "  $_" -ForegroundColor Green }
        Write-Host "  Toplam: $($localLogos.Count) logo" -ForegroundColor Green
    }
} else {
    Write-Host "  KLASOR YOK: $wwwrootPath" -ForegroundColor Red
}

# 2. DB'den airline code'lari cek (dotnet ef / raw SQL)
Write-Host "`n[2] Veritabanindaki airline code'lari:" -ForegroundColor Yellow

# appsettings.json'dan connection string oku
$appsettingsPath = Join-Path $PSScriptRoot "GBILET.Api" "appsettings.json"
if (-not (Test-Path $appsettingsPath)) {
    Write-Host "  appsettings.json bulunamadi: $appsettingsPath" -ForegroundColor Red
    exit 1
}

$config = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
$connStr = $config.ConnectionStrings.DefaultConnection

if (-not $connStr) {
    Write-Host "  ConnectionStrings.DefaultConnection bulunamadi!" -ForegroundColor Red
    exit 1
}

# psql ile Airlines tablosundan Code'lari cek
$query = "SELECT \"Code\" FROM \"Airlines\" WHERE \"IsActive\" = true ORDER BY \"Code\";"
$dbCodes = @()

try {
    $result = psql "$connStr" -t -A -c $query 2>&1
    if ($LASTEXITCODE -eq 0) {
        $dbCodes = $result | Where-Object { $_ -and $_.Trim() -ne "" } | ForEach-Object { $_.Trim().ToUpperInvariant() }
        $dbCodes | ForEach-Object { Write-Host "  $_" -ForegroundColor Green }
        Write-Host "  Toplam: $($dbCodes.Count) aktif airline" -ForegroundColor Green
    } else {
        Write-Host "  psql hatasi: $result" -ForegroundColor Red
        Write-Host "  (psql yuklu degil veya baglanti basarisiz)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "  psql calistirilamadi: $_" -ForegroundColor Red
    exit 1
}

# 3. Karsilastirma
Write-Host "`n[3] KARSILASTIRMA:" -ForegroundColor Cyan

$logosuOlan = $dbCodes | Where-Object { $_ -in $localLogos }
$logosuEksik = $dbCodes | Where-Object { $_ -notin $localLogos }
$fazlaLogo = $localLogos | Where-Object { $_ -notin $dbCodes }

Write-Host "`n  LOGOSU OLAN airline code'lar:" -ForegroundColor Green
if ($logosuOlan.Count -gt 0) {
    $logosuOlan | ForEach-Object { Write-Host "    [OK] $_" -ForegroundColor Green }
} else {
    Write-Host "    (yok)" -ForegroundColor DarkGray
}

Write-Host "`n  LOGOSU EKSIK airline code'lar:" -ForegroundColor Red
if ($logosuEksik.Count -gt 0) {
    $logosuEksik | ForEach-Object { Write-Host "    [EKSIK] $_" -ForegroundColor Red }
} else {
    Write-Host "    (hepsi mevcut)" -ForegroundColor Green
}

if ($fazlaLogo.Count -gt 0) {
    Write-Host "`n  DB'de OLMAYAN ama lokalde OLAN logolar:" -ForegroundColor Yellow
    $fazlaLogo | ForEach-Object { Write-Host "    [FAZLA] $_" -ForegroundColor Yellow }
}

Write-Host "`n===== OZET =====" -ForegroundColor Cyan
Write-Host "  DB'deki aktif airline: $($dbCodes.Count)"
Write-Host "  Lokaldeki logo:        $($localLogos.Count)"
Write-Host "  Logosu olan:           $($logosuOlan.Count)"
Write-Host "  Logosu eksik:          $($logosuEksik.Count)"
Write-Host ""

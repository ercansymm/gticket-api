-- =============================================================
-- Airport data fixes — 2026-05-21
-- Çalıştırma: önce devtest DB'de, doğrulandıktan sonra master DB'de.
-- Idempotent: tekrar çalıştırılırsa hata vermez (ON CONFLICT, WHERE clauses).
-- =============================================================

-- 1) Adana Şakirpaşa (ADA) — Şubat 2024'te kapandı, pasif yapıldı
UPDATE "Airports"
SET "IsActive"  = false,
    "IsPopular" = false,
    "SortOrder" = 999,
    "NameTr"    = 'Şakirpaşa Havalimanı (Kapalı)',
    "NameEn"    = 'Sakirpasa Airport (Closed)'
WHERE "IataCode" = 'ADA';

-- 2) Atatürk Havalimanı (ISL) — Nisan 2019'da ticari yolcu uçuşlarına kapatıldı
UPDATE "Airports"
SET "IsActive" = false,
    "NameTr"   = 'Atatürk Havalimanı (Ticari Uçuşlara Kapalı)',
    "NameEn"   = 'Ataturk Airport (Closed to Commercial Flights)',
    "CityTr"   = 'İstanbul',
    "CountryTr"= 'Türkiye'
WHERE "IataCode" = 'ISL';

-- 3) Çukurova Havalimanı (COV) — 2024'te açılan yeni havalimanı (Mersin/Tarsus)
INSERT INTO "Airports" (
  "IataCode", "IcaoCode", "NameTr", "NameEn",
  "CityTr", "CityEn", "CountryTr", "CountryEn", "CountryCode",
  "Timezone", "Latitude", "Longitude",
  "CityCode", "Type", "IsDomestic", "IsPopular", "IsActive", "SortOrder", "CreatedAt"
)
VALUES (
  'COV', '', 'Çukurova Havalimanı', 'Cukurova International Airport',
  'Mersin', 'Mersin', 'Türkiye', 'Turkey', 'TR',
  'Europe/Istanbul', 36.9358, 35.0589,
  'COV', 'airport', true, true, true, 1, NOW()
)
ON CONFLICT ("IataCode") DO NOTHING;

-- Doğrulama sorgusu (sonuçları gör):
-- SELECT "IataCode", "NameTr", "CityTr", "IsActive", "IsPopular"
-- FROM "Airports"
-- WHERE "IataCode" IN ('ADA','COV','ISL');

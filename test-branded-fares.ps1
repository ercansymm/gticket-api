# BiletBank AirSearch test — BrandedFares yapisini incele
$ErrorActionPreference = "Stop"

$url = "https://apitest.biletbank.com/TrevooWS.svc"

# 1. Login
$loginXml = '<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:tem="http://tempuri.org/" xmlns:trev1="http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Authentication.IO"><soap:Body><tem:Login><tem:request><trev1:Form><trev1:ChannelCode>2</trev1:ChannelCode><trev1:ClientIP></trev1:ClientIP><trev1:ClientName>GTRAVELAPI</trev1:ClientName><trev1:Password>Gtravel123!</trev1:Password><trev1:Username>GTRAVELAPI</trev1:Username></trev1:Form></tem:request></tem:Login></soap:Body></soap:Envelope>'

Write-Host "=== LOGIN ===" 
$loginResp = Invoke-WebRequest -Uri $url -Method POST -ContentType "text/xml; charset=utf-8" -Headers @{"SOAPAction"="http://tempuri.org/I_Authentication/Login"} -Body ([System.Text.Encoding]::UTF8.GetBytes($loginXml)) -UseBasicParsing
$loginText = $loginResp.Content

$sessionId = ([regex]::Match($loginText, '<[^>]*SessionId>([^<]+)<')).Groups[1].Value
$sessionToken = ([regex]::Match($loginText, '<[^>]*SessionToken>([^<]+)<')).Groups[1].Value
Write-Host "SessionId: $sessionId"
Write-Host "SessionToken length: $($sessionToken.Length)"

if ([string]::IsNullOrEmpty($sessionId)) {
    Write-Host "LOGIN BASARISIZ!"
    exit 1
}

# 2. AirSearch (yarin, IST->ADB)
$tomorrow = (Get-Date).AddDays(3).ToString("yyyy-MM-dd")
$searchXml = @"
<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:tem="http://tempuri.org/" xmlns:trev="http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Base" xmlns:trev1="http://schemas.datacontract.org/2004/07/Trevoo.WS.IO.Shopping" xmlns:trev2="http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Air">
<soap:Body>
   <tem:AirSearch>
      <tem:request>
         <trev:AuthenticationHeader>
            <trev:SessionId>$sessionId</trev:SessionId>
            <trev:SessionToken>$sessionToken</trev:SessionToken>
         </trev:AuthenticationHeader>
         <trev:ExtraParamList>
            <trev:ExtendedData>
               <trev:Name>BrandedFareVersion</trev:Name>
               <trev:Type>true</trev:Type>
               <trev:Value>v2</trev:Value>
            </trev:ExtendedData>
            <trev:ExtendedData>
               <trev:Name>SearchReason</trev:Name>
               <trev:Value>1</trev:Value>
            </trev:ExtendedData>
         </trev:ExtraParamList>
         <trev1:Form>
            <trev2:FlightType>OW</trev2:FlightType>
            <trev2:Options>
               <trev2:FlightClass>Y</trev2:FlightClass>
               <trev2:IfDirectFlightsOnly>false</trev2:IfDirectFlightsOnly>
               <trev2:IfRefundablesOnly>false</trev2:IfRefundablesOnly>
               <trev2:SearchTimeoutMilliseconds>30000</trev2:SearchTimeoutMilliseconds>
            </trev2:Options>
            <trev2:PaxItems>
               <trev2:T_AirSearch_PaxItem>
                  <trev2:PaxCode>ADT</trev2:PaxCode>
                  <trev2:PaxCount>1</trev2:PaxCount>
               </trev2:T_AirSearch_PaxItem>
            </trev2:PaxItems>
            <trev2:Segments>
                <trev2:T_AirSearch_SegmentItem>
                   <trev2:DepartureDay>${tomorrow}T00:00:00.000+00:00</trev2:DepartureDay>
                   <trev2:Destination>
                      <trev2:Code>ADB</trev2:Code>
                      <trev2:CountryCode>TR</trev2:CountryCode>
                      <trev2:IsCity>false</trev2:IsCity>
                      <trev2:Name/>
                   </trev2:Destination>
                   <trev2:Origin>
                      <trev2:Code>IST</trev2:Code>
                      <trev2:CountryCode>TR</trev2:CountryCode>
                      <trev2:IsCity>true</trev2:IsCity>
                      <trev2:Name/>
                   </trev2:Origin>
                   <trev2:SequenceNo>1</trev2:SequenceNo>
                </trev2:T_AirSearch_SegmentItem>
            </trev2:Segments>
         </trev1:Form>
      </tem:request>
   </tem:AirSearch>
</soap:Body>
</soap:Envelope>
"@

Write-Host "`n=== AIRSEARCH ==="
$searchResp = Invoke-WebRequest -Uri $url -Method POST -ContentType "text/xml; charset=utf-8" -Headers @{"SOAPAction"="http://tempuri.org/I_Shopping/AirSearch"} -Body ([System.Text.Encoding]::UTF8.GetBytes($searchXml)) -UseBasicParsing
$searchText = $searchResp.Content

# Tam response'u dosyaya yaz
$searchText | Out-File -FilePath "airsearch-response.xml" -Encoding UTF8
Write-Host "Tam response dosyaya yazildi: airsearch-response.xml ($(($searchText).Length) karakter)"

# XML parse et
[xml]$doc = $searchText

# Tum element isimlerini topla
$allElements = @()
$doc.SelectNodes("//*") | ForEach-Object { $allElements += $_.LocalName }
$uniqueElements = $allElements | Sort-Object -Unique

Write-Host "`n--- Tum benzersiz element isimleri ---"
$uniqueElements | ForEach-Object { Write-Host "  $_" }

# Brand iceren elementleri bul
Write-Host "`n--- 'Brand' iceren elementler ---"
$uniqueElements | Where-Object { $_ -match "brand|Brand" } | ForEach-Object { Write-Host "  $_" }

# T_FlightOption sayisi
$foNodes = $doc.GetElementsByTagName("T_FlightOption")
Write-Host "`nT_FlightOption sayisi: $($foNodes.Count)"

# T_RecommendationBox sayisi
$rbNodes = $doc.GetElementsByTagName("T_RecommendationBox")
Write-Host "T_RecommendationBox sayisi: $($rbNodes.Count)"

# Ilk T_FlightOption'da BrandedFares var mi?
if ($foNodes.Count -gt 0) {
    $firstFO = $foNodes[0]
    $brandedInFO = $firstFO.GetElementsByTagName("BrandedFares")
    Write-Host "`nIlk T_FlightOption'da BrandedFares: $($brandedInFO.Count) adet"
    if ($brandedInFO.Count -gt 0) {
        $bfiInFO = $brandedInFO[0].GetElementsByTagName("BrandedFareItem")
        $biInFO = $brandedInFO[0].GetElementsByTagName("BrandedItem")
        Write-Host "  BrandedFareItem: $($bfiInFO.Count)"
        Write-Host "  BrandedItem: $($biInFO.Count)"
    }
}

# Ilk T_RecommendationBox'da BrandedFares var mi?
if ($rbNodes.Count -gt 0) {
    $firstRB = $rbNodes[0]
    $brandedInRB = $firstRB.GetElementsByTagName("BrandedFares")
    Write-Host "`nIlk T_RecommendationBox'da BrandedFares: $($brandedInRB.Count) adet"
    if ($brandedInRB.Count -gt 0) {
        $bfiInRB = $brandedInRB[0].GetElementsByTagName("BrandedFareItem")
        $biInRB = $brandedInRB[0].GetElementsByTagName("BrandedItem")
        Write-Host "  BrandedFareItem: $($bfiInRB.Count)"
        Write-Host "  BrandedItem: $($biInRB.Count)"
    }
}

# ProductId'leri karsilastir
Write-Host "`n--- ProductId karsilastirmasi ---"
$foProductIds = @()
foreach ($fo in $foNodes) {
    $pid = $fo.GetElementsByTagName("ProductId")
    if ($pid.Count -gt 0) { $foProductIds += $pid[0].InnerText }
}
Write-Host "FlightOption ProductId sayisi: $($foProductIds.Count)"

$rbProductIds = @()
foreach ($rb in $rbNodes) {
    $pid = $rb.GetElementsByTagName("ProductId") 
    if ($pid.Count -gt 0) { $rbProductIds += $pid[0].InnerText }
}
Write-Host "RecommendationBox ProductId sayisi: $($rbProductIds.Count)"

# Kesisim
$intersection = $foProductIds | Where-Object { $rbProductIds -contains $_ }
Write-Host "Ortak ProductId sayisi: $($intersection.Count)"

if ($foProductIds.Count -gt 0) {
    Write-Host "`nIlk 3 FO ProductId: $($foProductIds[0..([Math]::Min(2,$foProductIds.Count-1))] -join ', ')"
}
if ($rbProductIds.Count -gt 0) {
    Write-Host "Ilk 3 RB ProductId: $($rbProductIds[0..([Math]::Min(2,$rbProductIds.Count-1))] -join ', ')"
}

Write-Host "`n=== BITTI ==="

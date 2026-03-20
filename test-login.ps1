$soapBody = '<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:tem="http://tempuri.org/" xmlns:trev1="http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Authentication.IO"><soap:Body><tem:Login><tem:request><trev1:Form><trev1:ChannelCode>2</trev1:ChannelCode><trev1:ClientIP></trev1:ClientIP><trev1:ClientName>GTRAVELAPI</trev1:ClientName><trev1:Password>Gtravel123!</trev1:Password><trev1:Username>GTRAVELAPI</trev1:Username></trev1:Form></tem:request></tem:Login></soap:Body></soap:Envelope>'

# SSL sertifika dogrulamasini atla (localhost uzerinden socat proxy)
if (-not ([System.Management.Automation.PSTypeName]'TrustAll').Type) {
    Add-Type @"
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
public class TrustAll {
    public static void Enable() {
        ServicePointManager.ServerCertificateValidationCallback = (s, cert, chain, errs) => true;
    }
}
"@
}
[TrustAll]::Enable()

$soapBody = @"
<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"
xmlns:tem="http://tempuri.org/"
xmlns:trev1="http://schemas.datacontract.org/2004/07/Trevoo.WS.Entities.Authentication.IO">
<soap:Body>
<tem:Login>
<tem:request>
<trev1:Form>
<trev1:ChannelCode>2</trev1:ChannelCode>
<trev1:ClientIP></trev1:ClientIP>
<trev1:ClientName>GTRAVELAPI</trev1:ClientName>
<trev1:Password>Gtravel123!</trev1:Password>
<trev1:Username>GTRAVELAPI</trev1:Username>
</trev1:Form>
</tem:request>
</tem:Login>
</soap:Body>
</soap:Envelope>
"@

try {
    $r = Invoke-WebRequest -Uri "https://localhost:8000/TrevooWS.svc" -Method POST -Body $soapBody -ContentType "text/xml; charset=utf-8" -Headers @{Host="apitest.biletbank.com"; SOAPAction="http://tempuri.org/I_Authentication/Login"} -TimeoutSec 30 -SkipCertificateCheck -ErrorAction Stop
    Write-Host "Status: $($r.StatusCode)"
    Write-Host $r.Content
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        Write-Host "Status: $($_.Exception.Response.StatusCode)"
        $sr = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        Write-Host $sr.ReadToEnd()
    }
}

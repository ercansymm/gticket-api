namespace GBILET.Core.Helpers;

public static class FlightMappings
{
    // Veritabanından yüklenen havayolu verileri (fallback olarak hard-coded dictionary kalıyor)
    private static Dictionary<string, string> _dbAirlines = new(StringComparer.OrdinalIgnoreCase);
    private static bool _airlinesLoadedFromDb;

    /// <summary>
    /// Uygulama başlangıcında veritabanından yüklenen havayolu verilerini set eder.
    /// </summary>
    public static void LoadAirlinesFromDatabase(Dictionary<string, string> airlines)
    {
        _dbAirlines = new Dictionary<string, string>(airlines, StringComparer.OrdinalIgnoreCase);
        _airlinesLoadedFromDb = true;
    }

    public static readonly Dictionary<string, string> Airlines = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VF"] = "AJet",
        ["TK"] = "Türk Hava Yolları",
        ["PC"] = "Pegasus",
        ["XQ"] = "SunExpress",
        ["AJ"] = "AJet",
        ["QF"] = "Qantas",
        ["LH"] = "Lufthansa",
        ["BA"] = "British Airways",
        ["AF"] = "Air France",
        ["KL"] = "KLM",
        ["EK"] = "Emirates",
        ["QR"] = "Qatar Airways",
        ["SV"] = "Saudia",
        ["MS"] = "EgyptAir",
        ["RJ"] = "Royal Jordanian",
        ["UA"] = "United Airlines",
        ["AA"] = "American Airlines",
        ["DL"] = "Delta Air Lines",
        ["OS"] = "Austrian Airlines",
        ["LX"] = "Swiss International Air Lines",
        ["SN"] = "Brussels Airlines",
        ["AZ"] = "ITA Airways",
        ["SK"] = "SAS Scandinavian Airlines",
        ["FI"] = "Icelandair",
        ["W6"] = "Wizz Air",
        ["FR"] = "Ryanair",
        ["U2"] = "easyJet",
        ["6E"] = "IndiGo",
        ["SQ"] = "Singapore Airlines",
        ["CX"] = "Cathay Pacific",
        ["NH"] = "All Nippon Airways",
        ["JL"] = "Japan Airlines",
        ["OZ"] = "Asiana Airlines",
        ["KE"] = "Korean Air",
        ["CI"] = "China Airlines",
        ["MH"] = "Malaysia Airlines",
        ["TG"] = "Thai Airways",
        ["GA"] = "Garuda Indonesia",
        ["ET"] = "Ethiopian Airlines",
        ["SA"] = "South African Airways",
        ["AC"] = "Air Canada",
        ["WY"] = "Oman Air"
    };

    public static readonly Dictionary<string, string> Airports = new(StringComparer.OrdinalIgnoreCase)
    {
        // Türkiye
        ["SAW"] = "İstanbul Sabiha Gökçen",
        ["IST"] = "İstanbul Havalimanı",
        ["ESB"] = "Ankara Esenboğa",
        ["ADB"] = "İzmir Adnan Menderes",
        ["AYT"] = "Antalya",
        ["TZX"] = "Trabzon",
        ["BJV"] = "Bodrum Milas",
        ["DLM"] = "Dalaman",
        ["GZT"] = "Gaziantep",
        ["VAN"] = "Van Ferit Melen",
        ["ERZ"] = "Erzurum",
        ["EZS"] = "Elazığ",
        ["DIY"] = "Diyarbakır",
        ["SZF"] = "Samsun Çarşamba",
        ["KYA"] = "Konya",
        ["ASR"] = "Kayseri Erkilet",
        ["HTY"] = "Hatay",
        ["MLX"] = "Malatya Erhaç",
        ["SFQ"] = "Şanlıurfa GAP",
        ["MZH"] = "Amasya Merzifon",
        ["SIC"] = "Sinop",
        ["KCM"] = "Kahramanmaraş",
        ["OGU"] = "Ordu-Giresun",
        ["CKZ"] = "Çanakkale",
        ["BZC"] = "Balıkesir Merkez",
        ["DNZ"] = "Denizli Çardak",
        ["ISE"] = "Isparta Süleyman Demirel",
        ["MQM"] = "Mardin",
        ["NKT"] = "Şırnak Şerafettin Elçi",
        ["SXZ"] = "Siirt",
        ["YKO"] = "Yozgat",
        ["HRK"] = "Hakkari Yüksekova Selahaddin Eyyubi",
        ["BAL"] = "Batman",
        ["IGD"] = "Iğdır Şehit Bülent Aydın",
        ["KSY"] = "Kars Harakani",
        ["MSR"] = "Muş",
        ["BGG"] = "Bingöl",
        ["TJK"] = "Tokat",
        ["ZON"] = "Zonguldak Çaycuma",
        ["AOE"] = "Eskişehir Hasan Polatkan",
        ["USQ"] = "Uşak",
        ["AFY"] = "Afyon",
        ["EDO"] = "Balıkesir Koca Seyit",
        ["TEQ"] = "Tekirdağ Çorlu",
        ["BDM"] = "Bandırma",
        ["KZR"] = "Zafer (Kütahya-Afyon-Uşak)",
        ["ADA"] = "Adana Şakirpaşa",
        ["COV"] = "Mersin Çukurova",
        ["NAV"] = "Nevşehir Kapadokya",
        ["GZP"] = "Gazipaşa-Alanya",
        ["RZV"] = "Rize-Artvin",

        // Popüler yurtdışı
        ["LHR"] = "Londra Heathrow",
        ["CDG"] = "Paris Charles de Gaulle",
        ["FRA"] = "Frankfurt",
        ["AMS"] = "Amsterdam Schiphol",
        ["FCO"] = "Roma Fiumicino",
        ["BCN"] = "Barcelona El Prat",
        ["MAD"] = "Madrid Barajas",
        ["MUC"] = "Münih",
        ["ZRH"] = "Zürih",
        ["VIE"] = "Viyana",
        ["BRU"] = "Brüksel",
        ["CPH"] = "Kopenhag",
        ["OSL"] = "Oslo",
        ["ARN"] = "Stockholm Arlanda",
        ["HEL"] = "Helsinki",
        ["ATH"] = "Atina",
        ["WAW"] = "Varşova",
        ["PRG"] = "Prag",
        ["BUD"] = "Budapeşte",
        ["OTP"] = "Bükreş",
        ["SOF"] = "Sofya",
        ["BEG"] = "Belgrad",
        ["ZAG"] = "Zagreb",
        ["TIA"] = "Tiran",
        ["SKP"] = "Üsküp",
        ["SJJ"] = "Saraybosna",
        ["DXB"] = "Dubai",
        ["DOH"] = "Doha",
        ["JED"] = "Cidde",
        ["RUH"] = "Riyad",
        ["MED"] = "Medine",
        ["KWI"] = "Kuveyt",
        ["BAH"] = "Bahreyn",
        ["AUH"] = "Abu Dhabi",
        ["TLV"] = "Tel Aviv Ben Gurion",
        ["AMM"] = "Amman",
        ["BEY"] = "Beyrut",
        ["CAI"] = "Kahire",
        ["CMN"] = "Kazablanka",
        ["TUN"] = "Tunus",
        ["JFK"] = "New York JFK",
        ["LAX"] = "Los Angeles",
        ["ORD"] = "Chicago O'Hare",
        ["MIA"] = "Miami",
        ["SFO"] = "San Francisco",
        ["IAD"] = "Washington Dulles",
        ["YYZ"] = "Toronto Pearson",
        ["PEK"] = "Pekin",
        ["PVG"] = "Şanghay Pudong",
        ["HND"] = "Tokyo Haneda",
        ["NRT"] = "Tokyo Narita",
        ["ICN"] = "Seul Incheon",
        ["SIN"] = "Singapur Changi",
        ["BKK"] = "Bangkok Suvarnabhumi",
        ["KUL"] = "Kuala Lumpur",
        ["DEL"] = "Delhi",
        ["BOM"] = "Mumbai",
        ["SYD"] = "Sidney",
        ["MEL"] = "Melbourne",
        ["GIG"] = "Rio de Janeiro",
        ["GRU"] = "São Paulo",
        ["ADD"] = "Addis Ababa",
        ["JNB"] = "Johannesburg",
        ["NBO"] = "Nairobi",
        ["LGW"] = "Londra Gatwick",
        ["STN"] = "Londra Stansted",
        ["ORY"] = "Paris Orly",
        ["SVO"] = "Moskova Sheremetyevo",
        ["DME"] = "Moskova Domodedovo",
        ["LED"] = "St. Petersburg",
        ["DUS"] = "Düsseldorf",
        ["HAM"] = "Hamburg",
        ["TXL"] = "Berlin Tegel",
        ["BER"] = "Berlin Brandenburg",
        ["MXP"] = "Milano Malpensa",
        ["LIN"] = "Milano Linate",
        ["NAP"] = "Napoli",
        ["VCE"] = "Venedik Marco Polo",
        ["LIS"] = "Lizbon",
        ["DUB"] = "Dublin",
        ["EDI"] = "Edinburgh",
        ["MAN"] = "Manchester",
        ["BHX"] = "Birmingham"
    };

    public static readonly Dictionary<string, string> FareTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ECO"] = "Ekonomi",
        ["BUS"] = "Business",
        ["PEF"] = "Premium Ekonomi",
        ["FIR"] = "First Class",
        ["Economy"] = "Ekonomi",
        ["Business"] = "Business",
        ["First"] = "First Class",
        ["PremiumEconomy"] = "Premium Ekonomi"
    };

    public static readonly Dictionary<string, string> BookingClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        // Full flex
        ["Y"] = "Ekonomi Full Flex",
        // Esnek
        ["M"] = "Ekonomi Esnek",
        ["S"] = "Ekonomi Esnek",
        // İndirimli
        ["B"] = "Ekonomi İndirimli",
        ["H"] = "Ekonomi İndirimli",
        ["K"] = "Ekonomi İndirimli",
        ["L"] = "Ekonomi İndirimli",
        ["Q"] = "Ekonomi İndirimli",
        ["T"] = "Ekonomi İndirimli",
        ["E"] = "Ekonomi İndirimli",
        ["N"] = "Ekonomi İndirimli",
        ["V"] = "Ekonomi İndirimli",
        ["W"] = "Ekonomi İndirimli",
        ["G"] = "Ekonomi İndirimli",
        ["O"] = "Ekonomi İndirimli",
        ["X"] = "Ekonomi İndirimli",
        // Business
        ["C"] = "Business",
        ["D"] = "Business",
        ["J"] = "Business",
        ["Z"] = "Business",
        ["I"] = "Business İndirimli",
        // First
        ["F"] = "First Class",
        ["A"] = "First Class",
        ["P"] = "First Class İndirimli",
        // Premium Economy
        ["R"] = "Premium Ekonomi"
    };

    public static string GetAirlineName(string? code)
    {
        if (string.IsNullOrEmpty(code)) return "";

        // Önce veritabanından yüklenen veriye bak
        if (_airlinesLoadedFromDb && _dbAirlines.TryGetValue(code, out var dbName))
            return dbName;

        // Fallback: hard-coded dictionary
        return Airlines.TryGetValue(code, out var name) ? name : code;
    }

    public static string GetAirportName(string? code)
    {
        if (string.IsNullOrEmpty(code)) return "";
        return Airports.TryGetValue(code, out var name) ? name : code;
    }

    public static string GetFareTypeName(string? code)
    {
        if (string.IsNullOrEmpty(code)) return "";
        return FareTypes.TryGetValue(code, out var name) ? name : code;
    }

    public static string GetBookingClassName(string? code)
    {
        if (string.IsNullOrEmpty(code)) return "";
        return BookingClasses.TryGetValue(code, out var name) ? name : code;
    }
}

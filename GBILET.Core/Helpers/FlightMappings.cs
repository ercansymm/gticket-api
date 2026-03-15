namespace GBILET.Core.Helpers;

public static class FlightMappings
{
    public static readonly Dictionary<string, string> Airlines = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VF"] = "AJet",
        ["TK"] = "Türk Hava Yollarý",
        ["PC"] = "Pegasus",
        ["XQ"] = "SunExpress",
        ["AJ"] = "AnadoluJet",
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
        ["SAW"] = "Ýstanbul Sabiha Gökçen",
        ["IST"] = "Ýstanbul Havalimaný",
        ["ESB"] = "Ankara Esenboða",
        ["ADB"] = "Ýzmir Adnan Menderes",
        ["AYT"] = "Antalya",
        ["TZX"] = "Trabzon",
        ["BJV"] = "Bodrum Milas",
        ["DLM"] = "Dalaman",
        ["GZT"] = "Gaziantep",
        ["VAN"] = "Van Ferit Melen",
        ["ERZ"] = "Erzurum",
        ["EZS"] = "Elazýð",
        ["DIY"] = "Diyarbakýr",
        ["SZF"] = "Samsun Çarþamba",
        ["KYA"] = "Konya",
        ["ASR"] = "Kayseri Erkilet",
        ["HTY"] = "Hatay",
        ["MLX"] = "Malatya Erhaç",
        ["GNY"] = "Þanlýurfa GAP",
        ["MZH"] = "Amasya Merzifon",
        ["NOP"] = "Sinop",
        ["KCM"] = "Kahramanmaraþ",
        ["OGU"] = "Ordu-Giresun",
        ["CKZ"] = "Çanakkale",
        ["BZC"] = "Balýkesir Koca Seyit",
        ["DNZ"] = "Denizli Çardak",
        ["ISE"] = "Isparta Süleyman Demirel",
        ["MQM"] = "Mardin",
        ["NKT"] = "Þýrnak Þerafettin Elçi",
        ["SXZ"] = "Siirt",
        ["YKO"] = "Hakkari Yüksekova Selahaddin Eyyubi",
        ["BAL"] = "Batman",
        ["IGD"] = "Iðdýr Þehit Bülent Aydýn",
        ["KSY"] = "Kars Harakani",
        ["MSR"] = "Muþ",
        ["BGG"] = "Bingöl",
        ["TJK"] = "Tokat",
        ["ONQ"] = "Zonguldak Çaycuma",
        ["AOE"] = "Eskiþehir Hasan Polatkan",
        ["USQ"] = "Uþak",
        ["AFY"] = "Afyon",
        ["EDO"] = "Balýkesir Merkez",
        ["TEQ"] = "Tekirdað Çorlu",
        ["BDM"] = "Bandýrma",
        ["KZR"] = "Zafer (Kütahya-Afyon-Uþak)",
        ["ADA"] = "Adana Þakirpaþa",
        ["NAV"] = "Nevþehir Kapadokya",
        ["GZP"] = "Rize-Artvin",

        // Popüler yurtdýþý
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
        ["WAW"] = "Varþova",
        ["PRG"] = "Prag",
        ["BUD"] = "Budapeþte",
        ["OTP"] = "Bükreþ",
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
        ["PVG"] = "Þanghay Pudong",
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
        // Ýndirimli
        ["B"] = "Ekonomi Ýndirimli",
        ["H"] = "Ekonomi Ýndirimli",
        ["K"] = "Ekonomi Ýndirimli",
        ["L"] = "Ekonomi Ýndirimli",
        ["Q"] = "Ekonomi Ýndirimli",
        ["T"] = "Ekonomi Ýndirimli",
        ["E"] = "Ekonomi Ýndirimli",
        ["N"] = "Ekonomi Ýndirimli",
        ["V"] = "Ekonomi Ýndirimli",
        ["W"] = "Ekonomi Ýndirimli",
        ["G"] = "Ekonomi Ýndirimli",
        ["O"] = "Ekonomi Ýndirimli",
        ["X"] = "Ekonomi Ýndirimli",
        // Business
        ["C"] = "Business",
        ["D"] = "Business",
        ["J"] = "Business",
        ["Z"] = "Business",
        ["I"] = "Business Ýndirimli",
        // First
        ["F"] = "First Class",
        ["A"] = "First Class",
        ["P"] = "First Class Ýndirimli",
        // Premium Economy
        ["R"] = "Premium Ekonomi"
    };

    public static string GetAirlineName(string? code)
    {
        if (string.IsNullOrEmpty(code)) return "";
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

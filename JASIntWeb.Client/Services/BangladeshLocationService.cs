namespace JASIntWeb.Client.Services;

public static class BangladeshLocationService
{
    public static readonly string[] Divisions =
    [
        "Dhaka",
        "Chattogram",
        "Khulna",
        "Rajshahi",
        "Barishal",
        "Sylhet",
        "Rangpur",
        "Mymensingh"
    ];

    public static readonly Dictionary<string, string[]> DistrictsByDivision = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dhaka"] = ["Dhaka", "Gazipur", "Narayanganj", "Tangail", "Faridpur", "Manikganj", "Munshiganj", "Narsingdi", "Kishoreganj", "Gopalganj", "Madaripur", "Rajbari", "Shariatpur"],
        ["Chattogram"] = ["Chattogram", "Cox's Bazar", "Cumilla", "Feni", "Brahmanbaria", "Noakhali", "Chandpur", "Lakshmipur", "Khagrachhari", "Rangamati", "Bandarban"],
        ["Khulna"] = ["Khulna", "Jashore", "Satkhira", "Bagerhat", "Kushtia", "Jhenaidah", "Chuadanga", "Meherpur", "Magura", "Narail"],
        ["Rajshahi"] = ["Rajshahi", "Bogura", "Pabna", "Sirajganj", "Naogaon", "Natore", "Chapai Nawabganj", "Joypurhat"],
        ["Barishal"] = ["Barishal", "Patuakhali", "Bhola", "Pirojpur", "Jhalokathi", "Barguna"],
        ["Sylhet"] = ["Sylhet", "Moulvibazar", "Habiganj", "Sunamganj"],
        ["Rangpur"] = ["Rangpur", "Dinajpur", "Kurigram", "Gaibandha", "Nilphamari", "Lalmonirhat", "Thakurgaon", "Panchagarh"],
        ["Mymensingh"] = ["Mymensingh", "Jamalpur", "Netrokona", "Sherpur"]
    };

    public static readonly Dictionary<string, string[]> ThanasByDistrict = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Khulna"] = ["Khulna Sadar", "Sonadanga", "Khalishpur", "Daulatpur", "Khan Jahan Ali", "Dighalia", "Phultala", "Terokhada", "Dumuria", "Batiaghata", "Dacope", "Paikgachha", "Koyra", "Rupsha"],
        ["Jashore"] = ["Jashore Sadar", "Jhikargachha", "Sharsha", "Manirampur", "Abhaynagar", "Keshabpur", "Chaugachha", "Bagherpara"],
        ["Satkhira"] = ["Satkhira Sadar", "Kalaroa", "Tala", "Debhata", "Kaliganj", "Assasuni", "Shyamnagar"],
        ["Bagerhat"] = ["Bagerhat Sadar", "Mongla", "Morrelganj", "Kachua", "Fakirhat", "Rampal", "Chitalmari", "Mollahat", "Sarankhola"],
        ["Kushtia"] = ["Kushtia Sadar", "Kumarkhali", "Khoksa", "Mirpur", "Bheramara", "Daulatpur"],
        ["Jhenaidah"] = ["Jhenaidah Sadar", "Kaliganj", "Kotchandpur", "Maheshpur", "Shailkupa", "Harinakundu"],
        ["Chuadanga"] = ["Chuadanga Sadar", "Alamdanga", "Damurhuda", "Jibannagar"],
        ["Meherpur"] = ["Meherpur Sadar", "Gangni", "Mujibnagar"],
        ["Magura"] = ["Magura Sadar", "Sreepur", "Mohammadpur", "Shalikha"],
        ["Narail"] = ["Narail Sadar", "Lohagara", "Kalia"],

        ["Dhaka"] = ["Dhanmondi", "Gulshan", "Banani", "Uttara", "Mirpur", "Mohammadpur", "Badda", "Motijheel", "Tejgaon", "Khilgaon", "Pallabi", "Rampura", "Shahbagh", "Jatrabari", "Lalbagh", "Keraniganj", "Savar", "Dhamrai"],
        ["Gazipur"] = ["Gazipur Sadar", "Tongi", "Kaliakair", "Kapasia", "Sreepur", "Kaliganj"],
        ["Narayanganj"] = ["Narayanganj Sadar", "Bandar", "Fatullah", "Siddhirganj", "Sonargaon", "Rupganj", "Araihazar"],

        ["Chattogram"] = ["Panchlaish", "Kotwali", "Khulshi", "Halishahar", "Agrabad", "Bakalia", "Bayazid", "Chandgaon", "Double Mooring", "Pahartali", "Patenga", "Hathazari", "Sitakunda", "Raozan", "Fatikchhari", "Patiya"],
        ["Rajshahi"] = ["Boalia", "Rajpara", "Motihar", "Shah Mokhdum", "Paba", "Godagari", "Tanore", "Bagmara", "Durgapur", "Charghat", "Puthia", "Bagha"],
        ["Barishal"] = ["Barishal Sadar", "Bakerganj", "Babuganj", "Wazirpur", "Banaripara", "Gournadi", "Agailjhara", "Mehendiganj", "Muladi", "Hizla"],
        ["Sylhet"] = ["Sylhet Sadar", "Kotwali", "South Surma", "Beanibazar", "Golapganj", "Companiganj", "Fenchuganj", "Gowainghat", "Jaintiapur", "Kanaighat", "Zakiganj"],
        ["Rangpur"] = ["Rangpur Sadar", "Kotwali", "Badarganj", "Mithapukur", "Pirgachha", "Pirganj", "Kaunia", "Gangachhara", "Taraganj"],
        ["Mymensingh"] = ["Mymensingh Sadar", "Trishal", "Muktagachha", "Bhaluka", "Fulbaria", "Gaffargaon", "Gauripur", "Ishwarganj", "Haluaghat", "Dhobaura", "Nandail", "Tara Khanda"]
    };

    public static IReadOnlyList<string> GetDistricts(string? division)
    {
        if (string.IsNullOrWhiteSpace(division) || !DistrictsByDivision.TryGetValue(division, out var districts))
        {
            return [];
        }
        return districts;
    }

    public static IReadOnlyList<string> GetThanas(string? district)
    {
        if (string.IsNullOrWhiteSpace(district))
        {
            return [];
        }
        if (ThanasByDistrict.TryGetValue(district, out var thanas))
        {
            return thanas;
        }
        return [$"{district} Sadar", $"{district} City", "Other / Rural Area"];
    }
}

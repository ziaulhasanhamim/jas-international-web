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
        ["Dhaka"] =
        [
            "Dhaka", "Gazipur", "Narayanganj", "Tangail", "Faridpur", "Manikganj", "Munshiganj",
            "Narsingdi", "Kishoreganj", "Gopalganj", "Madaripur", "Rajbari", "Shariatpur"
        ],
        ["Chattogram"] =
        [
            "Chattogram", "Cox's Bazar", "Cumilla", "Feni", "Brahmanbaria", "Noakhali", "Chandpur",
            "Lakshmipur", "Khagrachhari", "Rangamati", "Bandarban"
        ],
        ["Khulna"] =
        [
            "Khulna", "Jashore", "Satkhira", "Bagerhat", "Kushtia", "Jhenaidah", "Chuadanga",
            "Meherpur", "Magura", "Narail"
        ],
        ["Rajshahi"] =
        [
            "Rajshahi", "Bogura", "Pabna", "Sirajganj", "Naogaon", "Natore", "Chapainawabganj", "Joypurhat"
        ],
        ["Barishal"] =
        [
            "Barishal", "Patuakhali", "Bhola", "Pirojpur", "Jhalakathi", "Barguna"
        ],
        ["Sylhet"] =
        [
            "Sylhet", "Moulvibazar", "Habiganj", "Sunamganj"
        ],
        ["Rangpur"] =
        [
            "Rangpur", "Dinajpur", "Kurigram", "Gaibandha", "Nilphamari", "Lalmonirhat", "Thakurgaon", "Panchagarh"
        ],
        ["Mymensingh"] =
        [
            "Mymensingh", "Jamalpur", "Netrokona", "Sherpur"
        ]
    };

    public static readonly Dictionary<string, string[]> ThanasByDistrict = new(StringComparer.OrdinalIgnoreCase)
    {
        // ------------------ DHAKA DIVISION ------------------
        ["Dhaka"] =
        [
            "Dhanmondi", "Gulshan", "Banani", "Uttara", "Mirpur", "Mohammadpur", "Badda", "Motijheel",
            "Tejgaon", "Khilgaon", "Pallabi", "Rampura", "Shahbagh", "Jatrabari", "Lalbagh", "New Market",
            "Cantonment", "Kafrul", "Darus Salam", "Hazaribagh", "Kamrangirchar", "Kotwali", "Sutrapur",
            "Wari", "Gendaria", "Demra", "Kadamtali", "Shyampur", "Biman Bandar", "Khilkhet", "Bhatara",
            "Vatara", "Sabujbagh", "Mugda", "Paltan", "Ramna", "Sher-e-Bangla Nagar", "Adabor", "Turag",
            "Uttarkhan", "Dakshinkhan", "Savar", "Dhamrai", "Keraniganj", "Nawabganj", "Dohar"
        ],
        ["Gazipur"] =
        [
            "Gazipur Sadar", "Joydebpur", "Tongi", "Kaliakair", "Kapasia", "Sreepur", "Kaliganj"
        ],
        ["Narayanganj"] =
        [
            "Narayanganj Sadar", "Fatullah", "Siddhirganj", "Bandar", "Sonargaon", "Rupganj", "Araihazar"
        ],
        ["Tangail"] =
        [
            "Tangail Sadar", "Gopalpur", "Basail", "Bhuapur", "Delduar", "Ghatail", "Kalihati", "Madhupur",
            "Mirzapur", "Nagarpur", "Sakhipur", "Dhanbari"
        ],
        ["Faridpur"] =
        [
            "Faridpur Sadar", "Alfadanga", "Bhanga", "Boalmari", "Charbhadrasan", "Madhukhali", "Nagarkanda",
            "Sadarpur", "Saltha"
        ],
        ["Manikganj"] =
        [
            "Manikganj Sadar", "Singair", "Shibalaya", "Saturia", "Harirampur", "Ghior", "Daulatpur"
        ],
        ["Munshiganj"] =
        [
            "Munshiganj Sadar", "Tongibari", "Sreenagar", "Lauhajang", "Gajaria", "Sirajdikhan"
        ],
        ["Narsingdi"] =
        [
            "Narsingdi Sadar", "Belabo", "Monohardi", "Palash", "Raipura", "Shibpur"
        ],
        ["Kishoreganj"] =
        [
            "Kishoreganj Sadar", "Bajitpur", "Bhairab", "Hossainpur", "Itna", "Karimganj", "Katiadi",
            "Kuliarchar", "Mithamain", "Nikli", "Pakundia", "Tarail", "Ashtagram"
        ],
        ["Gopalganj"] =
        [
            "Gopalganj Sadar", "Kashiani", "Kotalipara", "Muksudpur", "Tungipara"
        ],
        ["Madaripur"] =
        [
            "Madaripur Sadar", "Kalkini", "Rajoir", "Shibchar", "Dasar"
        ],
        ["Rajbari"] =
        [
            "Rajbari Sadar", "Goalanda", "Pangsha", "Baliakandi", "Kalukhali"
        ],
        ["Shariatpur"] =
        [
            "Shariatpur Sadar", "Damudya", "Naria", "Jajira", "Bhedarganj", "Gosairhat"
        ],

        // ------------------ CHATTOGRAM DIVISION ------------------
        ["Chattogram"] =
        [
            "Agrabad", "Kotwali", "Panchlaish", "Nasirabad", "Halishahar", "Khulshi", "Chandgaon", "Bayazid",
            "Bakalia", "Double Mooring", "Pahartali", "Patenga", "Karnafuli", "Hathazari", "Sitakunda",
            "Mirsharai", "Raozan", "Rangunia", "Fatikchhari", "Patiya", "Boalkhali", "Anwara", "Chandanaish",
            "Satkania", "Lohagara", "Banshkhali", "Sandwip"
        ],
        ["Cox's Bazar"] =
        [
            "Cox's Bazar Sadar", "Chakaria", "Teknaf", "Ukhiya", "Ramu", "Maheshkhali", "Kutubdia", "Pekua", "Eidgaon"
        ],
        ["Cumilla"] =
        [
            "Cumilla Adarsha Sadar", "Cumilla Sadar Dakshin", "Barura", "Chandina", "Chauddagram", "Daudkandi",
            "Debidwar", "Homna", "Laksam", "Muradnagar", "Nangalkot", "Burichang", "Brahmanpara", "Meghna",
            "Monohargonj", "Titas", "Lalmai"
        ],
        ["Feni"] =
        [
            "Feni Sadar", "Daganbhuiyan", "Chhagalnaiya", "Sonagazi", "Parshuram", "Fulgazi"
        ],
        ["Brahmanbaria"] =
        [
            "Brahmanbaria Sadar", "Ashuganj", "Nasirnagar", "Nabinagar", "Sarail", "Kasba", "Akhaura",
            "Bancharampur", "Bijoynagar"
        ],
        ["Noakhali"] =
        [
            "Noakhali Sadar (Sudharam)", "Begumganj", "Chatkhil", "Companiganj", "Hatiya", "Senbagh",
            "Subarnachar", "Kabirhat", "Sonaimuri"
        ],
        ["Chandpur"] =
        [
            "Chandpur Sadar", "Faridganj", "Haimchar", "Hajiganj", "Kachua", "Matlab Dakshin", "Matlab Uttar", "Shahrasti"
        ],
        ["Lakshmipur"] =
        [
            "Lakshmipur Sadar", "Raipur", "Ramganj", "Ramgati", "Kamalnagar"
        ],
        ["Khagrachhari"] =
        [
            "Khagrachhari Sadar", "Dighinala", "Panchhari", "Mahalchhari", "Matiranga", "Manikchhari", "Ramgarh", "Guimara"
        ],
        ["Rangamati"] =
        [
            "Rangamati Sadar", "Kaptai", "Kawkhali", "Baghaichhari", "Barkal", "Langadu", "Rajasthali",
            "Belaichhari", "Juraichhari", "Naniarchar"
        ],
        ["Bandarban"] =
        [
            "Bandarban Sadar", "Ruma", "Thanchi", "Rowangchhari", "Lama", "Alikadam", "Naikhongchhari"
        ],

        // ------------------ KHULNA DIVISION ------------------
        ["Khulna"] =
        [
            "Khulna Sadar", "Sonadanga", "Khalishpur", "Daulatpur", "Khan Jahan Ali", "Harintana", "Aranghata",
            "Dighalia", "Phultala", "Terokhada", "Dumuria", "Batiaghata", "Dacope", "Paikgachha", "Koyra", "Rupsha"
        ],
        ["Jashore"] =
        [
            "Jashore Sadar", "Jhikargachha", "Sharsha", "Manirampur", "Abhaynagar", "Keshabpur", "Chaugachha",
            "Bagherpara", "Benapole"
        ],
        ["Satkhira"] =
        [
            "Satkhira Sadar", "Kalaroa", "Tala", "Debhata", "Kaliganj", "Assasuni", "Shyamnagar"
        ],
        ["Bagerhat"] =
        [
            "Bagerhat Sadar", "Mongla", "Morrelganj", "Kachua", "Fakirhat", "Rampal", "Chitalmari", "Mollahat", "Sarankhola"
        ],
        ["Kushtia"] =
        [
            "Kushtia Sadar", "Kumarkhali", "Khoksa", "Mirpur", "Bheramara", "Daulatpur"
        ],
        ["Jhenaidah"] =
        [
            "Jhenaidah Sadar", "Kaliganj", "Kotchandpur", "Maheshpur", "Shailkupa", "Harinakundu"
        ],
        ["Chuadanga"] =
        [
            "Chuadanga Sadar", "Alamdanga", "Damurhuda", "Jibannagar", "Darshana"
        ],
        ["Meherpur"] =
        [
            "Meherpur Sadar", "Gangni", "Mujibnagar"
        ],
        ["Magura"] =
        [
            "Magura Sadar", "Sreepur", "Mohammadpur", "Shalikha"
        ],
        ["Narail"] =
        [
            "Narail Sadar", "Lohagara", "Kalia"
        ],

        // ------------------ RAJSHAHI DIVISION ------------------
        ["Rajshahi"] =
        [
            "Boalia", "Rajpara", "Motihar", "Shah Mokhdum", "Chandrima", "Kashiadanga", "Katakhali", "Paba",
            "Godagari", "Tanore", "Bagmara", "Durgapur", "Charghat", "Puthia", "Bagha", "Mohanpur"
        ],
        ["Bogura"] =
        [
            "Bogura Sadar", "Shibganj", "Sonatola", "Gabtali", "Sariakandi", "Dhunat", "Sherpur", "Nandigram",
            "Adamdighi", "Dupchanchia", "Kahaloo", "Shajahanpur"
        ],
        ["Pabna"] =
        [
            "Pabna Sadar", "Ishwardi", "Atgharia", "Bera", "Bhangoora", "Chatmohar", "Faridpur", "Santhia", "Sujanagar"
        ],
        ["Sirajganj"] =
        [
            "Sirajganj Sadar", "Belkuchi", "Chauhali", "Kamarkhanda", "Kazipur", "Raiganj", "Shahjadpur",
            "Tarash", "Ullahpara"
        ],
        ["Naogaon"] =
        [
            "Naogaon Sadar", "Atrai", "Badalgachhi", "Dhamoirhat", "Manda", "Mohadevpur", "Niamatpur",
            "Patnitala", "Porsha", "Raninagar", "Sapahar"
        ],
        ["Natore"] =
        [
            "Natore Sadar", "Bagatipara", "Baraigram", "Gurudaspur", "Lalpur", "Singra", "Naldanga"
        ],
        ["Chapainawabganj"] =
        [
            "Chapainawabganj Sadar", "Shibganj", "Gomastapur", "Nachole", "Bholahat"
        ],
        ["Joypurhat"] =
        [
            "Joypurhat Sadar", "Akkelpur", "Kalai", "Khetlal", "Panchbibi"
        ],

        // ------------------ BARISHAL DIVISION ------------------
        ["Barishal"] =
        [
            "Barishal Sadar (Kotwali)", "Airport", "Kawnia", "Bakerganj", "Babuganj", "Wazirpur", "Banaripara",
            "Gournadi", "Agailjhara", "Mehendiganj", "Muladi", "Hizla"
        ],
        ["Patuakhali"] =
        [
            "Patuakhali Sadar", "Bauphal", "Dashmina", "Galachipa", "Kalapara", "Mirzaganj", "Dumki", "Rangabali", "Kuakata"
        ],
        ["Bhola"] =
        [
            "Bhola Sadar", "Burhanuddin", "Char Fasson", "Daulatkhan", "Lalmohan", "Manpura", "Tazumuddin"
        ],
        ["Pirojpur"] =
        [
            "Pirojpur Sadar", "Bhandaria", "Mathbaria", "Nazirpur", "Nesarabad (Swarupkathi)", "Kawkhali", "Zianagar (Indurkani)"
        ],
        ["Jhalakathi"] =
        [
            "Jhalakathi Sadar", "Kathalia", "Nalchhiti", "Rajapur"
        ],
        ["Barguna"] =
        [
            "Barguna Sadar", "Amtali", "Bamna", "Betagi", "Patharghata", "Taltali"
        ],

        // ------------------ SYLHET DIVISION ------------------
        ["Sylhet"] =
        [
            "Kotwali", "Jalalabad", "Airport", "Shah Paran", "Moglabazar", "South Surma", "Sylhet Sadar",
            "Beanibazar", "Golapganj", "Companiganj", "Fenchuganj", "Gowainghat", "Jaintiapur", "Kanaighat",
            "Zakiganj", "Balaganj", "Bishwanath", "Osmani Nagar"
        ],
        ["Moulvibazar"] =
        [
            "Moulvibazar Sadar", "Barlekha", "Juri", "Kamalganj", "Kulaura", "Rajnagar", "Sreemangal"
        ],
        ["Habiganj"] =
        [
            "Habiganj Sadar", "Bahubal", "Baniachong", "Chunarughat", "Ajmiriganj", "Lakhai", "Madhabpur",
            "Nabiganj", "Sayestaganj"
        ],
        ["Sunamganj"] =
        [
            "Sunamganj Sadar", "Chhatak", "Jagannathpur", "Derai", "Dharamapasha", "Dowarabazar",
            "Bishwamvarpur", "Jamalganj", "Sullah", "Tahirpur", "Shantiganj", "Madhyanagar"
        ],

        // ------------------ RANGPUR DIVISION ------------------
        ["Rangpur"] =
        [
            "Kotwali", "Tajhat", "Haragach", "Mahiganj", "Parshuram", "Uttar", "Rangpur Sadar", "Badarganj",
            "Mithapukur", "Pirgachha", "Pirganj", "Kaunia", "Gangachhara", "Taraganj"
        ],
        ["Dinajpur"] =
        [
            "Dinajpur Sadar", "Birampur", "Birganj", "Birol", "Bochaganj", "Chirirbandar", "Fulbari",
            "Ghoraghat", "Hakimpur", "Kaharole", "Khansama", "Nawabganj", "Parbatipur"
        ],
        ["Kurigram"] =
        [
            "Kurigram Sadar", "Nageshwari", "Bhurungamari", "Phulbari", "Rajarhat", "Ulipur", "Chilmari",
            "Rowmari", "Char Rajibpur"
        ],
        ["Gaibandha"] =
        [
            "Gaibandha Sadar", "Gobindaganj", "Palashbari", "Sadullapur", "Sughatta", "Sundarganj", "Shaghata"
        ],
        ["Nilphamari"] =
        [
            "Nilphamari Sadar", "Saidpur", "Domar", "Dimla", "Jaldhaka", "Kishoreganj"
        ],
        ["Lalmonirhat"] =
        [
            "Lalmonirhat Sadar", "Aditmari", "Kaliganj", "Hatibandha", "Patgram"
        ],
        ["Thakurgaon"] =
        [
            "Thakurgaon Sadar", "Pirganj", "Ranisankail", "Haripur", "Baliadangi"
        ],
        ["Panchagarh"] =
        [
            "Panchagarh Sadar", "Boda", "Atwari", "Debiganj", "Tetulia"
        ],

        // ------------------ MYMENSINGH DIVISION ------------------
        ["Mymensingh"] =
        [
            "Kotwali", "Mymensingh Sadar", "Trishal", "Muktagachha", "Bhaluka", "Fulbaria", "Gaffargaon",
            "Gauripur", "Ishwarganj", "Haluaghat", "Dhobaura", "Nandail", "Tara Khanda", "Phulpur"
        ],
        ["Jamalpur"] =
        [
            "Jamalpur Sadar", "Bakshiganj", "Dewanganj", "Islampur", "Madarganj", "Melandaha", "Sarishabari"
        ],
        ["Netrokona"] =
        [
            "Netrokona Sadar", "Kendua", "Mohanganj", "Barhatta", "Atpara", "Kalmakanda", "Durgapur",
            "Purbadhala", "Madan", "Khaliajuri"
        ],
        ["Sherpur"] =
        [
            "Sherpur Sadar", "Nakla", "Nalitabari", "Jhenaigati", "Sreebardi"
        ]
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

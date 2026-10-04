using System;
using UnityEngine;

namespace Flightline
{
    // Origin airport on the boarding pass. Guessed from the phone's time zone (no permission needed),
    // otherwise a random airport picked once and kept.
    public static class HomePort
    {
        static readonly string[] Zones =
        {
            "America/Los_Angeles|LAX|LOS ANGELES", "America/Vancouver|YVR|VANCOUVER", "America/Denver|DEN|DENVER", "America/Phoenix|PHX|PHOENIX",
            "America/Chicago|ORD|CHICAGO", "America/New_York|JFK|NEW YORK", "America/Detroit|DTW|DETROIT", "America/Toronto|YYZ|TORONTO",
            "America/Anchorage|ANC|ANCHORAGE", "Pacific/Honolulu|HNL|HONOLULU", "America/Mexico_City|MEX|MEXICO CITY", "America/Bogota|BOG|BOGOTA",
            "America/Lima|LIM|LIMA", "America/Santiago|SCL|SANTIAGO", "America/Sao_Paulo|GRU|SAO PAULO", "America/Argentina/Buenos_Aires|EZE|BUENOS AIRES",
            "Europe/London|LHR|LONDON", "Europe/Dublin|DUB|DUBLIN", "Europe/Lisbon|LIS|LISBON", "Europe/Madrid|MAD|MADRID", "Europe/Paris|CDG|PARIS",
            "Europe/Amsterdam|AMS|AMSTERDAM", "Europe/Brussels|BRU|BRUSSELS", "Europe/Berlin|BER|BERLIN", "Europe/Zurich|ZRH|ZURICH", "Europe/Rome|FCO|ROME",
            "Europe/Vienna|VIE|VIENNA", "Europe/Stockholm|ARN|STOCKHOLM", "Europe/Oslo|OSL|OSLO", "Europe/Copenhagen|CPH|COPENHAGEN", "Europe/Helsinki|HEL|HELSINKI",
            "Europe/Warsaw|WAW|WARSAW", "Europe/Athens|ATH|ATHENS", "Europe/Istanbul|IST|ISTANBUL", "Europe/Moscow|SVO|MOSCOW", "Asia/Jerusalem|TLV|TEL AVIV",
            "Africa/Cairo|CAI|CAIRO", "Africa/Lagos|LOS|LAGOS", "Africa/Nairobi|NBO|NAIROBI", "Africa/Johannesburg|JNB|JOHANNESBURG",
            "Asia/Riyadh|RUH|RIYADH", "Asia/Dubai|DXB|DUBAI", "Asia/Karachi|KHI|KARACHI", "Asia/Kolkata|DEL|DELHI", "Asia/Calcutta|DEL|DELHI",
            "Asia/Kathmandu|KTM|KATHMANDU", "Asia/Dhaka|DAC|DHAKA", "Asia/Bangkok|BKK|BANGKOK", "Asia/Ho_Chi_Minh|SGN|HO CHI MINH", "Asia/Singapore|SIN|SINGAPORE",
            "Asia/Kuala_Lumpur|KUL|KUALA LUMPUR", "Asia/Jakarta|CGK|JAKARTA", "Asia/Manila|MNL|MANILA", "Asia/Hong_Kong|HKG|HONG KONG", "Asia/Shanghai|PVG|SHANGHAI",
            "Asia/Taipei|TPE|TAIPEI", "Asia/Seoul|ICN|SEOUL", "Asia/Tokyo|HND|TOKYO", "Australia/Perth|PER|PERTH", "Australia/Brisbane|BNE|BRISBANE",
            "Australia/Sydney|SYD|SYDNEY", "Australia/Melbourne|MEL|MELBOURNE", "Pacific/Auckland|AKL|AUCKLAND",
            // Windows zone names (editor only)
            "Pacific Standard Time|LAX|LOS ANGELES", "Mountain Standard Time|DEN|DENVER", "Central Standard Time|ORD|CHICAGO", "Eastern Standard Time|JFK|NEW YORK",
            "GMT Standard Time|LHR|LONDON", "W. Europe Standard Time|FRA|FRANKFURT", "India Standard Time|DEL|DELHI", "Tokyo Standard Time|HND|TOKYO",
        };

        static string code, city;
        public static string Code { get { Resolve(); return code; } }
        public static string City { get { Resolve(); return city; } }

        static void Resolve()
        {
            if (code != null) return;
            string tz = "";
            try { tz = TimeZoneInfo.Local.Id ?? ""; } catch { }
            if (Match(tz)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            // Mono's TimeZoneInfo can report "UTC"/"Local" on some devices; ask Java for the IANA id.
            try
            {
                using (var c = new AndroidJavaClass("java.util.TimeZone"))
                using (var z = c.CallStatic<AndroidJavaObject>("getDefault"))
                    if (Match(z.Call<string>("getID") ?? "")) return;
            }
            catch { }
#endif
            // unknown zone: a random real airport, chosen once per install
            var d = Save.D;
            var q = (d.homePort ?? "").Split('|');
            if (q.Length < 3 || q[1].Length == 0)
            {
                d.homePort = Zones[UnityEngine.Random.Range(0, Zones.Length - 8)]; Save.Write(); // last 8 are Windows names
                q = d.homePort.Split('|');
            }
            code = q[1]; city = q[2];
        }

        static bool Match(string tz)
        {
            if (string.IsNullOrEmpty(tz)) return false;
            foreach (var z in Zones)
            {
                var p = z.Split('|');
                if (string.Equals(p[0], tz, StringComparison.OrdinalIgnoreCase)) { code = p[1]; city = p[2]; return true; }
            }
            return false;
        }
    }
}

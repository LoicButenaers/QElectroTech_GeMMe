using System.Globalization;
using System.Text;

namespace GeMMeElec;

public static partial class Symbols
{
    public static string SearchKey(string text) => string.Concat(text.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToUpperInvariant();
    public static IEnumerable<SymbolDefinition> Search(string query, string category = "")
    {
        var key = SearchKey(query); return All.Values.Where(s => (category == "" || s.Category == category) && SearchKey(s.Name + " " + s.Id + " " + s.Category).Contains(key)).OrderBy(s => s.Category).ThenBy(s => s.Name);
    }
    private static void Extend(Dictionary<string, SymbolDefinition> all)
    {
        const string protection = "01 · Protection et coupure", contacts = "02 · Contacts et commandes", coils = "03 · Relais et actionneurs", power = "04 · Sources et conversion", motors = "05 · Moteurs et récepteurs", signals = "06 · Signalisation et mesure", sensors = "07 · Capteurs", automation = "08 · Automatisme", terminals = "09 · Bornes et connexions", refs = "10 · Renvois interfolios";
        void Category(string category, params string[] ids) { foreach (var id in ids) all[id] = all[id] with { Category = category }; }
        Category(protection, "breaker1", "breaker2", "breaker3", "breaker4", "rcd2", "rcd4", "isolator", "motorbreaker", "fuse", "overload");
        Category(contacts, "contact_no", "contact_nc", "push_no", "push_nc", "selector", "estop", "contactor");
        Category(coils, "coil", "timer", "safety"); Category(power, "source", "psu", "transformer", "drive"); Category(motors, "motor"); Category(signals, "lamp"); Category(sensors, "sensor_ind", "sensor_photo"); Category(automation, "plc", "io"); Category(terminals, "terminal", "terminal_pe", "earth", "junction");
        void Add(string id, string name, string prefix, string category, double w, double h, List<Port> ports, List<Shape> shapes) => all.Add(id, new(id, name, prefix, w, h, ports, shapes) { Category = category });
        void Two(string id, string name, string prefix, string category, List<Shape> body, string top = "1", string bottom = "2") => Add(id, name, prefix, category, 80, 100, [new(top, 0, -50), new(bottom, 0, 50)], [L(0, -50, 0, -25), .. body, L(0, 25, 0, 50)]);
        void Contact(string id, string name, string category, string mark, bool nc, string top = "", string bottom = "")
        {
            var basis = all[nc ? "contact_nc" : "contact_no"];
            Add(id, name, category == contacts ? "S" : "K", category, 100, 100,
                [new(top.Length > 0 ? top : nc ? "21" : "13", 0, -50), new(bottom.Length > 0 ? bottom : nc ? "22" : "14", 0, 50)],
                [.. basis.Shapes, L(-8, 0, -30, 0), R(-52, -14, 22, 28), T(-50, -9, mark, 10)]);
        }
        foreach (int poles in new[] { 1, 2, 4 })
        {
            foreach (var (stem, name, prefix, cat) in new[] { ("isolator", "Sectionneur", "Q", protection), ("contactor", "Contacteur de puissance", "KM", contacts) })
            {
                List<Port> ports = []; List<Shape> shapes = [];
                for (int i = 0; i < poles; i++) { double x = (i - (poles - 1) / 2.0) * 40; ports.AddRange([new((2 * i + 1).ToString(), x, -50), new((2 * i + 2).ToString(), x, 50)]); shapes.AddRange([L(x, -50, x, -16), L(x, 16, x, 50), L(x, 16, x + 13, -14)]); if (stem == "contactor") shapes.Add(L(x - 8, -16, x + 8, -16)); }
                if (poles > 1) for (double x = -(poles - 1) * 20; x < (poles - 1) * 20; x += 10) shapes.Add(L(x, 4, x + 5, 4));
                Add(stem + poles, name + $" {poles}P", prefix, cat, poles * 40 + 10, 100, ports, shapes);
            }
        }
        Contact("overload_nc", "Relais thermique · contact 95–96 NC", contacts, "θ", true, "95", "96");
        Contact("overload_no", "Relais thermique · contact 97–98 NO", contacts, "θ", false, "97", "98");
        foreach (var (id, name, mark, cat) in new[] { ("limit", "Fin de course", "○", contacts), ("pressure", "Pressostat", "P", sensors), ("thermostat", "Thermostat", "θ", sensors), ("level", "Contact de niveau", "h", sensors), ("timer_contact", "Contact temporisé", "t", contacts) })
        { Contact(id + "_no", name + " NO", cat, mark, false); Contact(id + "_nc", name + " NC", cat, mark, true); }
        var selector = all["selector"]; Add("selector_nc", "Sélecteur · contact NC", "S", contacts, 80, 100, all["contact_nc"].Ports.ToList(), [.. all["contact_nc"].Shapes, L(-8, 0, -24, 0), L(-24, 0, -35, -13)]);
        Add("changeover", "Contact inverseur", "K", contacts, 90, 100, [new("11", 0, 50), new("12", -20, -50), new("14", 20, -50)], [L(0, 50, 0, 15), L(0, 15, -20, -15), L(-20, -15, -20, -50), L(20, -15, 20, -50), L(-27, -15, -13, -15)]);
        Two("timer_on", "Temporisation à l’enclenchement · bobine", "KT", coils, [R(-25, -25, 50, 50), T(-19, -9, "t ON", 12)], "A1", "A2");
        Two("timer_off", "Temporisation au déclenchement · bobine", "KT", coils, [R(-25, -25, 50, 50), T(-21, -9, "t OFF", 11)], "A1", "A2");
        Two("solenoid", "Électrovanne · bobine", "Y", coils, [R(-25, -25, 50, 50), L(-25, 25, 25, -25)], "A1", "A2");
        Two("brake", "Frein électromagnétique", "Y", coils, [R(-25, -25, 50, 50), T(-10, -12, "B", 20)], "+", "−");
        Two("relay_latch", "Relais bistable · bobine", "K", coils, [R(-25, -25, 50, 50), T(-17, -9, "SET", 12)], "A1", "A2");
        Two("resistor", "Résistance", "R", motors, [R(-10, -25, 20, 50)]);
        Two("heater", "Résistance chauffante", "E", motors, [R(-15, -25, 30, 50), L(-9, 12, 9, -12), T(20, -9, "θ", 13)]);
        Two("potentiometer", "Résistance variable · rhéostat", "R", motors, [R(-10, -25, 20, 50), L(-26, 22, 26, -22), L(26, -22, 14, -20), L(26, -22, 23, -10)]);
        Two("capacitor", "Condensateur", "C", power, [L(0, -25, 0, -6), L(-22, -6, 22, -6), L(-22, 6, 22, 6), L(0, 6, 0, 25)]);
        Two("diode", "Diode", "V", power, [L(0, -25, 0, -16), L(-16, -16, 16, -16), L(-16, -16, 0, 16), L(16, -16, 0, 16), L(-16, 16, 16, 16), L(0, 16, 0, 25)], "A", "K");
        Two("battery", "Batterie / source DC", "G", power, [L(0, -25, 0, -7), L(-22, -7, 22, -7), L(-12, 7, 12, 7), L(0, 7, 0, 25), T(25, -22, "+")], "+", "−");
        foreach (var (id, name, label, prefix, cat) in new[] { ("motor1", "Moteur monophasé", "M 1~", "M", motors), ("motor_dc", "Moteur courant continu", "M =", "M", motors), ("generator", "Générateur AC", "G ~", "G", power), ("ammeter", "Ampèremètre", "A", "P", signals), ("voltmeter", "Voltmètre", "V", "P", signals), ("buzzer", "Avertisseur sonore", "BZ", "H", signals) })
            Two(id, name, prefix, cat, [C(0, 0, 25), T(-18, -10, label, 14)]);
        Add("motor6", "Moteur triphasé · six bornes d’enroulements", "M", motors, 140, 140, [new("U1", -30, -70), new("V1", 0, -70), new("W1", 30, -70), new("U2", -30, 70), new("V2", 0, 70), new("W2", 30, 70), new("PE", 70, 0)], [C(0, 0, 42), T(-12, -20, "M", 24), T(-10, 10, "3~", 14), L(-30, -70, -30, -30), L(0, -70, 0, -42), L(30, -70, 30, -30), L(-30, 30, -30, 70), L(0, 42, 0, 70), L(30, 30, 30, 70), L(42, 0, 70, 0)]);
        foreach (var (id, name, label, cat) in new[] { ("rectifier", "Pont redresseur · bloc fonctionnel", "AC / DC", power), ("dc_converter", "Convertisseur DC / DC", "DC / DC", power), ("softstarter", "Démarreur progressif · bloc fonctionnel", "SOFT", power), ("plc_di", "Entrée automate numérique", "DI", automation), ("plc_do", "Sortie automate numérique", "DO", automation), ("plc_ai", "Entrée automate analogique", "AI", automation), ("plc_ao", "Sortie automate analogique", "AO", automation), ("sensor_cap", "Détecteur capacitif · bloc 3 fils", "C", sensors), ("sensor_pressure", "Transmetteur de pression · bloc", "P / I", sensors), ("encoder", "Codeur incrémental · bloc", "ENC", sensors) })
        {
            string[] top = id == "softstarter" ? ["L1", "L2", "L3"] : id == "rectifier" ? ["AC1", "AC2"] : ["L+", "M"];
            string[] bottom = id == "softstarter" ? ["U", "V", "W"] : id is "rectifier" or "dc_converter" ? ["+", "−"] : id == "encoder" ? ["A", "B", "Z"] : id == "sensor_cap" ? ["OUT"] : ["SIG", "COM"];
            List<Port> ports = []; List<Shape> shapes = [R(-60, -35, 120, 70), T(-48, -10, label, 16)];
            foreach (var pair in new[] { (top, -60, -35), (bottom, 60, 35) }) for (int i = 0; i < pair.Item1.Length; i++) { double x = (i - (pair.Item1.Length - 1) / 2.0) * 30; ports.Add(new(pair.Item1[i], x, pair.Item2)); shapes.Add(L(x, pair.Item2, x, pair.Item3)); }
            Add(id, name, cat == sensors ? "B" : "A", cat, 130, 120, ports, shapes);
        }
        Two("rtd", "Sonde résistive · Pt100 / Pt1000 (2 fils)", "B", sensors, [R(-12, -25, 24, 50), L(-22, 22, 22, -22), T(22, -16, "θ", 12)]);
        Two("thermocouple", "Thermocouple · bloc fonctionnel", "B", sensors, [R(-22, -25, 44, 50), T(-16, -10, "TC", 16)], "+", "−");
        Add("terminal_disconnect", "Borne sectionnable", "X", terminals, 50, 60, [new("1", 0, -30), new("2", 0, 30)], [L(0, -30, 0, -8), L(0, 8, 0, 30), L(0, 8, 12, -8), C(0, -15, 3), C(0, 15, 3)]);
        Add("terminal_fuse", "Borne porte-fusible", "X", terminals, 40, 60, [new("1", 0, -30), new("2", 0, 30)], [L(0, -30, 0, 30), R(-8, -15, 16, 30)]);
        Add("terminal_n", "Borne de neutre N", "X", terminals, 50, 40, all["terminal"].Ports.ToList(), [.. all["terminal"].Shapes, T(12, -8, "N")]);
        Add("connector", "Connecteur débrochable · un contact", "X", terminals, 50, 70, [new("1", 0, -35), new("2", 0, 35)], [L(0, -35, 0, 0), L(0, 8, 0, 35), L(-10, -2, -10, 8), L(-10, 8, 10, 8), L(10, 8, 10, -2)]);
        Add("shield", "Raccordement de blindage", "SH", terminals, 60, 60, [new("SH", 0, -30)], [L(0, -30, 0, -12), C(0, 0, 12), L(-20, 18, 20, 18), L(-20, 18, -25, 25), L(0, 18, -5, 25), L(20, 18, 15, 25)]);
        Add("functional_earth", "Terre fonctionnelle FE", "FE", terminals, 50, 50, [new("FE", 0, -25)], [L(0, -25, 0, 0), L(-18, 0, 18, 0), L(-18, 0, -23, 9), L(0, 0, -5, 9), L(18, 0, 13, 9)]);
        Add("folio_next", "Folio suivant · départ de renvoi", "RF", refs, 80, 30, [new("1", -40, 0)], [L(-40, 0, 20, 0), L(20, 0, 6, -10), L(20, 0, 6, 10)]);
        Add("folio_previous", "Folio précédent · arrivée de renvoi", "RF", refs, 80, 30, [new("1", 40, 0)], [L(-20, 0, 40, 0), L(0, 0, -14, -10), L(0, 0, -14, 10)]);
    }
}

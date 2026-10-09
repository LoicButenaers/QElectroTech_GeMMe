namespace GeMMeElec;

public sealed record Port(string Id, double X, double Y);
public sealed record Shape(string Kind, double X, double Y, double A = 0, double B = 0, string Text = "");
public sealed record SymbolDefinition(string Id, string Name, string Prefix, double Width, double Height, List<Port> Ports, List<Shape> Shapes);

// Original vector drawings. Equipment blocks describe functions; terminal numbering
// must be checked against the exact manufacturer's article before wiring.
public static class Symbols
{
    public static readonly Dictionary<string, SymbolDefinition> All = Build();
    private static Shape L(double x, double y, double a, double b) => new("line", x, y, a, b);
    private static Shape R(double x, double y, double w, double h) => new("rect", x, y, w, h);
    private static Shape C(double x, double y, double r) => new("circle", x, y, r);
    private static Shape T(double x, double y, string text, double size = 12) => new("text", x, y, size, 0, text);
    private static Dictionary<string, SymbolDefinition> Build()
    {
        Dictionary<string, SymbolDefinition> all = [];
        void Add(string id, string name, string prefix, double w, double h, List<Port> ports, List<Shape> shapes) => all.Add(id, new(id, name, prefix, w, h, ports, shapes));
        void Switch(string id, string name, string prefix, int poles, string kind)
        {
            List<Port> ports = []; List<Shape> shapes = [];
            for (int i = 0; i < poles; i++)
            {
                double x = (i - (poles - 1) / 2.0) * 40;
                ports.Add(new((i * 2 + 1).ToString(), x, -50)); ports.Add(new((i * 2 + 2).ToString(), x, 50));
                shapes.AddRange([L(x, -50, x, -16), L(x, 16, x, 50), L(x, 16, x + 13, -14)]);
                if (kind is "breaker" or "motorbreaker" or "rcd") shapes.AddRange([L(x - 6, -22, x + 6, -10), L(x + 6, -22, x - 6, -10), R(x - 7, 26, 14, 10)]);
                if (kind == "contactor") shapes.Add(L(x - 8, -16, x + 8, -16));
                if (kind == "rcd") shapes.Add(T(x - 13, 0, "Δ", 11));
            }
            if (poles > 1) for (double x = -(poles - 1) * 20; x < (poles - 1) * 20; x += 10) shapes.Add(L(x, 4, x + 5, 4));
            Add(id, name, prefix, poles * 40 + 10, 100, ports, shapes);
        }
        for (int n = 1; n <= 4; n++) Switch("breaker" + n, $"Disjoncteur {n}P", "Q", n, "breaker");
        Switch("rcd2", "Interrupteur différentiel 2P", "FI", 2, "rcd");
        Switch("rcd4", "Interrupteur différentiel 4P", "FI", 4, "rcd");
        Switch("isolator", "Interrupteur-sectionneur 3P", "Q", 3, "isolator");
        Switch("motorbreaker", "Disjoncteur moteur 3P", "Q", 3, "motorbreaker");
        Switch("contactor", "Contacteur de puissance 3P", "KM", 3, "contactor");
        Add("fuse", "Fusible", "F", 40, 100, [new("1", 0, -50), new("2", 0, 50)], [L(0, -50, 0, 50), R(-10, -25, 20, 50)]);
        List<Port> thermalPorts = []; List<Shape> thermal = [];
        for (int i = 0; i < 3; i++) { int x = (i - 1) * 40; thermalPorts.AddRange([new((2 * i + 1).ToString(), x, -50), new((2 * i + 2).ToString(), x, 50)]); thermal.AddRange([L(x, -50, x, 50), R(x - 10, -20, 20, 40), L(x - 7, 10, x + 7, -10)]); }
        Add("overload", "Relais thermique 3P", "F", 130, 100, thermalPorts, thermal);
        void Contact(string id, string name, bool nc, string actuator = "")
        {
            List<Shape> shapes = [L(0, -50, 0, -15), L(0, 15, 0, 50), L(0, 15, nc ? 0 : 15, -15)];
            if (nc) shapes.Add(L(-10, -15, 10, -15));
            if (actuator.Length > 0) { shapes.AddRange([L(-6, 0, -24, 0), L(-24, -14, -24, 14)]); if (actuator == "stop") shapes.Add(R(-34, -17, 10, 34)); if (actuator == "selector") shapes.Add(L(-24, 0, -35, -13)); }
            Add(id, name, actuator.Length > 0 ? "S" : "K", 80, 100, [new(nc ? "21" : "13", 0, -50), new(nc ? "22" : "14", 0, 50)], shapes);
        }
        Contact("contact_no", "Contact normalement ouvert", false); Contact("contact_nc", "Contact normalement fermé", true);
        Contact("push_no", "Bouton-poussoir NO", false, "button"); Contact("push_nc", "Bouton-poussoir NC", true, "button");
        Contact("estop", "Arrêt d’urgence · contact NC", true, "stop"); Contact("selector", "Sélecteur · contact NO", false, "selector");
        Add("coil", "Bobine de relais / contacteur", "K", 70, 100, [new("A1", 0, -50), new("A2", 0, 50)], [L(0, -50, 0, -20), R(-25, -20, 50, 40), L(0, 20, 0, 50)]);
        Add("timer", "Relais temporisé · bobine", "KT", 70, 100, [new("A1", 0, -50), new("A2", 0, 50)], [L(0, -50, 0, -20), R(-25, -20, 50, 40), L(0, 20, 0, 50), T(-5, -10, "t")]);
        Add("lamp", "Voyant lumineux", "H", 60, 100, [new("X1", 0, -50), new("X2", 0, 50)], [L(0, -50, 0, -22), C(0, 0, 22), L(-15, -15, 15, 15), L(15, -15, -15, 15), L(0, 22, 0, 50)]);
        Add("terminal", "Borne de passage", "X", 40, 40, [new("1", 0, -20), new("2", 0, 20)], [L(0, -20, 0, 20), C(0, 0, 6)]);
        Add("terminal_pe", "Borne de protection PE", "X", 50, 60, [new("1", 0, -30), new("2", 0, 30)], [L(0, -30, 0, 30), C(0, 0, 6), L(0, 10, 18, 10), L(18, 0, 18, 20), L(23, 3, 23, 17), L(28, 6, 28, 14)]);
        Add("earth", "Terre de protection", "PE", 40, 40, [new("PE", 0, -20)], [L(0, -20, 0, 0), L(-18, 0, 18, 0), L(-12, 6, 12, 6), L(-6, 12, 6, 12)]);
        Add("junction", "Jonction électrique", "J", 20, 20, [new("1", 0, 0)], [new("dot", 0, 0, 3)]);
        Add("source", "Arrivée d’alimentation", "L", 50, 40, [new("1", 0, 20)], [L(0, -10, 0, 20), L(-8, -2, 0, -10), L(8, -2, 0, -10)]);
        Add("motor", "Moteur triphasé", "M", 120, 120, [new("U1", -20, -60), new("V1", 0, -60), new("W1", 20, -60), new("PE", 60, 20)], [C(0, 0, 38), T(-10, -20, "M", 24), T(-10, 10, "3~", 15), L(-20, -60, -20, -33), L(0, -60, 0, -38), L(20, -60, 20, -33), L(33, 20, 60, 20)]);
        void Block(string id, string name, string prefix, string label, string[] top, string[] bottom)
        {
            double width = Math.Max(120, Math.Max(top.Length, bottom.Length) * 30 + 30);
            List<Port> ports = []; List<Shape> shapes = [R(-width / 2, -40, width, 80), T(-width / 2 + 12, -10, label, 16)];
            foreach (var pair in new[] { (top, -60, -40), (bottom, 60, 40) }) for (int i = 0; i < pair.Item1.Length; i++) { double x = (i - (pair.Item1.Length - 1) / 2.0) * 30; ports.Add(new(pair.Item1[i], x, pair.Item2)); shapes.Add(L(x, pair.Item2, x, pair.Item3)); }
            Add(id, name, prefix, width + 10, 120, ports, shapes);
        }
        Block("psu", "Alimentation AC / DC", "G", "AC / DC", ["L", "N", "PE"], ["+", "−"]);
        Block("plc", "Automate · bloc fonctionnel", "A", "PLC", ["L+", "M"], ["I0", "I1", "Q0", "Q1"]);
        Block("io", "Module E/S · bloc fonctionnel", "A", "I/O", ["L+", "M"], ["CH0", "CH1", "CH2", "CH3"]);
        Block("drive", "Variateur de fréquence", "U", "AC / AC", ["L1", "L2", "L3", "PE"], ["U", "V", "W"]);
        Block("safety", "Relais de sécurité · bloc fonctionnel", "K", "SAFETY", ["A1", "A2", "S11", "S12"], ["13", "14", "23", "24"]);
        Block("sensor_ind", "Détecteur inductif · bloc 3 fils", "B", ")))", ["L+", "M"], ["OUT"]);
        Block("sensor_photo", "Détecteur photoélectrique · bloc 3 fils", "B", "↗ ↗", ["L+", "M"], ["OUT"]);
        Block("transformer", "Transformateur · bloc fonctionnel", "T", "~ / ~", ["P1", "P2"], ["S1", "S2"]);
        return all;
    }
}

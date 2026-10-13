namespace GeMMeElec;

// Only explicit, permanent continuity joins device terminals. Switches, coils,
// fuse holders and separate equipment outputs never acquire inferred edges.
public sealed class ConductorNetworks
{
    private readonly Dictionary<Endpoint, HashSet<Endpoint>> edges = [];
    private readonly Dictionary<Endpoint, List<Wire>> wires = [];
    private readonly Dictionary<string, Component> components;
    public ConductorNetworks(Project project)
    {
        components = project.Sheets.SelectMany(s => s.Components).ToDictionary(c => c.Id);
        void Edge(Endpoint a, Endpoint b) { if (!edges.TryGetValue(a, out var list)) edges[a] = list = []; list.Add(b); }
        void Pair(Endpoint a, Endpoint b) { Edge(a, b); Edge(b, a); }
        void WireAt(Endpoint at, Wire wire) { if (!wires.TryGetValue(at, out var list)) wires[at] = list = []; list.Add(wire); }
        foreach (var wire in project.Sheets.SelectMany(s => s.Wires)) { Pair(wire.From, wire.To); WireAt(wire.From, wire); WireAt(wire.To, wire); }
        foreach (var c in components.Values) foreach (var pair in c.Definition.InternalConnections) Pair(new(c.Id, pair.From), new(c.Id, pair.To));
        foreach(var strip in project.TerminalStrips)foreach(var bridge in strip.Bridges)Pair(new(bridge.FromComponent,bridge.FromPort),new(bridge.ToComponent,bridge.ToPort));
        var refs = new CrossReferences(project);
        foreach (var point in CrossReferences.Points(project)) foreach (var target in refs.Targets(point.Component.Id)) Pair(new(point.Component.Id, "1"), new(target.Component.Id, "1"));
    }
    public HashSet<Endpoint> Trace(Endpoint start)
    {
        if (!components.TryGetValue(start.ComponentId, out var c) || !c.Definition.Ports.Any(p => p.Id == start.PortId)) throw new ArgumentException("Borne introuvable.");
        HashSet<Endpoint> seen = [start]; Queue<Endpoint> queue = new(); queue.Enqueue(start);
        while (queue.TryDequeue(out var at)) foreach (var next in edges.GetValueOrDefault(at) ?? []) if (seen.Add(next)) queue.Enqueue(next);
        return seen;
    }
    public IEnumerable<Wire> Connected(Endpoint start) => Trace(start).SelectMany(at => wires.GetValueOrDefault(at) ?? []).DistinctBy(w => w.Id);
    public string? ExistingNumber(Endpoint from, Endpoint to, string? except = null) => Connected(from).Concat(Connected(to)).FirstOrDefault(w => w.Id != except && !w.NumberIndependent && w.Number.Length > 0)?.Number;
}

public sealed partial class Editor
{
    internal string NumberForConnection(Endpoint from, Endpoint to, string? except = null) => new ConductorNetworks(Project).ExistingNumber(from, to, except) ?? $"W{NextWireNumber()}";
    internal void SynchronizeWireNumbers(Wire wire, string number)
    {
        if (wire.NumberIndependent) { wire.Number = number; return; }
        foreach (var connected in new ConductorNetworks(Project).Connected(wire.From).Where(w => !w.NumberIndependent)) connected.Number = number;
    }
    internal void ConfigureIndependentNumber(Wire wire, bool independent)
    {
        if (wire.NumberIndependent == independent) return;
        wire.NumberIndependent = independent;
        if (!independent) SynchronizeWireNumbers(wire, new ConductorNetworks(Project).ExistingNumber(wire.From, wire.To, wire.Id) ?? wire.Number);
    }
    public void SetWireNumberIndependent(string id, bool independent) => Change(() => ConfigureIndependentNumber(Sheet.Wires.Single(w => w.Id == id), independent));
    public void SetWireNumber(string id, string number, string? historyKey = null) => Change(() => SynchronizeWireNumbers(Sheet.Wires.Single(w => w.Id == id), number), historyKey);
}

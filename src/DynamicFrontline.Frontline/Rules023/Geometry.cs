namespace DynamicFrontline.Frontline.Rules023;

// Conservative raster certificate: a continuously dominant, spatially embedded source-edge
// separator (or an explicitly blocked terrain portal), plus independent counterfactual reachability.
// A blocked start cell in the influence field is never, by itself, a certificate.
public sealed class EncirclementRegionDetector(PreparedTerrain terrain, Rules023Config config)
{
    private sealed record Cache(long FieldRevision, long TerrainRevision, bool[] Baseline, bool[] Route, bool[] SeparatorReach, bool[] Separator, bool Complete)
    { public Dictionary<int,EncirclementCertificate> Pockets {get;}=new(); }
    private readonly Dictionary<(int, Mobility), Cache> caches = new();
    public bool OpenGap(double physicalWidth) => physicalWidth >= config.CorridorWidth;
    public GeometryResult Evaluate(Unit023 unit, ResolvedZoc[] sources, ZocInfluenceField field)
    {
        if (!field.Complete || !terrain.Complete || terrain.Revision < 1) return new(CorridorState.Unknown, Evidence.Unknown, null);
        var g = terrain.Grid; var cell = g.Index(unit.Position); if (cell < 0||!terrain.Passable[(int)unit.Profile.Mobility][cell]) return new(CorridorState.Unknown, Evidence.Unknown, null);
        var key = (unit.Team, unit.Profile.Mobility);
        if (!caches.TryGetValue(key, out var c) || c.FieldRevision != field.Revision || c.TerrainRevision != terrain.Revision)
            caches[key] = c = Build(unit.Team, unit.Profile.Mobility, sources, field);
        if (!c.Complete) return new(CorridorState.Unknown, Evidence.Unknown, null);
        if (c.Route[cell]) return new(CorridorState.Open, Evidence.False, null);
        if (!c.Baseline[cell]) return new(CorridorState.Blocked, Evidence.False, null); // geography alone
        bool enclosed = !c.SeparatorReach[cell];int anchor=cell;
        if (c.Separator[cell])
        {
            // Several valid separator edges can cross at the query cell in a filled ring.
            // Inspect the nearest non-edge raster neighbourhood rather than treating an
            // intersection of internal chords as an exit. An exterior neighbour vetoes it.
            var nearby = new List<int>();
            for (int r = 1; r <= 8 && nearby.Count == 0; r++)
            for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                int x = cell % g.Width + dx, y = cell / g.Width + dy;
                if (x < 0 || y < 0 || x >= g.Width || y >= g.Height) continue;
                var j = y * g.Width + x; if (terrain.Passable[(int)unit.Profile.Mobility][j] && !c.Separator[j]) nearby.Add(j);
            }
            var near = nearby.ToArray();
            enclosed = near.Length > 0 && near.All(i => !c.SeparatorReach[i]);
            if(enclosed)anchor=near[0];
        }
        if (!enclosed) return new(CorridorState.Contested, Evidence.Unknown, null);
        if(!c.Pockets.TryGetValue(anchor,out var certificate)){
            var interior=new HashSet<int>();var boundary=new HashSet<int>();var queue=new Queue<int>();queue.Enqueue(anchor);interior.Add(anchor);
            while(queue.TryDequeue(out var i))foreach(var j in g.Neighbors(i)){
                if(!terrain.EdgeAllowed(i,j,unit.Profile.Mobility)||!terrain.Passable[(int)unit.Profile.Mobility][j]||!c.Baseline[j]||c.SeparatorReach[j])continue;
                if(c.Separator[j])boundary.Add(j);else if(interior.Add(j))queue.Enqueue(j);
            }
            certificate=new(unit.Team,unit.Profile.Mobility,terrain.Revision,interior.OrderBy(i=>i).ToArray(),boundary.OrderBy(i=>i).ToArray(),"Connected pocket bounded by dominant separator","Conservative raster/native edge certificate; cell-size tolerance",true,true);
            foreach(var i in interior)c.Pockets[i]=certificate;
        }
        return new(CorridorState.Blocked,Evidence.True,certificate);
    }
    private Cache Build(int team, Mobility mobility, ResolvedZoc[] sources, ZocInfluenceField field)
    {
        var g = terrain.Grid; var pass = terrain.Passable[(int)mobility]; var blocked = new bool[g.Count]; var separator = new bool[g.Count]; int spent = 0;
        var enemy = 1 - team;
        for (int i = 0; i < g.Count; i++) blocked[i] = field.Control(i, config) == enemy;
        // Width is checked against dominant control, not raw circles. Raster cells are the
        // declared spatial resolution; exact narrow openings are represented by portals.
        var widened = (bool[])blocked.Clone(); int radius = (int)Math.Ceiling(config.CorridorWidth / (2 * g.CellSize));
        for (int i = 0; i < g.Count; i++) if (blocked[i])
        for (int dy = -radius; dy <= radius; dy++) for (int dx = -radius; dx <= radius; dx++)
        {
            int x = i % g.Width + dx, y = i / g.Width + dy;
            if (x >= 0 && y >= 0 && x < g.Width && y < g.Height && Math.Sqrt(dx * dx + dy * dy) * g.CellSize < config.CorridorWidth / 2) widened[y * g.Width + x] = true;
        }
        var enemies = sources.Where(s => s.Unit.Team == enemy && s.Power > 0).ToArray();
        var spatial = new SpatialIndex(config.CombinedRadius * 2);
        for (int i = 0; i < enemies.Length; i++) spatial.Add(enemies[i].Unit.Position, i);
        for (int i = 0; i < enemies.Length; i++) foreach (var j in spatial.Near(enemies[i].Unit.Position))
        {
            if (j <= i) continue; var a = enemies[i]; var b = enemies[j];
            var distance = Math.Sqrt(a.Unit.Position.DistanceSquared(b.Unit.Position)); if (distance >= a.Radius + b.Radius + config.CorridorWidth) continue;
            int n = Math.Max(1, (int)Math.Ceiling(distance / (g.CellSize / 4))); var cells = new List<int>(); bool continuous = true;
            for (int k = 0; k <= n; k++)
            {
                if (++spent > config.GeometryBudget) return new(field.Revision, terrain.Revision, [], [], [], [], false);
                var p = new Point2(a.Unit.Position.X + (b.Unit.Position.X - a.Unit.Position.X) * k / n, a.Unit.Position.Y + (b.Unit.Position.Y - a.Unit.Position.Y) * k / n);
                int cell = g.Index(p); if (cell < 0 || !pass[cell] || !widened[cell]) { continuous = false; break; } cells.Add(cell);
            }
            if (continuous) foreach (var cell in cells) separator[cell] = true;
        }
        bool PortalBlocked(NavPortal p) => blocked[p.A] || blocked[p.B];
        var baseline = Flood(_ => true, _ => true);
        var route = Flood(i => !widened[i], p => !PortalBlocked(p));
        var topology = Flood(i => !separator[i], p => !PortalBlocked(p));
        if(terrain.NativeEdges!=null){
            // A conservative native quotient proves routes, not the absence of omitted routes.
            // For a closed-ring certificate allow every omitted terrain edge/cell as a possible
            // escape. Only an enemy separator that also closes this optimistic graph is proof.
            var optimistic=new bool[g.Count];var q=new Queue<int>();
            void Visit(int i){if(i>=0&&i<g.Count&&!separator[i]&&!optimistic[i]){optimistic[i]=true;q.Enqueue(i);}}
            foreach(var root in terrain.Roots.Where(r=>r.Team==team))Visit(g.Index(root.Position));
            while(q.TryDequeue(out var i)){if(++spent>config.GeometryBudget)break;foreach(var j in g.Neighbors(i))Visit(j);}
            for(int i=0;i<g.Count;i++)topology[i]|=optimistic[i];
        }
        return new(field.Revision, terrain.Revision, baseline, route, topology, separator, spent <= config.GeometryBudget);

        bool[] Flood(Func<int, bool> permitted, Func<NavPortal, bool> portalPermitted)
        {
            var reached = new bool[g.Count]; var queue = new Queue<int>();
            void Visit(int i) { if (i >= 0 && i < g.Count && pass[i] && permitted(i) && !reached[i]) { reached[i] = true; queue.Enqueue(i); } }
            foreach (var root in terrain.Roots.Where(r => r.Team == team)) Visit(g.Index(root.Position));
            while (queue.TryDequeue(out var i))
            {
                if (++spent > config.GeometryBudget) break;
                foreach (var j in g.Neighbors(i)) if(terrain.EdgeAllowed(i,j,mobility))Visit(j);
                foreach (var p in terrain.Portals) if (p.Mobility == mobility && p.Open && p.Width > 0 && !string.IsNullOrWhiteSpace(p.Provenance) && portalPermitted(p))
                { if (p.A == i) Visit(p.B); else if (p.B == i) Visit(p.A); }
            }
            return reached;
        }
    }
}

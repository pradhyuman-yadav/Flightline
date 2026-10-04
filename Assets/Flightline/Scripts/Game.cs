using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Flightline
{
    public enum GState { Menu, Playing, Paused, Crashing, Over }
    // Lightning = charging storm cell. Arc = live electric arc (wire between two nodes, or a sweeping hand).
    public enum Kind { Coin, Fuel, Storm, Balloon, Drone, Shield, Magnet, Boost, Lightning, Wind, Gate, Arc }

    public class Ent
    {
        public Kind kind; public GameObject go; public Transform t; public SpriteRenderer sr;
        public Transform child; public SpriteRenderer childSr;
        public float r, r0, vx, baseX, phase, baseScale = 1f, scale0 = 1f, h, minX, maxX, t2, extent, minD;
        public bool passed, warned, struck; public int mode;
        public Vector2 a, b;
        public SpriteRenderer[] partSr; public LineRenderer[] lines; public float[] fa, fb;
        public string chunk;
        public bool Hazard => kind == Kind.Storm || kind == Kind.Balloon || kind == Kind.Drone || kind == Kind.Lightning || kind == Kind.Arc;
        public bool Custom => kind == Kind.Lightning || kind == Kind.Arc; // own hit + near-miss logic
    }

    class Particle { public Transform t; public SpriteRenderer sr; public Vector2 v; public float life, max, s0, s1, drag; public Color c; public bool scroll; }
    class Flyer { public Transform t; public SpriteRenderer sr; public Vector3 a, c; public float k; }

    public class RunResult
    {
        public int score, coins, earned, near, zone, xp, flight, level, levelUps;
        public float miles; public bool best; public string reason;
    }

    // Root of the game. Lives on a single GameObject in the scene and builds everything at runtime.
    public partial class Game : MonoBehaviour
    {
        public static Game I;
        public GState state;
        public Camera cam;
        public float halfW, halfH;

        // ----- run state -----
        public Prog.RunStats run = new Prog.RunStats();
        public float fuel = 1f, score, combo, shieldT, magnetT, boostT, invulnT, speed, miles;
        public float shieldMax = 1, magnetMax = 1, boostMax = 1;
        public int multiplier = 1, zone, flightNo;
        public bool headStart;
        public string endReason;
        float headStartMiles, takeoffT, achTimer, crashT, crashRot, trailT, shake, nightK;
        bool lowFuelWarned;

        // ----- player -----
        Transform plane, planeShadow; SpriteRenderer planeSr, shieldSr, magnetSr, shadowSr;
        float planeX, planeVX, targetX, planeAlt, planeTilt, surge; bool dragging; float dragPointer0, dragPlane0;
        public const float PlaneY = -4.6f; const float PlaneR = 0.36f;
        const float SteerK = 560f, SteerZeta = 0.8f; // spring steering: stiffness and damping ratio (<1 = slight overshoot)

        // ----- camera -----
        float camX, camY, camRoll;
        const float MenuCamY = -3.4f; // on the menu the camera sits lower so the plane rests mid-screen, clear of the buttons
        int paidZone;

        // ----- world -----
        readonly List<Ent> ents = new List<Ent>(); readonly Dictionary<Kind, Stack<Ent>> pool = new Dictionary<Kind, Stack<Ent>>();
        readonly List<Vector2> rowHaz = new List<Vector2>();
        float travel, nextRowAt, nextFuelAt, nextPowerAt;
        Transform world, runwayT; SpriteRenderer skyBase, skyTop, runway;
        readonly List<SpriteRenderer> farClouds = new List<SpriteRenderer>(), nearClouds = new List<SpriteRenderer>(), streaks = new List<SpriteRenderer>(), stars = new List<SpriteRenderer>();
        readonly List<Particle> parts = new List<Particle>(); readonly Stack<Particle> partPool = new Stack<Particle>();
        readonly List<Flyer> flyers = new List<Flyer>(); readonly Stack<Flyer> flyerPool = new Stack<Flyer>();
        static readonly List<RaycastResult> hits = new List<RaycastResult>();

        float PlayHalfW => Mathf.Min(halfW, 4.4f);
        float SpawnY => halfH + 1.5f;
        public Vector3 PlanePos => plane.position;
        // Head start burns down by distance flown, not time.
        public float HeadStartFrac => headStart && headStartMiles > 0 ? Mathf.Clamp01((headStartMiles - miles) / headStartMiles) : 0f;
        public float HeadStartMilesLeft => headStart ? Mathf.Max(0f, headStartMiles - miles) : 0f;

        // =========================================================== setup
        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Save.Load(); Theme.SetNight(false);
            BuildCamera(); BuildWorld(); BuildPlane();
            gameObject.AddComponent<Sfx>().Init();
            gameObject.AddComponent<GameUI>().Build(this);
            ToMenu();
        }

        void BuildCamera()
        {
            var go = new GameObject("Camera"); go.tag = "MainCamera";
            cam = go.AddComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Theme.Get(Tok.SkyHorizon);
            cam.transform.position = new Vector3(0, 0, -10); cam.nearClipPlane = 0.1f; cam.farClipPlane = 50f;
            UpdateBounds();
        }

        void UpdateBounds() { halfH = cam.orthographicSize; halfW = halfH * cam.aspect; }

        SpriteRenderer SR(Transform parent, string name, Sprite s, int order, Color c)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.sortingOrder = order; sr.color = c; return sr;
        }

        void BuildWorld()
        {
            world = new GameObject("World").transform;
            skyBase = SR(world, "SkyBase", Art.White, -100, Theme.Get(Tok.SkyHorizon));
            skyTop = SR(world, "SkyTop", Art.Gradient, -99, Theme.Get(Tok.Sky));
            for (int i = 0; i < 40; i++)
            {
                var s = SR(world, "Star", Art.Circle, -95, new Color(1, 1, 1, 0));
                s.transform.position = new Vector3(Random.Range(-6f, 6f), Random.Range(-9f, 9f), 0); s.transform.localScale = Vector3.one * Random.Range(0.04f, 0.1f);
                stars.Add(s);
            }
            for (int i = 0; i < 7; i++)
            {
                var c = SR(world, "FarCloud", Art.Cloud, -90, Color.white);
                c.transform.position = new Vector3(Random.Range(-5f, 5f), Random.Range(-10f, 10f), 0); c.transform.localScale = Vector3.one * Random.Range(0.55f, 0.95f);
                c.flipX = Random.value < 0.5f; farClouds.Add(c);
            }
            runway = SR(world, "Runway", Art.Runway, -80, Color.white); runwayT = runway.transform;
            for (int i = 0; i < 4; i++)
            {
                var c = SR(world, "NearCloud", Art.Cloud, -70, Color.white);
                c.transform.position = new Vector3(Random.Range(-5f, 5f), Random.Range(-10f, 10f), 0); c.transform.localScale = Vector3.one * Random.Range(1.2f, 1.7f);
                c.flipX = Random.value < 0.5f; nearClouds.Add(c);
            }
            for (int i = 0; i < 12; i++)
            {
                var s = SR(world, "Streak", Art.Streak, -60, new Color(1, 1, 1, 0));
                s.transform.position = new Vector3(Random.Range(-5f, 5f), Random.Range(-10f, 10f), 0); s.transform.localScale = new Vector3(1, Random.Range(1.2f, 2.4f), 1);
                streaks.Add(s);
            }
        }

        void BuildPlane()
        {
            shadowSr = SR(null, "PlaneShadow", Art.Plane, 7, new Color(0, 0, 0, 0.3f)); planeShadow = shadowSr.transform;
            planeSr = SR(null, "Plane", Art.Plane, 10, Color.white); plane = planeSr.transform;
            shieldSr = SR(plane, "Shield", Art.Ring, 11, Color.white); shieldSr.transform.localScale = Vector3.one * 1.9f;
            magnetSr = SR(plane, "Magnet", Art.Ring, 9, Theme.Get(Tok.Signal, 0.6f)); magnetSr.transform.localScale = Vector3.one * 2.6f;
        }

        // =========================================================== flow
        public void ToMenu()
        {
            state = GState.Menu; Time.timeScale = 1f; ClearEnts(); Theme.SetNight(false);
            planeX = targetX = 0; planeVX = 0; planeAlt = 0; takeoffT = 0; crashT = 0; crashRot = 0; speed = 0; shieldT = magnetT = boostT = invulnT = 0; headStart = false;
            planeSr.enabled = true; shadowSr.enabled = true;
            runwayT.position = new Vector3(0, PlaneY + 8f - 16f, 0); runway.enabled = true;
            GameUI.I.ShowTitle();
        }

        public void StartRun()
        {
            ClearEnts(); Theme.SetNight(false);
            var d = Save.D; d.flightNo++; flightNo = d.flightNo;
            run = new Prog.RunStats();
            fuel = 1f; score = 0; combo = 0; multiplier = 1; miles = 0; travel = 0; zone = 0; speed = 0; takeoffT = 0; crashT = 0; crashRot = 0;
            shieldT = magnetT = boostT = invulnT = 0; lowFuelWarned = false; achTimer = 0;
            nextRowAt = 26f; nextFuelAt = 45f; nextPowerAt = Random.Range(90f, 130f);
            ResetDirector(); Missions.EnsureToday(); paidZone = d.maxZone;
            planeX = targetX = 0; planeVX = 0; dragging = false; planeAlt = 0;
            int hs = Prog.Lvl(Up.HeadStart); headStartMiles = Prog.HeadStartMiles(hs); headStart = hs > 0;
            planeSr.enabled = true; shadowSr.enabled = true; plane.rotation = Quaternion.identity;
            runwayT.position = new Vector3(0, PlaneY + 8f - 16f, 0); runway.enabled = true;
            state = GState.Playing; Time.timeScale = 1f;
            GameUI.I.OnRunStart();
            GameUI.I.Toast($"NOW BOARDING · FLIGHT FL-{flightNo}", $"Gate {Prog.Zones[0].gate} · {Prog.Zones[0].name}. Drag to steer.");
            Sfx.I.Power();
        }

        public void Pause(bool on)
        {
            if (on && state == GState.Playing) { state = GState.Paused; Time.timeScale = 0f; GameUI.I.ShowPause(true); }
            else if (!on && state == GState.Paused) { state = GState.Playing; Time.timeScale = 1f; GameUI.I.ShowPause(false); dragging = false; }
        }

        public void EndFromPause()
        {
            if (state != GState.Paused) return;
            Time.timeScale = 1f; GameUI.I.ShowPause(false);
            endReason = "abort"; state = GState.Over; planeSr.enabled = false; shadowSr.enabled = false;
            EndRun();
        }

        void OnApplicationPause(bool p) { if (p) Pause(true); }

        // =========================================================== loop
        void Update()
        {
            float dt = Time.deltaTime;
            UpdateBounds();
            var kb = Keyboard.current;
            if (state == GState.Paused && kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) { Pause(false); return; }

            switch (state)
            {
                case GState.Playing: Tick(dt); break;
                case GState.Crashing: TickCrash(dt); break;
                case GState.Over:
                    speed = Mathf.MoveTowards(speed, 1.2f, dt * 4f);
                    MoveEnts(dt, false);
#if UNITY_EDITOR
                    if (autopilot && (botWait -= Time.unscaledDeltaTime) <= 0) { StartRun(); Time.timeScale = botTimeScale; }
#endif
                    break;
                case GState.Menu: speed = 0f; break;
            }
            ScrollBackground(dt);
            UpdateParticles(dt);
            UpdateFlyers(dt);
            UpdatePlaneVisuals(dt);
            UpdateWeatherShared(dt);
            UpdateCamera(dt);
            Sfx.I.SetWind(state == GState.Playing ? 0.04f + speed * 0.006f + Mathf.Abs(windPush) * 0.01f : 0.03f);
        }

        // Camera drifts a little toward the plane and rolls with its turns, so the world never feels locked to a grid.
        void UpdateCamera(float dt)
        {
            float k = 1f - Mathf.Exp(-3f * dt);
            bool live = state == GState.Playing;
            camX = Mathf.Lerp(camX, live ? planeX * 0.12f : 0f, k);
            camY = Mathf.Lerp(camY, state == GState.Menu ? MenuCamY : 0f, 1f - Mathf.Exp(-2.4f * dt));
            camRoll = Mathf.Lerp(camRoll, live ? Mathf.Clamp(-planeVX * 0.22f, -2.2f, 2.2f) : 0f, k);
            shake = Mathf.MoveTowards(shake, 0, dt * 1.6f);
            var sh = shake > 0 ? Random.insideUnitCircle * shake * 0.5f : Vector2.zero;
            cam.transform.SetPositionAndRotation(new Vector3(camX + sh.x, camY + sh.y, -10), Quaternion.Euler(0, 0, camRoll));
        }

        void Tick(float dt)
        {
            takeoffT += dt;
            HandleInput(dt);
            float baseSp = CurTune().speed;
            bool burner = boostT > 0 || headStart;
            if (takeoffT < 1.6f) speed = baseSp * Mathf.Lerp(0.05f, 1f, Mathf.SmoothStep(0, 1, takeoffT / 1.6f));
            else speed = Mathf.MoveTowards(speed, baseSp * (burner ? 1.9f : 1f), dt * 7f);
            planeAlt = Mathf.Clamp01((takeoffT - 0.5f) / 1.2f);

            float dy = speed * dt; travel += dy;
            float dm = dy * 10f; miles += dm; run.miles = miles;
            score += dm * multiplier;

            if (headStart && miles >= headStartMiles) headStart = false;
            if (boostT > 0) boostT -= dt;
            if (shieldT > 0) shieldT -= dt;
            if (magnetT > 0) magnetT -= dt;
            if (invulnT > 0) invulnT -= dt;

            if (takeoffT > 1.6f && !burner) fuel -= Prog.BurnRate(Prog.Lvl(Up.Tank)) * dt;
            if (fuel < 0.25f && !lowFuelWarned) { lowFuelWarned = true; GameUI.I.Toast("FUEL LOW", "Fuel low. Grab a canister."); }
            if (fuel > 0.4f) lowFuelWarned = false;
            if (fuel <= 0f) { fuel = 0f; Crash("fuel"); return; }

            int z = Prog.ZoneIndex(miles);
            if (z > zone) { zone = z; run.zone = z; EnterZone(z); }

            PityFuel();
            while (travel >= nextRowAt) SpawnChunk();
            MoveEnts(dt, true);
            if (state != GState.Playing) return;

            if (runway.enabled) { runwayT.position += Vector3.down * dy; if (runwayT.position.y + 16f < -halfH - 1f) runway.enabled = false; }

            achTimer += dt;
            if (achTimer > 0.5f) { achTimer = 0; foreach (var a in Prog.Check(run)) { GameUI.I.AchToast(a); Sfx.I.Chime(); } ReportMissions(); }

            // contrails bend with the wind; afterburner flicker
            trailT += dt;
            var drift = new Vector2(windPush * 0.45f, 0);
            while (trailT > 0.016f)
            {
                trailT -= 0.016f;
                if (planeAlt > 0.4f)
                {
                    var c = Theme.Get(Tok.RunwayWhite, 0.4f * planeAlt);
                    Emit(plane.TransformPoint(new Vector3(-0.5f, -0.1f, 0)), drift, c, 0.7f, 0.12f, 0.42f, true, -50, 0.4f);
                    Emit(plane.TransformPoint(new Vector3(0.5f, -0.1f, 0)), drift, c, 0.7f, 0.12f, 0.42f, true, -50, 0.4f);
                }
                if (burner)
                {
                    Emit(plane.TransformPoint(new Vector3(0, -0.62f, 0)), new Vector2(Random.Range(-0.6f, 0.6f), -3f), Theme.Get(Tok.Signal), 0.25f, 0.28f, 0.05f, true, 9);
                    Emit(plane.TransformPoint(new Vector3(0, -0.62f, 0)), new Vector2(Random.Range(-0.6f, 0.6f), -2f), Theme.Get(Tok.HudCaution), 0.2f, 0.18f, 0.04f, true, 9);
                }
            }
        }

        void HandleInput(float dt)
        {
            float minX = -PlayHalfW + 0.6f, maxX = PlayHalfW - 0.6f;
            var ptr = Pointer.current;
            if (ptr != null)
            {
                Vector2 pos = ptr.position.ReadValue();
                if (ptr.press.wasPressedThisFrame && !OverUI(pos)) { dragging = true; dragPointer0 = WorldX(pos.x); dragPlane0 = targetX; }
                if (dragging && ptr.press.isPressed)
                {
                    targetX = dragPlane0 + (WorldX(pos.x) - dragPointer0) * 1.35f;
                    if (targetX < minX) { dragPlane0 += minX - targetX; targetX = minX; }
                    if (targetX > maxX) { dragPlane0 -= targetX - maxX; targetX = maxX; }
                }
                if (!ptr.press.isPressed) dragging = false;
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                float k = ((kb.rightArrowKey.isPressed || kb.dKey.isPressed) ? 1f : 0f) - ((kb.leftArrowKey.isPressed || kb.aKey.isPressed) ? 1f : 0f);
                targetX += k * 9f * dt;
                if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame) Pause(true);
            }
#if UNITY_EDITOR
            if (autopilot) { targetX = AutoX(); dragPlane0 = targetX; }
#endif
            float wp = windPush * dt; targetX += wp; dragPlane0 += wp;
            targetX = Mathf.Clamp(targetX, minX, maxX);

            // spring-damper steering, sub-stepped for stability at any frame rate
            float c = 2f * Mathf.Sqrt(SteerK) * SteerZeta;
            int n = Mathf.Max(1, Mathf.CeilToInt(dt / 0.008f)); float h = dt / n;
            for (int s = 0; s < n; s++)
            {
                float acc = SteerK * (targetX - planeX) - c * planeVX;
                planeVX += acc * h; planeX += planeVX * h;
            }
            planeX = Mathf.Clamp(planeX, minX - 0.25f, maxX + 0.25f);
        }

        float WorldX(float sx) => cam.ScreenToWorldPoint(new Vector3(sx, 0, 10)).x;

        bool OverUI(Vector2 pos)
        {
            var es = EventSystem.current; if (es == null) return false;
            var pe = new PointerEventData(es) { position = pos }; hits.Clear(); es.RaycastAll(pe, hits);
            return hits.Count > 0;
        }

        void EnterZone(int z)
        {
            var zn = Prog.Zones[z];
            string sub = $"Gate {zn.gate}. {zn.line}";
            if (z > paidZone)
            {
                int r = Prog.ArrivalReward(z); paidZone = z; Save.D.coins += r;
                sub = $"First arrival. +{Prog.N(r)} coins."; Burst(plane.position, Theme.Get(Tok.Signal), 16, 5f, 0.14f);
            }
            GameUI.I.Toast($"NOW ENTERING · {zn.name}", sub);
            Theme.SetNight(zn.night);
            Sfx.I.Chime();
        }

        // =========================================================== spawning (see GameLevel.cs for the director)
        Ent Spawn(Kind k, float x, float y)
        {
            if (!pool.TryGetValue(k, out var st)) pool[k] = st = new Stack<Ent>();
            var e = st.Count > 0 ? st.Pop() : Create(k);
            e.go.SetActive(true);
            e.r = e.r0; e.baseScale = e.scale0; e.sr.flipX = false;
            e.t.position = new Vector3(x, y, 0); e.t.localRotation = Quaternion.identity; e.t.localScale = Vector3.one * e.baseScale;
            e.baseX = x; e.vx = 0; e.phase = Random.value * 6.28f; e.passed = false; e.warned = false; e.struck = false; e.t2 = 0; e.h = 0;
            e.mode = 0; e.extent = 0; e.minD = 99f;
            e.minX = -PlayHalfW + 0.6f; e.maxX = PlayHalfW - 0.6f;
            switch (k)
            {
                case Kind.Storm: case Kind.Balloon: case Kind.Drone: e.sr.color = Theme.Get(Tok.Stop); break;
                case Kind.Fuel: e.sr.color = Theme.Get(Tok.HudGo); if (e.childSr) e.childSr.color = Theme.Get(Tok.HudGo, 0.45f); break;
                case Kind.Coin: e.sr.color = Color.white; e.phase = y * 0.9f; break; // phase from height: spin ripples along a trail
                default: e.sr.color = Color.white; break;
            }
            if (k == Kind.Storm && e.childSr) e.childSr.color = Theme.Get(Tok.InkInverse);
            InitWeather(e);
            e.chunk = curChunk;
            ents.Add(e);
            if (e.Hazard) rowHaz.Add(new Vector2(x, y));
            return e;
        }

        Ent Create(Kind k)
        {
            var e = new Ent { kind = k };
            e.go = new GameObject(k.ToString()); e.t = e.go.transform; e.t.SetParent(world, false);
            e.sr = e.go.AddComponent<SpriteRenderer>();
            switch (k)
            {
                case Kind.Coin: e.sr.sprite = Art.Coin; e.r = 0.42f; e.sr.sortingOrder = 2; break;
                case Kind.Fuel:
                    e.sr.sprite = Art.Fuel; e.r = 0.55f; e.sr.sortingOrder = 4;
                    e.childSr = SR(e.t, "Glow", Art.Soft, 3, Color.white); e.child = e.childSr.transform; e.child.localScale = Vector3.one * 2.2f; break;
                case Kind.Storm:
                    e.sr.sprite = Art.Storm; e.r = 0.55f; e.sr.sortingOrder = 5; e.baseScale = 0.85f;
                    e.childSr = SR(e.t, "Bolt", Art.Bolt, 6, Color.white); e.child = e.childSr.transform; e.child.localPosition = new Vector3(0.05f, -0.1f, 0); break;
                case Kind.Balloon: e.sr.sprite = Art.Balloon; e.r = 0.42f; e.sr.sortingOrder = 5; break;
                case Kind.Drone: e.sr.sprite = Art.Drone; e.r = 0.46f; e.sr.sortingOrder = 5; break;
                case Kind.Lightning: case Kind.Arc: case Kind.Wind: case Kind.Gate: CreateWeather(e); break;
                default:
                    e.sr.sprite = Art.Picto(k == Kind.Shield ? "shield" : k == Kind.Magnet ? "magnet" : "boost"); e.r = 0.55f; e.sr.sortingOrder = 4;
                    e.baseScale = 0.75f;
                    e.childSr = SR(e.t, "Glow", Art.Soft, 3, Theme.Get(Tok.Signal, 0.55f)); e.child = e.childSr.transform; e.child.localScale = Vector3.one * 2.4f; break;
            }
            e.r0 = e.r; e.scale0 = e.baseScale;
            return e;
        }

        void Despawn(int i)
        {
            var e = ents[i]; ents.RemoveAt(i); e.go.SetActive(false); pool[e.kind].Push(e);
        }

        void ClearEnts() { for (int i = ents.Count - 1; i >= 0; i--) Despawn(i); }

        // =========================================================== movement + collision
        void MoveEnts(float dt, bool collide)
        {
            float dy = speed * dt, time = Time.time;
            var pp = new Vector2(planeX, PlaneY);
            float magR = magnetT > 0 ? 4.2f : 0f;
            if (collide) windPush = 0f;
            for (int i = ents.Count - 1; i >= 0; i--)
            {
                if (i >= ents.Count) continue;
                var e = ents[i]; var p = e.t.position; p.y -= dy;
                switch (e.kind)
                {
                    case Kind.Balloon:
                        if (e.vx != 0) { e.baseX += e.vx * dt; if ((e.baseX < e.minX && e.vx < 0) || (e.baseX > e.maxX && e.vx > 0)) e.vx = -e.vx; }
                        p.x = e.baseX + Mathf.Sin(time * 1.6f + e.phase) * 0.3f;
                        e.t.localRotation = Quaternion.Euler(0, 0, Mathf.Cos(time * 1.6f + e.phase) * 6f); break;
                    case Kind.Drone:
                        p.x += e.vx * dt;
                        if ((p.x < e.minX && e.vx < 0) || (p.x > e.maxX && e.vx > 0)) e.vx = -e.vx;
                        e.t.localRotation = Quaternion.Euler(0, 0, -e.vx * 5f + Mathf.Sin(time * 3f + e.phase) * 6f); break;
                    case Kind.Coin:
                        e.t.localScale = new Vector3(Mathf.Abs(Mathf.Cos(time * 3f + e.phase)) * 0.8f + 0.2f, 1, 1);
                        if (magR > 0 && collide)
                        {
                            var to = pp - (Vector2)p; float m = to.magnitude;
                            if (m < magR) { var sw = new Vector2(-to.y, to.x) / Mathf.Max(m, 0.001f); p += (Vector3)((to / Mathf.Max(m, 0.001f) + sw * 0.6f) * Mathf.Min(m, 16f * dt)); } // spiral in
                        }
                        break;
                    case Kind.Storm:
                        // storms breathe and drift; the bolt glyph flickers
                        e.t.localScale = Vector3.one * e.baseScale * (1f + Mathf.Sin(time * 1.1f + e.phase) * 0.035f);
                        p.x = e.baseX + Mathf.Sin(time * 0.45f + e.phase) * 0.06f;
                        if (e.childSr) e.childSr.enabled = Mathf.Repeat(time * 1.3f + e.phase, 1f) < 0.1f;
                        break;
                    case Kind.Lightning: UpdateCell(e, p, dt, collide); break;
                    case Kind.Arc: UpdateArc(e, p, dt, collide); break;
                    case Kind.Wind: UpdateWind(e, p, dt, collide); break;
                    case Kind.Gate: UpdateGate(e, time); break;
                    default: e.t.localScale = Vector3.one * e.baseScale * (1f + Mathf.Sin(time * 5f + e.phase) * 0.06f); break;
                }
                e.t.position = p;

                if (collide && state == GState.Playing)
                {
                    if (e.r > 0f && Vector2.Distance(p, pp) < e.r + PlaneR)
                    {
                        if (Hit(e)) { Despawn(i); continue; }
                        if (state != GState.Playing) return;
                    }
                    if (e.Hazard && !e.Custom && !e.passed && p.y < PlaneY)
                    {
                        e.passed = true;
                        if (Mathf.Abs(p.x - planeX) < e.r + PlaneR + 0.75f) NearMiss(e);
                    }
                }
                if (p.y < -halfH - 2f - e.extent)
                {
                    if (e.kind == Kind.Coin && collide) { combo = Mathf.Max(0, combo - 3); UpdateMult(); }
                    Despawn(i);
                }
            }
        }

        void UpdateMult()
        {
            int m = Mathf.Clamp(1 + (int)(combo / 12f), 1, 5);
            if (m > multiplier) Pop($"x{m} MULTIPLIER", Tok.Signal);
            if (m != multiplier) GameUI.I.PunchMult();
            multiplier = m;
        }

        bool Hit(Ent e)
        {
            var pos = e.t.position;
            switch (e.kind)
            {
                case Kind.Coin:
                    run.coins++; combo += 1; Missions.Add(MType.Coins, 1); UpdateMult(); score += 10 * multiplier;
                    Sfx.I.Coin(multiplier); Burst(pos, Theme.Get(Tok.Signal), 4, 2f, 0.09f); FlyCoin(pos);
                    return true;
                case Kind.Fuel:
                    if (fuel < 0.1f) run.fumes++;
                    Missions.Add(MType.Canisters, 1);
                    fuel = Mathf.Min(1f, fuel + 0.45f); Sfx.I.Fuel(); Burst(pos, Theme.Get(Tok.HudGo), 10, 3f, 0.14f); Pop("FUEL +45%", Tok.HudGo);
                    return true;
                case Kind.Shield:
                    shieldMax = shieldT = Prog.ShieldTime(Prog.Lvl(Up.Shield)); run.powerups++; Missions.Add(MType.Powerups, 1); Sfx.I.Power(); Pop("SHIELD ON", Tok.InkInverse); return true;
                case Kind.Magnet:
                    magnetMax = magnetT = Prog.MagnetTime(Prog.Lvl(Up.Magnet)); run.powerups++; Missions.Add(MType.Powerups, 1); Sfx.I.Power(); Pop("MAGNET ON", Tok.InkInverse); return true;
                case Kind.Boost:
                    boostMax = boostT = 3.5f; run.powerups++; Missions.Add(MType.Powerups, 1); Sfx.I.Power(); Pop("AFTERBURNER", Tok.Signal); shake = 0.15f; return true;
                default:
                    return HazardContact(e);
            }
        }

        // area = electric hit (no cloud to smash). Returns true when the hazard should be removed.
        bool HazardContact(Ent e, bool area = false)
        {
            if (boostT > 0 || headStart || takeoffT < 2f) { if (!area) { Smash(e); score += 25 * multiplier; } return !area; }
            if (invulnT > 0) return false;
            if (shieldT > 0)
            {
                shieldT = 0; invulnT = 1f; run.shieldBlocks++; Missions.Add(MType.ShieldBlocks, 1);
                if (!area) Smash(e); Pop("SHIELD HIT", Tok.HudCaution); shake = 0.3f; return !area;
            }
            killer = $"{e.kind}@{e.chunk} px={planeX:0.00} hx={e.t.position.x:0.00} hy={e.t.position.y - PlaneY:0.00} r={e.r:0.00} tx={targetX:0.00} vx={planeVX:0.0}";
            Crash("crash");
            return false;
        }

        void ReportMissions()
        {
            Missions.Max(MType.MilesRun, (int)miles);
            Missions.Max(MType.ScoreRun, (int)score);
            Missions.Max(MType.CoinsRun, run.coins);
            Missions.Max(MType.NearRun, run.nearMiss);
            Missions.Max(MType.ReachZone, zone);
            int m = (int)miles; Missions.Add(MType.MilesTotal, m - lastMilesReported); lastMilesReported = m;
        }

        void NearMiss(Ent e)
        {
            run.nearMiss++; combo += 4; UpdateMult(); Missions.Add(MType.CloseCalls, 1);
            int pts = 50 * multiplier; score += pts;
            Pop($"CLOSE CALL +{pts}", Tok.Signal); Sfx.I.Whoosh();
        }

        void Smash(Ent e)
        {
            Sfx.I.Smash();
            Burst(e.t.position, Theme.Get(Tok.Stop), 14, 5f, 0.22f);
            Burst(e.t.position, Theme.Get(Tok.Cloud), 8, 3f, 0.4f);
        }

        void Crash(string reason)
        {
            if (state != GState.Playing) return;
            endReason = reason; state = GState.Crashing; crashT = 0; crashRot = plane.eulerAngles.z;
            if (reason == "crash")
            {
                Sfx.I.Crash(); shake = 0.7f;
                Burst(plane.position, Theme.Get(Tok.Stop), 26, 7f, 0.32f);
                Burst(plane.position, Theme.Get(Tok.HudCaution), 14, 5f, 0.25f);
                Burst(plane.position, Color.white, 12, 3.5f, 0.55f);
#if UNITY_ANDROID || UNITY_IOS
                if (Save.D.haptics) Handheld.Vibrate();
#endif
            }
            else Sfx.I.Sputter();
            GameUI.I.OnCrash();
        }

        void TickCrash(float dt)
        {
            crashT += dt;
            speed = Mathf.MoveTowards(speed, 0, dt * (endReason == "crash" ? 12f : 4f));
            MoveEnts(dt, false);
            if (runway.enabled) runwayT.position += Vector3.down * speed * dt;
            if (endReason == "crash" && crashT < 0.6f && Random.value < 0.5f) Emit(plane.position, Random.insideUnitCircle * 0.5f, new Color(0.3f, 0.3f, 0.3f, 0.6f), 0.8f, 0.2f, 0.6f, true, 8);
            if (crashT > 1.4f) { state = GState.Over; planeSr.enabled = false; shadowSr.enabled = false; EndRun(); }
        }

        void EndRun()
        {
            var d = Save.D;
            int earned = Mathf.RoundToInt(run.coins * Prog.CoinMult(Prog.Lvl(Up.Coin)));
            run.score = Mathf.RoundToInt(score);
            bool best = run.score > d.bestScore && run.score > 0;
            d.coins += earned; d.totalCoins += earned; d.totalMiles += (long)miles; d.runs++;
            if (best) d.bestScore = run.score;
            d.bestMiles = Mathf.Max(d.bestMiles, (int)miles); d.maxZone = Mathf.Max(d.maxZone, zone);
            int xp = Mathf.RoundToInt(miles / 10f) + run.nearMiss * 5 + run.coins;
            var ups = new List<int>(); Prog.AddXp(xp, ups);
            if (d.recentMiles == null) d.recentMiles = new List<int>();
            d.recentMiles.Add((int)miles); while (d.recentMiles.Count > 5) d.recentMiles.RemoveAt(0);
            if (endReason != "abort") { ReportMissions(); Missions.Add(MType.Flights, 1); }
            var achs = Prog.Check(run);
            Save.Write();
#if UNITY_EDITOR
            if (autopilot) { BotLog.Add($"{(int)miles},{zone},{endReason},{(endReason == "crash" ? killer : "-")},{run.coins},{run.nearMiss}"); botWait = 0.3f; }
#endif
            GameUI.I.ShowGameOver(new RunResult
            {
                score = run.score, coins = run.coins, earned = earned, near = run.nearMiss, zone = zone, xp = xp, flight = flightNo,
                level = d.level, levelUps = ups.Count, miles = miles, best = best, reason = endReason
            });
            foreach (var l in ups) { GameUI.I.Toast($"PROMOTED · LV {l}", $"{Prog.Rank(l)}. +{Prog.LevelReward(l)} coins."); }
            foreach (var a in achs) GameUI.I.AchToast(a);
            if (ups.Count > 0 || achs.Count > 0) Sfx.I.Chime();
        }

        // =========================================================== visuals
        void ScrollBackground(float dt)
        {
            float sp = state == GState.Menu ? 1.2f : Mathf.Max(speed, 0.8f);
            float w = halfW + 3f;
            skyBase.transform.position = new Vector3(camX, camY, 0); skyBase.transform.localScale = new Vector3(halfW * 2 + 6, halfH * 2 + 6, 1);
            skyTop.transform.position = new Vector3(camX, camY, 0); skyTop.transform.localScale = new Vector3((halfW * 2 + 6) * 64f, halfH * 2 + 3, 1);
            float k = 1f - Mathf.Exp(-dt * 1.5f);
            skyBase.color = Color.Lerp(skyBase.color, Theme.Get(Tok.SkyHorizon), k);
            skyTop.color = Color.Lerp(skyTop.color, Theme.Get(Tok.Sky), k);
            cam.backgroundColor = skyBase.color;
            nightK = Mathf.MoveTowards(nightK, Theme.Night ? 1f : 0f, dt * 0.7f);
            var cloudC = Color.Lerp(Theme.Get(Tok.Cloud, false), Theme.Get(Tok.Cloud, true), nightK);

            Wrap(farClouds, sp * 0.3f * dt, w, 2f, cloudC, 0.75f, 0.12f, dt);
            Wrap(nearClouds, sp * 0.62f * dt, w, 3f, cloudC, 0.9f, 0.25f, dt);
            float sa = Mathf.Clamp01((speed - 7.5f) / 6f) * 0.28f + (boostT > 0 || headStart ? 0.25f : 0f);
            Wrap(streaks, sp * 1.8f * dt, w, 2f, Theme.Get(Tok.RunwayWhite, sa), 1f, 0f, dt);
            var lean = Quaternion.Euler(0, 0, Mathf.Clamp(planeVX * 1.4f - windPush * 2f, -14f, 14f)); // streaks lean with motion
            foreach (var s in streaks) s.transform.rotation = Quaternion.Slerp(s.transform.rotation, lean, 1f - Mathf.Exp(-6f * dt));
            for (int i = 0; i < stars.Count; i++)
            {
                var s = stars[i]; var p = s.transform.position; p.y -= sp * 0.04f * dt;
                if (p.y < camY - halfH - 1) { p.y = camY + halfH + 1; p.x = Random.Range(-w, w); }
                s.transform.position = p;
                s.color = new Color(1, 1, 1, nightK * (0.5f + 0.5f * Mathf.Sin(Time.time * 2f + i)));
            }
        }

        void Wrap(List<SpriteRenderer> list, float dy, float w, float margin, Color c, float a, float drift, float dt)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                var p = s.transform.position; p.y -= dy;
                p.x += (Mathf.Sin(Time.time * 0.13f + i * 1.7f) * drift + windPush * drift * 0.3f) * dt; // clouds wander sideways
                if (p.y < camY - halfH - margin - 1f) { p.y = camY + halfH + margin + Random.Range(0f, 3f); p.x = Random.Range(-w, w); }
                s.transform.position = p;
                var cc = c; cc.a *= a; s.color = cc;
            }
        }

        void UpdatePlaneVisuals(float dt)
        {
            float t = Time.time;
            if (state == GState.Crashing)
            {
                if (endReason == "crash")
                {
                    crashRot += 620f * dt;
                    plane.rotation = Quaternion.Euler(0, 0, crashRot);
                    plane.position = new Vector3(planeX, PlaneY - crashT * crashT * 3f, 0);
                    plane.localScale = Vector3.one * Mathf.Max(0.2f, 1f - crashT * 0.55f);
                }
                else
                {
                    plane.rotation = Quaternion.Euler(0, 0, Mathf.Sin(crashT * 3f) * 12f);
                    plane.position = new Vector3(planeX, PlaneY - crashT * 1.6f, 0);
                    plane.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, crashT / 1.4f);
                }
                planeSr.color = Color.white;
                shieldSr.enabled = magnetSr.enabled = false;
                shadowSr.color = new Color(0, 0, 0, 0);
                return;
            }

            // nose follows the flight path: heading from lateral speed vs forward speed
            float fwd = Mathf.Max(speed, 4f) * 1.1f;
            float heading = -Mathf.Atan2(planeVX, fwd) * Mathf.Rad2Deg * 1.15f;
            planeTilt = Mathf.Lerp(planeTilt, Mathf.Clamp(heading, -38f, 38f), 1f - Mathf.Exp(-14f * dt));
            plane.rotation = Quaternion.Euler(0, 0, planeTilt);
            bool burner = boostT > 0 || headStart;
            surge = Mathf.Lerp(surge, burner ? 0.35f : 0f, 1f - Mathf.Exp(-3f * dt));
            float sc = Mathf.Lerp(0.82f, 1f, planeAlt) * (burner ? 1.06f : 1f);
            float roll = Mathf.Lerp(1f, 0.8f, Mathf.Abs(planeTilt) / 38f); // banking squashes the wings
            plane.localScale = new Vector3(sc * roll, sc, 1);
            float bob = state == GState.Playing ? (Mathf.Sin(t * 2.2f) * 0.06f + Mathf.Sin(t * 0.9f) * 0.04f) * planeAlt : 0f;
            plane.position = new Vector3(planeX, PlaneY + bob + surge, 0);
            planeSr.color = invulnT > 0 && Mathf.Repeat(t * 10f, 1f) < 0.5f ? new Color(1, 1, 1, 0.45f) : Color.white;

            planeShadow.position = plane.position + new Vector3(0.12f + 0.7f * planeAlt, -0.18f - 1.0f * planeAlt, 0);
            planeShadow.rotation = plane.rotation; planeShadow.localScale = plane.localScale * (1f - 0.25f * planeAlt);
            shadowSr.color = new Color(0, 0, 0, 0.3f * (1f - planeAlt));

            bool sh = shieldT > 0 || burner;
            shieldSr.enabled = sh && (shieldT > 1.5f || burner || Mathf.Repeat(t * 6f, 1f) < 0.6f);
            shieldSr.color = burner ? Theme.Get(Tok.Signal, 0.85f) : Theme.Get(Tok.RunwayWhite, 0.9f);
            shieldSr.transform.localScale = new Vector3(1.9f + Mathf.Sin(t * 6f) * 0.06f, 1.9f + Mathf.Cos(t * 5f) * 0.06f, 1);
            magnetSr.enabled = magnetT > 0 && (magnetT > 1.5f || Mathf.Repeat(t * 6f, 1f) < 0.6f);
            magnetSr.transform.localScale = Vector3.one * (2.4f + Mathf.Repeat(t * 1.5f, 1f) * 1.2f);
            magnetSr.color = Theme.Get(Tok.Signal, 0.6f * (1f - Mathf.Repeat(t * 1.5f, 1f)));
        }

        // =========================================================== particles
        void Emit(Vector3 pos, Vector2 v, Color c, float life, float s0, float s1, bool scroll, int order = 20, float drag = 2.5f)
        {
            Particle p;
            if (partPool.Count > 0) p = partPool.Pop();
            else { p = new Particle(); p.sr = SR(world, "Fx", Art.Circle, order, c); p.t = p.sr.transform; }
            p.t.gameObject.SetActive(true); p.t.position = pos; p.v = v; p.c = c; p.life = 0; p.max = life; p.s0 = s0; p.s1 = s1; p.scroll = scroll; p.drag = drag;
            p.sr.sortingOrder = order; p.t.localScale = Vector3.one * s0; p.sr.color = c;
            parts.Add(p);
        }

        void Burst(Vector3 pos, Color c, int n, float spd, float size)
        {
            for (int i = 0; i < n; i++)
                Emit(pos, Random.insideUnitCircle.normalized * Random.Range(0.3f, 1f) * spd, c, Random.Range(0.35f, 0.7f), size, size * 0.3f, true, 20);
        }

        void UpdateParticles(float dt)
        {
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i]; p.life += dt;
                if (p.life >= p.max) { p.t.gameObject.SetActive(false); partPool.Push(p); parts.RemoveAt(i); continue; }
                float k = p.life / p.max;
                var pos = p.t.position + (Vector3)(p.v * dt);
                if (p.scroll) pos.y -= speed * dt;
                p.v *= Mathf.Max(0, 1f - p.drag * dt);
                p.t.position = pos;
                p.t.localScale = Vector3.one * Mathf.Lerp(p.s0, p.s1, k);
                var c = p.c; c.a *= 1f - k * k; p.sr.color = c;
            }
        }

        // Collected coins curve up to the HUD counter.
        void FlyCoin(Vector3 from)
        {
            Flyer f;
            if (flyerPool.Count > 0) f = flyerPool.Pop();
            else { f = new Flyer(); f.sr = SR(world, "FlyCoin", Art.Coin, 30, Color.white); f.t = f.sr.transform; }
            f.t.gameObject.SetActive(true); f.a = from; f.k = 0;
            f.c = from + new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(1.2f, 2.4f), 0);
            f.t.position = from; flyers.Add(f);
        }

        void UpdateFlyers(float dt)
        {
            if (flyers.Count == 0) return;
            var target = cam.ScreenToWorldPoint(new Vector3(GameUI.I.CoinTargetScreen.x, GameUI.I.CoinTargetScreen.y, 10)); target.z = 0;
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i]; f.k += Time.unscaledDeltaTime / 0.5f;
                if (f.k >= 1f) { f.t.gameObject.SetActive(false); flyerPool.Push(f); flyers.RemoveAt(i); GameUI.I.PunchCoins(); continue; }
                float e = f.k * f.k * (3f - 2f * f.k);
                f.a.y -= speed * dt * (1f - e);
                var p = (1 - e) * (1 - e) * f.a + 2 * (1 - e) * e * f.c + e * e * target;
                f.t.position = p;
                f.t.localScale = new Vector3(Mathf.Abs(Mathf.Cos(f.k * 12f)) * 0.7f + 0.3f, 1, 1) * Mathf.Lerp(1f, 0.55f, e);
            }
        }

#if UNITY_EDITOR
        // ---------- Editor-only autopilot for balance testing ----------
        [Header("Editor balance test")] public bool autopilot; public float botLookahead = 6f; public float botTimeScale = 3f;
        public static readonly List<string> BotLog = new List<string>();
        float botWait;

        public void StartBot(bool on, float timeScale = 3f)
        {
            autopilot = on; botTimeScale = timeScale; Save.Suspend = on;
            if (on) { Application.runInBackground = true; BotLog.Clear(); StartRun(); Time.timeScale = timeScale; }
            else { Time.timeScale = 1f; Save.Load(); ToMenu(); }
        }

        float AutoX()
        {
            float best = planeX, bestS = float.MaxValue;
            for (float x = -PlayHalfW + 0.6f; x <= PlayHalfW - 0.6f; x += 0.2f)
            {
                float sc = Mathf.Abs(x - planeX) * 0.15f;
                foreach (var e in ents)
                {
                    float dy = e.t.position.y - PlaneY;
                    if (dy < -0.6f - e.extent || dy > botLookahead + e.extent) continue;
                    float dx = Mathf.Abs(e.t.position.x - x);
                    if (e.Hazard)
                    {
                        HazardSpan(e, false, out float lo, out float hi);
                        lo -= PlaneR + 0.3f; hi += PlaneR + 0.3f;
                        if (x > lo && x < hi) sc += 100f / (Mathf.Max(dy, 0f) + 1f);
                        // don't plan a route that sweeps through a hazard that is about to pass
                        float a = Mathf.Min(x, planeX), b = Mathf.Max(x, planeX);
                        if (dy < 1.8f && b > lo && a < hi) sc += 400f;
                    }
                    else if (e.kind == Kind.Coin) { if (dx < 0.4f) sc -= 2f / (dy + 1f); }
                    else if (e.kind == Kind.Fuel) { if (dx < 0.5f) sc -= (fuel < 0.5f ? 40f : 6f) / (dy + 1f); }
                    else if (e.kind >= Kind.Shield && e.kind <= Kind.Boost) { if (dx < 0.5f) sc -= 6f / (dy + 1f); }
                }
                if (sc < bestS) { bestS = sc; best = x; }
            }
            return best;
        }
#endif

        void Pop(string text, Tok tok) => GameUI.I.Pop(plane.position + new Vector3(0, 1.1f, 0), text, tok);
    }
}

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Flightline.EditorTools
{
    // Flightline > Store Screenshots: enters play mode, flies the autopilot and captures clean store screenshots
    // (iOS 1284x2778 and Android 1080x2160) of the home screen, three zones and the boarding pass.
    // Saves are suspended the whole time, so your progress is untouched.
    public static class StoreShots
    {
        const string Root = "Builds/Screenshots";
        static readonly (string name, float miles)[] Zones = { ("02_jet_stream", 2600f), ("03_storm_front", 5800f), ("04_aurora", 9800f) };
        static readonly HashSet<string> done = new HashSet<string>();
        static double overAt, startAt;
        static bool running;

        [MenuItem("Flightline/Store Screenshots (iOS + Android)")]
        public static void Run()
        {
            Directory.CreateDirectory(Root + "/ios"); Directory.CreateDirectory(Root + "/android");
            done.Clear(); overAt = 0; running = true; startAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }

        static void Shot(string name)
        {
            var t = GameObject.Find("Toasts"); var p = GameObject.Find("Pops");
            if (t) t.SetActive(false); if (p) p.SetActive(false);
            FlightlineMenu.Capture($"{Root}/ios/{name}.png", 1284, 2778);
            FlightlineMenu.Capture($"{Root}/android/{name}.png", 1080, 2160);
            if (t) t.SetActive(true); if (p) p.SetActive(true);
            done.Add(name); Debug.Log("Flightline store shot: " + name);
        }

        static void Tick()
        {
            if (!running) return;
            if (!EditorApplication.isPlaying || Game.I == null) return;
            var g = Game.I;
            if (EditorApplication.timeSinceStartup - startAt < 3) return; // let the menu settle
            Application.runInBackground = true; Save.Suspend = true;

            if (!done.Contains("01_home"))
            {
                if (g.state != GState.Menu) return;
                Shot("01_home"); g.StartBot(true, 3f); return;
            }

            if (g.state == GState.Playing)
            {
                g.fuel = Mathf.Max(g.fuel, 0.8f); // no FUEL LOW warnings in the shots
                float speedUp = 3f;
                foreach (var (name, miles) in Zones)
                {
                    if (done.Contains(name)) continue;
                    if (g.miles >= miles - 600f) speedUp = 1f; // real time just before a shot, so the HUD isn't mid-bounce
                    if (g.miles >= miles) Shot(name);
                    break;
                }
                Time.timeScale = speedUp; g.botTimeScale = speedUp;
                bool allZones = true; foreach (var z in Zones) allZones &= done.Contains(z.name);
                g.autopilot = !allZones; // after the zone shots, let the run end for the boarding pass
                return;
            }

            if (g.state == GState.Crashing) { g.autopilot = false; return; }

            if (g.state == GState.Over)
            {
                if (overAt == 0) overAt = EditorApplication.timeSinceStartup;
                bool allZones = true; foreach (var z in Zones) allZones &= done.Contains(z.name);
                if (EditorApplication.timeSinceStartup - overAt < 2.5) return; // score flap + review delay settle
                if (allZones && !done.Contains("05_boarding_pass")) Shot("05_boarding_pass");
                overAt = 0;
                if (done.Contains("05_boarding_pass")) { Finish(); return; }
                g.StartBot(true, 3f); // crashed before the zones: fly again
            }
        }

        static void Finish()
        {
            running = false; EditorApplication.update -= Tick;
            Game.I.StartBot(false);
            Debug.Log($"Flightline store shots done: {Path.GetFullPath(Root)}");
        }
    }
}

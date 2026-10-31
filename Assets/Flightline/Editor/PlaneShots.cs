using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Flightline.EditorTools
{
    // Flightline > Plane Preview Shots: enters play mode and, for each plane with imported pixel art, captures a still on
    // the runway and a short burst of in-flight frames (Builds/PlaneShots/<art>/). Saves are suspended, so progress is untouched.
    public static class PlaneShots
    {
        const string Root = "Builds/PlaneShots";
        const int Frames = 24; const float FrameDt = 1f / 12f, FlyFor = 5f;
        static int idx, frame; static float phaseAt, nextAt; static double startAt;
        static bool running, quitAfter, flying;

        [MenuItem("Flightline/Plane Preview Shots")]
        public static void Run()
        {
            idx = 0; frame = 0; flying = false; phaseAt = -1f; running = true; startAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }

        // Command line: Unity -projectPath . -executeMethod Flightline.EditorTools.PlaneShots.RunAndQuit
        public static void RunAndQuit()
        {
            quitAfter = true;
            EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
            Run();
        }

        static int NextArtPlane(int from)
        {
            for (int i = from; i < Prog.Planes.Length; i++) if (!string.IsNullOrEmpty(Prog.Planes[i].art)) return i;
            return -1;
        }

        static void Tick()
        {
            if (!running || !EditorApplication.isPlaying || Game.I == null) return;
            if (EditorApplication.timeSinceStartup - startAt < 3) return; // let the menu settle
            var g = Game.I;
            Application.runInBackground = true; Save.Suspend = true;
            idx = NextArtPlane(idx);
            if (idx < 0) { Finish(); return; }
            var art = Prog.Planes[idx].art; var dir = $"{Root}/{art}";

            if (!flying)
            {
                if (g.state != GState.Menu) return;
                if (phaseAt < 0) { Save.D.plane = idx; g.ApplyPlane(); phaseAt = Time.realtimeSinceStartup; return; }
                if (Time.realtimeSinceStartup - phaseAt < 1f) return;
                Directory.CreateDirectory(dir);
                FlightlineMenu.Capture($"{dir}/runway.png");
                g.StartBot(true, 1f); Save.D.plane = idx; flying = true; phaseAt = Time.time; nextAt = phaseAt + FlyFor; frame = 0;
                return;
            }

            if (g.state != GState.Playing) return;
            g.fuel = Mathf.Max(g.fuel, 0.8f);
            if (Time.time < nextAt) return;
            FlightlineMenu.Capture($"{dir}/f{frame:00}.png");
            nextAt = Time.time + FrameDt;
            if (++frame < Frames) return;
            Debug.Log("Flightline plane shots: " + art);
            g.StartBot(false); flying = false; phaseAt = -1f; idx++;
        }

        static void Finish()
        {
            running = false; EditorApplication.update -= Tick;
            Debug.Log($"Flightline plane shots done: {Path.GetFullPath(Root)}");
            if (quitAfter) { EditorApplication.isPlaying = false; EditorApplication.delayCall += () => EditorApplication.Exit(0); }
        }
    }
}

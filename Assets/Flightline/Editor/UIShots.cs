using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Flightline.EditorTools
{
    // Flightline > UI Screenshots: plays through every screen (home, hangar, missions, logbook, flight HUD, pause,
    // keep-flying card, game over) and saves a phone-sized PNG of each to Builds/UIShots. Saves are suspended.
    public static class UIShots
    {
        const string Root = "Builds/UIShots";
        static int step; static double at; static bool running, quitAfter;

        [MenuItem("Flightline/UI Screenshots")]
        public static void Run()
        {
            step = 0; at = EditorApplication.timeSinceStartup + 4; running = true;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }

        // Command line: Unity -projectPath . -executeMethod Flightline.EditorTools.UIShots.RunAndQuit
        public static void RunAndQuit()
        {
            quitAfter = true;
            EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
            Run();
        }

        static object Field(string name) => typeof(GameUI).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(GameUI.I);
        static void Go(string screen, int dir) => typeof(GameUI).GetMethod("Go").Invoke(GameUI.I, new[] { Field(screen), dir });
        static void Shot(string name) { Directory.CreateDirectory(Root); FlightlineMenu.Capture($"{Root}/{name}.png"); Debug.Log("Flightline UI shot: " + name); }

        // Each step: an action, then the wait before the next step (time for slides, fades and the flap counters).
        static readonly (Action act, double wait)[] Steps =
        {
            (() => Shot("1_home"), 0.2),
            (() => Go("scrHangar", 1), 1.2), (() => Shot("2_hangar"), 0.2),
            (() => Go("scrMissions", 1), 1.2), (() => Shot("3_missions"), 0.2),
            (() => Go("scrLog", 1), 1.2), (() => Shot("4_logbook"), 0.2),
            (() => { Go("scrTitle", -1); Game.I.StartRun(); }, 5), (() => Shot("5_hud"), 0.2),
            (() => Game.I.Pause(true), 0.8), (() => Shot("6_pause"), 0.2),
            (() => { Game.I.Pause(false); GameUI.I.ShowRevive(); }, 0.8), (() => Shot("7_revive"), 0.2),
            (() => { ((RectTransform)Field("scrRevive")).gameObject.SetActive(false); Game.I.Pause(true); Game.I.EndFromPause(); }, 2), (() => Shot("8_gameover"), 0.2),
        };

        static void Tick()
        {
            if (!running || !EditorApplication.isPlaying || Game.I == null || GameUI.I == null) return;
            Application.runInBackground = true; Save.Suspend = true;
            if (EditorApplication.timeSinceStartup < at) return;
            if (step >= Steps.Length) { Finish(); return; }
            try { Steps[step].act(); }
            catch (Exception e) { Debug.LogError($"Flightline UI shots, step {step}: {e}"); }
            at = EditorApplication.timeSinceStartup + Steps[step].wait; step++;
        }

        static void Finish()
        {
            running = false; EditorApplication.update -= Tick;
            Debug.Log($"Flightline UI shots done: {Path.GetFullPath(Root)}");
            if (quitAfter) { EditorApplication.isPlaying = false; EditorApplication.delayCall += () => EditorApplication.Exit(0); }
        }
    }
}

using System;
using System.Collections;
using UnityEngine;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using MalumMenu.features;

namespace MalumMenu
{
    internal class SelfTab : ITab
    {
        private const int HandlingId = 60017;
        public string name => "Self";

        private uint level = 0;

        public void Draw()
        {
            try
            {
                GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));
            if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null)
            {
                GUILayout.Label("You are not currently in a game, these options will not work.");
            }
            else
            {
                GUILayout.Label($"Role: {PlayerControl.LocalPlayer.Data.RoleType}");
            }

            // Self.BypassIntentionalDisconnectionBlocks.Enabled = GUILayout.Toggle(Self.BypassIntentionalDisconnectionBlocks.Enabled, "Bypass intentional disconnection temp bans");
            Self.UpdateStatsFreeplay.Enabled = GUILayout.Toggle(Self.UpdateStatsFreeplay.Enabled, "Update Stats in Freeplay");
            Immortality.Enabled = GUILayout.Toggle(Immortality.Enabled, "Become Immortal");
            Self.AlwaysShowTaskAnimations = GUILayout.Toggle(Self.AlwaysShowTaskAnimations, "Always Show Task Animations");
            Self.NoLadderCooldown.Enabled = GUILayout.Toggle(Self.NoLadderCooldown.Enabled, "No Ladder Cooldown");
            Self.UnlimitedMeetings.enabled = GUILayout.Toggle(Self.UnlimitedMeetings.enabled, "Unlimited Meetings");
            CheatToggles.invisible = GUILayout.Toggle(CheatToggles.invisible, "Invisibility (off-map net pos)");
            CheatToggles.invisiblePoof = GUILayout.Toggle(CheatToggles.invisiblePoof, "Phantom Poof");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Phantom In Lobby"))
            {
                Invisibility.LobbyPhantom.Vanish();
            }

            if (GUILayout.Button("Appear"))
            {
                Invisibility.LobbyPhantom.Appear();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.Label("Mirage (desync: you move normally, others see frozen/jitter):");
            CheatToggles.mirage = GUILayout.Toggle(CheatToggles.mirage, "Enable Mirage");
            if (CheatToggles.mirage)
            {
                CheatToggles.mirageFreeze = GUILayout.Toggle(CheatToggles.mirageFreeze, "Freeze frame (frozen to others)");
                CheatToggles.mirageFlicker = GUILayout.Toggle(CheatToggles.mirageFlicker, "Flicker (broken signal)");
                if (CheatToggles.mirageFlicker)
                {
                    GUILayout.Label($"Flicker min (frames): {CheatToggles.mirageFlickerMin}");
                    CheatToggles.mirageFlickerMin = (int)GUILayout.HorizontalSlider(CheatToggles.mirageFlickerMin, 1, 30);
                    GUILayout.Label($"Flicker max (frames): {CheatToggles.mirageFlickerMax}");
                    CheatToggles.mirageFlickerMax = (int)GUILayout.HorizontalSlider(CheatToggles.mirageFlickerMax, 1, 30);
                    if (CheatToggles.mirageFlickerMax < CheatToggles.mirageFlickerMin)
                        CheatToggles.mirageFlickerMax = CheatToggles.mirageFlickerMin;
                }
            }

            if (GUILayout.Button("Call Meeting"))
            {
                if (AmongUsClient.Instance.AmHost)
                {
                    MalumMenu.Log.LogInfo("We are the host, we can force a meeting");
                    Utilities.OpenMeeting(PlayerControl.LocalPlayer, null);
                }
                else
                {
                    PlayerControl.LocalPlayer.CmdReportDeadBody(null);
                }
            }

            GUILayout.Space(5);
            GUILayout.Label("Ghost:");
            CheatToggles.ghostAfterStart = GUILayout.Toggle(CheatToggles.ghostAfterStart, "Ghost After Start (die locally on spawn)");
            if (GUILayout.Button("SUICIDE (IMPOSTOR)"))
            {
                MalumMenu.notifications.Send("Suicide", GhostTools.SuicideNow(), 2.5f);
            }
            if (GUILayout.Button("LEAVE A BODY (HOST)"))
            {
                string err = Cheats.LeaveBody.Drop();
                MalumMenu.notifications.Send("Leave A Body", err ?? "Body dropped, reviving...", 2.5f);
            }

            if (GUILayout.Button("Complete All Tasks"))
            {
                PlayerControl.LocalPlayer.StartCoroutine(ErrorReporter.GuardCoroutine(CompleteAllTasks(), HandlingId, "CompleteAllTasks").WrapToIl2Cpp());
            }

            CheatToggles.autoTasks = GUILayout.Toggle(CheatToggles.autoTasks, " Auto-Complete Tasks (one by one)");
            if (CheatToggles.autoTasks)
            {
                GUILayout.Label($"  Gap Between Tasks: {Mathf.Max(0.8f, CheatToggles.autoTasksDelay):F1}s ({Cheats.AutoTasks.Left()} left)");
                CheatToggles.autoTasksDelay = GUILayout.HorizontalSlider(CheatToggles.autoTasksDelay, 0.8f, 6f);
            }

            if (GUILayout.Button("Randomize Avatar"))
            {
                if (AmongUsClient.Instance.AmConnected)
                {
                    Utilities.RandomizePlayer(true);

                    MalumMenu.notifications.Send("Player Randomizer", "Your avatar has been randomized for this game.", 5);
                }
                else
                {
                    AccountManager.Instance.RandomizeName();
                    Utilities.RandomizePlayer();

                    MalumMenu.notifications.Send("Player Randomizer", "Your name and avatar has been randomized.", 5);
                }
            }

            GUILayout.Label("Body Mode:");
            if (GUILayout.Button($" Body: {Cheats.BodyMode.ModeName()}"))
            {
                Cheats.BodyMode.Cycle();
            }
            GUILayout.Label("Client-side cosmetic only — others see it on your body.");

            GUILayout.Label("Outfits:");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Classic Look"))
            {
                MalumMenu.notifications.Send("Outfit", Cheats.OutfitTools.ClassicLook());
            }
            if (GUILayout.Button($"Apply Fav {CheatToggles.outfitFavSlot + 1}"))
            {
                MalumMenu.notifications.Send("Outfit", Cheats.OutfitTools.ApplyFavorite(CheatToggles.outfitFavSlot));
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Save Fav {CheatToggles.outfitFavSlot + 1}"))
            {
                MalumMenu.notifications.Send("Outfit", Cheats.OutfitTools.CaptureFavorite(CheatToggles.outfitFavSlot));
            }
            if (GUILayout.Button($"Fav Slot: {CheatToggles.outfitFavSlot + 1}"))
            {
                CheatToggles.outfitFavSlot = (CheatToggles.outfitFavSlot + 1) % 4;
            }
            GUILayout.EndHorizontal();
            CheatToggles.outfitResetMatch = GUILayout.Toggle(CheatToggles.outfitResetMatch, " Reset to Fav on Match Start");
            CheatToggles.outfitResetLobby = GUILayout.Toggle(CheatToggles.outfitResetLobby, " Reset to Fav on Lobby Join");

            GUILayout.Label("Colors:");
            CheatToggles.snipeColor = GUILayout.Toggle(CheatToggles.snipeColor, " Snipe Color in Lobby (auto-grab when free)");
            GUILayout.Label($"Sniped color: {CheatToggles.snipeColorId}");
            CheatToggles.snipeColorId = Mathf.Clamp((int)GUILayout.HorizontalSlider(CheatToggles.snipeColorId, 0, Cheats.ColorTools.MaxColor()), 0, Cheats.ColorTools.MaxColor());
            CheatToggles.nameColor = GUILayout.Toggle(CheatToggles.nameColor, " Colored Name (local rainbow/preset nick)");
            if (CheatToggles.nameColor)
            {
                if (GUILayout.Button($"Style: {Cheats.ColorTools.StyleName(CheatToggles.nameColorStyle)}"))
                {
                    CheatToggles.nameColorStyle = (CheatToggles.nameColorStyle + 1) % Cheats.ColorTools.NamePresetCount;
                }
                CheatToggles.nameColorAnimated = GUILayout.Toggle(CheatToggles.nameColorAnimated, " Animated Name");
                GUILayout.Label("(Hidden while ESP role/player info nametags are on.)");
            }

            GUILayout.Label("Your Past Nicks:");
            CheatToggles.nameHistory = GUILayout.Toggle(CheatToggles.nameHistory, " Track Nick History (by friend code)");
            CheatToggles.notifyKnown = GUILayout.Toggle(CheatToggles.notifyKnown, " Notify Known Players + Nick Changes");
            foreach (string nick in Cheats.NameHistory.OwnNicks())
            {
                GUILayout.Label("- " + nick);
            }

            GUILayout.Label("Task Animations:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Medbay Scan"))
            {
                if (!Utils.isLobby)
                {
                    Network.RPCEmitter.SendSetScanner(true);
                }else
                {
                    MalumMenu.notifications.Send("Anticheat Notice", "This cheat is disabled in lobby due to anticheat detection. You can use it once the game starts.");
                }
            }

            if (GUILayout.Button("Finish Medbay Scan"))
            {
                if (!Utils.isLobby)
                {
                    Network.RPCEmitter.SendSetScanner(false);
                }else
                {
                    MalumMenu.notifications.Send("Anticheat Notice", "This cheat is disabled in lobby due to anticheat detection. You can use it once the game starts.");
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Asteroids"))
            {
                if (!Utils.isLobby)
                {
                    Network.RPCEmitter.SendPlayAnimation((byte)TaskTypes.ClearAsteroids);
                }else
                {
                    MalumMenu.notifications.Send("Anticheat Notice", "This cheat is disabled in lobby due to anticheat detection. You can use it once the game starts.");
                }
            }

            if (GUILayout.Button("Empty Garbage"))
            {
                if (!Utils.isLobby)
                {
                    Network.RPCEmitter.SendPlayAnimation((byte)TaskTypes.EmptyGarbage);
                }else
                {
                    MalumMenu.notifications.Send("Anticheat Notice", "This cheat is disabled in lobby due to anticheat detection. You can use it once the game starts.");
                }
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Prime Shields"))
            {
                if (!Utils.isLobby)
                {
                    Network.RPCEmitter.SendPlayAnimation((byte)TaskTypes.PrimeShields);
                } else
                {
                    MalumMenu.notifications.Send("Anticheat Notice", "This cheat is disabled in lobby due to anticheat detection. You can use it once the game starts.");
                }
            }

            GUILayout.Space(5);
            GUILayout.Label($"Update level to: {level + 1}");
            level = (uint)GUILayout.HorizontalSlider(level, 0, 199);

            if (GUILayout.Button("Send Level Update"))
            {
                PlayerControl.LocalPlayer.RpcSetLevel(level);
                MalumMenu.notifications.Send("Level Updater", $"Your level has been changed to {level + 1}", 5);
            }
            GUILayout.EndVertical();
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SelfTab.Draw: draw self controls"); }
        }
        private IEnumerator CompleteAllTasks()
        {
            Il2CppSystem.Collections.Generic.List<PlayerTask> allTasks = PlayerControl.LocalPlayer.myTasks;

            MalumMenu.Log.LogInfo("Completing all tasks...");
            foreach (PlayerTask task in allTasks)
            {
                if (task.IsComplete)
                {
                    MalumMenu.Log.LogInfo($"Task {task.Id} has already been completed, skipping");
                    continue;
                }

                MalumMenu.Log.LogInfo($"Sent CompleteTask RPC for task {task.Id}");
                PlayerControl.LocalPlayer.RpcCompleteTask(task.Id);

                // If we want to complete more than six tasks then a delay needs to be implemented
                // otherwise the vanilla anticheat will kick us for violating ratelimits
                yield return Effects.Wait(0.05f);
            }

            MalumMenu.notifications.Send("Task Finisher", "All your tasks have been finished.", 5);
        }

    }
}

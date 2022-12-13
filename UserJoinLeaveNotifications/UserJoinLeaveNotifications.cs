using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System;
using BaseX;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections;
using FrooxEngine.UIX;
using static CloudX.Shared.CloudXInterface;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using System.IO;

namespace UserJoinLeaveNotifications
{
    public class UserJoinLeaveNotifications : NeosMod
    {
        public static ModConfiguration Config;

        private static readonly MethodInfo addNotificationMethod = AccessTools.Method(typeof(NotificationPanel), "AddNotification", new Type[] { typeof(string), typeof(string), typeof(Uri), typeof(color), typeof(string), typeof(Uri), typeof(IAssetProvider<AudioClip>) });

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<Uri> EasterEggList = new ModConfigurationKey<Uri>("EasterEggList", "URI to load the list of override sounds from.", () => new Uri("https://raw.githubusercontent.com/Banane9/NeosUserJoinLeaveNotifications/master/EasterEggs.json"), true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> EnableFocusedJoinSound = new ModConfigurationKey<bool>("EnableFocusedJoinSound", "Enable playing the sound clip set for users joining the focused session.", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> EnableFocusedLeaveSound = new ModConfigurationKey<bool>("EnableFocusedLeaveSound", "Enable playing the sound clip set for users leaving the focused session.", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> EnableUnfocusedJoinSound = new ModConfigurationKey<bool>("EnableUnfocusedJoinSound", "Enable playing the sound clip set for users joining an unfocused session.", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> EnableUnfocusedLeaveSound = new ModConfigurationKey<bool>("EnableUnfocusedLeaveSound", "Enable playing the sound clip set for users leaving an unfocused session.", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> FocusWorldFromNotifications = new ModConfigurationKey<bool>("FocusWorldFromNotifications", "Makes clicking a User join/leave notification focus the world it relates to.", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<Uri> JoinFocusedNotificationSoundUri = new ModConfigurationKey<Uri>("NotificationSound", "Notification sound for users joining the focused session. Disabled when null.", () => new Uri("neosdb:///9dc5b04079ade110bed56ae55af986f237d8f55d1973e41e19825b60156de996.wav"));

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<color> JoinFocusedWorldColor = new ModConfigurationKey<color>("JoinFocusedWorldColor", "Color of the notification for a User joining the focused session.", () => BlendColor(color.Blue));

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<Uri> JoinUnfocusedNotificationSoundUri = new ModConfigurationKey<Uri>("JoinUnfocusedNotificationSound", "Notification sound for users joining an unfocused session. Disabled when null.", () => new Uri("neosdb:///c0d5cfb879a42bd04f262071e20fd7033ec3172102ae7d14dcecee536cf3b64d.wav"));

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<color> JoinUnfocusedWorldColor = new ModConfigurationKey<color>("JoinUnfocusedWorldColor", "Color of the notification for a User joining an unfocused session.", () => BlendColor(BlendColor(color.Blue)));

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<Uri> LeaveFocusedNotificationSoundUri = new ModConfigurationKey<Uri>("NotificationLeaveSound", "Notification sound for users joining the focused session. Disabled when null.", () => new Uri("neosdb:///887ac33061fc43e029d994a05169bc0f869689b0e7af37813bb11e0f21e2ae14.wav"));

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<color> LeaveFocusedWorldColor = new ModConfigurationKey<color>("LeaveFocusedWorldColor", "Color of the notification for a User leaving the focused session.", () => BlendColor(color.Red));

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<Uri> LeaveUnfocusedNotificationSoundUri = new ModConfigurationKey<Uri>("LeaveUnfocusedNotificationSound", "Notification sound for users leaving an unfocused session. Disabled when null.", () => new Uri("neosdb:///1a56efe474f539f167b75a802e3afa302ba1fc4d20599273fce017e62fb40c4c.wav"));

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<color> LeaveUnfocusedWorldColor = new ModConfigurationKey<color>("LeaveUnfocusedWorldColor", "Color of the notification for a User leaving an unfocused session.", () => BlendColor(BlendColor(color.Red)));

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> ShowUnfocusedWorldEvents = new ModConfigurationKey<bool>("ShowUnfocusedWorldEvents", "Show notifications for Users joining/leaving unfocused sessions.", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> UseEasterEggSounds = new ModConfigurationKey<bool>("UseEasterEggSounds", "Use override sounds for some users.", () => true);

        private static Action<string, string, Uri, color, string, Uri, IAssetProvider<AudioClip>> addNotification;
        private static Dictionary<string, SoundOverride> soundOverrides = new Dictionary<string, SoundOverride>();
        public override string Author => "Banane9";
        public override string Link => "https://github.com/Banane9/NeosUserJoinLeaveNotifications";
        public override string Name => "UserJoinLeaveNotifications";
        public override string Version => "2.2.0";

        public static void Setup()
        {
            // Hook into the world focused event
            Engine.Current.WorldManager.WorldAdded += world => world.WorldRunning += OnNewWorldRunning;
        }

        public override void OnEngineInit()
        {
            Config = GetConfiguration();
            Config.Save(true);

            Engine.Current.OnReady += Setup;

            Harmony harmony = new Harmony($"{Author}.{Name}");
            harmony.PatchAll();

            loadEasterEggSounds();
        }

        private static void AddNotification(World world, string userId, string message, Uri thumbnail, color backgroundColor, string mainMessage = "N/A", Uri overrideProfile = null, IAssetProvider<AudioClip> clip = null)
        {
            NotificationPanel.Current.RunSynchronously(() =>
            { // ;-;
                addNotification(userId, message, thumbnail, backgroundColor, mainMessage, overrideProfile, clip);
                AddWorldFocus(NotificationPanel.Current, world);
            });
        }

        private static void AddWorldFocus(NotificationPanel notificationPanel, World world)
        {
            if (!Config.GetValue(FocusWorldFromNotifications))
                return;

            // _items is List<NotificationPanel.NotificationItem>
            var items = Traverse.Create(notificationPanel).Field("_items").GetValue<IList>();
            var root = Traverse.Create(items[items.Count - 1]).Field("root").GetValue<Slot>();

            root.GetComponentInChildren<Button>().LocalPressed += (button, data) => Engine.Current.WorldManager.FocusWorld(world);
        }

        private static color BlendColor(color color)
        {
            return MathX.Lerp(color, color.White, 0.5f);
        }

        private static UserBag GetUserbag(World world)
        {
            return Traverse.Create(world).Field<UserBag>("_users").Value;
        }

        private static string GetUserChangeMessage(User user, bool joining, World world)
        {
            return $"{user.UserName} {(joining ? "joined" : "left")} {world.Name}";
        }

        // Patch the add notification method to do this
        // Async method to fetch thumbnail from user id
        private static async Task<Uri> GetUserThumbnail(string userId)
        {
            // Handle fetching profile, AddNotification only gets profile data for friends
            var cloudUserProfile = (await Engine.Current.Cloud.GetUser(userId))?.Entity?.Profile;
            return TryFromString(cloudUserProfile?.IconUrl) ?? NeosAssets.Graphics.Thumbnails.AnonymousHeadset;
        }

        private static async void loadEasterEggSounds()
        {
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetStreamAsync(Config.GetValue(EasterEggList));
                soundOverrides = JsonSerializer.CreateDefault()
                    .Deserialize<Dictionary<string, SoundOverride>>(new JsonTextReader(new StreamReader(response)));

                Warn($"Loaded Sound Overrides for: {string.Join(", ", soundOverrides.Keys)}");
            }
        }

        private static void OnNewWorldRunning(World world)
        {
            // Get the user bag of the new world
            var userBag = GetUserbag(world);

            // Add the event handler to the new world
            userBag.OnElementAdded += OnUserJoined;
            userBag.OnElementRemoved += OnUserLeft;
        }

        private static void OnUserJoined(SyncBagBase<RefID, User> bag, RefID key, User user, bool isNew)
        {
            var focusedWorld = Engine.Current.WorldManager.FocusedWorld == bag.World;

            if (user.IsLocalUser || NotificationPanel.Current == null
            || (!Config.GetValue(ShowUnfocusedWorldEvents) && !focusedWorld))
                return;

            NotificationPanel.Current.RunInUpdates(3, async () =>
            {
                // Running immediately results in the getuser to return a BadRequest
                var thumbnail = await GetUserThumbnail(user.UserID);
                var audioClipUri = Config.GetValue(focusedWorld ? EnableFocusedJoinSound : EnableUnfocusedJoinSound) ?
                                    (Config.GetValue(UseEasterEggSounds) && soundOverrides.TryGetValue(user.UserID, out var soundOverride) ?
                                        (focusedWorld ? soundOverride.JoinFocused : soundOverride.JoinUnfocused)
                                        : Config.GetValue(focusedWorld ? JoinFocusedNotificationSoundUri : JoinUnfocusedNotificationSoundUri))
                                    : null;

                var audioClip = audioClipUri != null ? NotificationPanel.Current.Slot.AttachAudioClip(audioClipUri) : null;

                AddNotification(bag.World,
                    user.UserID,
                    GetUserChangeMessage(user, true, bag.World),
                    TryFromString(bag.World.GetSessionInfo().Thumbnail),
                    Config.GetValue(focusedWorld ? JoinFocusedWorldColor : JoinUnfocusedWorldColor),
                    "User Joined",
                    thumbnail,
                    audioClip);
            });
        }

        private static async void OnUserLeft(SyncBagBase<RefID, User> bag, RefID key, User user)
        {
            var focusedWorld = Engine.Current.WorldManager.FocusedWorld == bag.World;

            if (user.IsLocalUser || NotificationPanel.Current == null
            || (!Config.GetValue(ShowUnfocusedWorldEvents) && !focusedWorld))
                return;

            var thumbnail = await GetUserThumbnail(user.UserID);
            var audioClipUri = Config.GetValue(focusedWorld ? EnableFocusedLeaveSound : EnableUnfocusedLeaveSound) ?
                                (Config.GetValue(UseEasterEggSounds) && soundOverrides.TryGetValue(user.UserID, out var soundOverride) ?
                                    (focusedWorld ? soundOverride.LeaveFocused : soundOverride.LeaveUnfocused)
                                    : Config.GetValue(focusedWorld ? LeaveFocusedNotificationSoundUri : LeaveUnfocusedNotificationSoundUri))
                                : null;

            var audioClip = audioClipUri != null ? NotificationPanel.Current.Slot.AttachAudioClip(audioClipUri) : null;

            AddNotification(bag.World,
                user.UserID,
                GetUserChangeMessage(user, false, bag.World),
                TryFromString(bag.World.GetSessionInfo().Thumbnail),
                Config.GetValue(focusedWorld ? LeaveFocusedWorldColor : LeaveUnfocusedWorldColor),
                "User Left",
                thumbnail,
                audioClip);
        }

        [HarmonyPatch(typeof(NotificationPanel))]
        private static class NotificationPanelPatches
        {
            [HarmonyPostfix]
            [HarmonyPatch("OnAttach")]
            private static void OnAttachPostfix(NotificationPanel __instance)
            {
                addNotification = AccessTools.MethodDelegate<Action<string, string, Uri, color, string, Uri, IAssetProvider<AudioClip>>>(addNotificationMethod, NotificationPanel.Current);
            }
        }
    }
}
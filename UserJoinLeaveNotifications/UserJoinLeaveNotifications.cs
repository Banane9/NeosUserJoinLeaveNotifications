using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System;
using BaseX;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections;
using FrooxEngine.UIX;

namespace UserJoinLeaveNotifications
{
    public class UserJoinLeaveNotifications : NeosMod
    {
        public static ModConfiguration Config;

        private static readonly MethodInfo addNotification = AccessTools.Method(typeof(NotificationPanel), "AddNotification", new Type[] { typeof(string), typeof(string), typeof(Uri), typeof(color), typeof(string), typeof(Uri), typeof(IAssetProvider<AudioClip>) });

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> FocusWorldFromNotifications = new ModConfigurationKey<bool>("FocusWorldFromNotifications", "Makes clicking a User join/leave notification focus the world it relates to.", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<color> JoinFocusedWorldColor = new ModConfigurationKey<color>("JoinFocusedWorldColor", "Color of the notification for a User joining the focused world.", () => BlendColor(color.Blue));

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<color> JoinUnfocusedWorldColor = new ModConfigurationKey<color>("JoinUnfocusedWorldColor", "Color of the notification for a User joining an unfocused world.", () => BlendColor(BlendColor(color.Blue)));

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<color> LeaveFocusedWorldColor = new ModConfigurationKey<color>("LeaveFocusedWorldColor", "Color of the notification for a User leaving the focused world.", () => BlendColor(color.Red));

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<color> LeaveUnfocusedWorldColor = new ModConfigurationKey<color>("LeaveUnfocusedWorldColor", "Color of the notification for a User leaving an unfocused world.", () => BlendColor(BlendColor(color.Red)));

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> ShowUnfocusedWorldEvents = new ModConfigurationKey<bool>("ShowUnfocusedWorldEvents", "Color of the notification for a User leaving an unfocused world.", () => true);

        public override string Author => "Banane9";
        public override string Link => "https://github.com/Banane9/NeosUserJoinLeaveNotifications";
        public override string Name => "UserJoinLeaveNotifications";
        public override string Version => "2.0.0";

        public static void Setup()
        {
            // Hook into the world focused event
            Engine.Current.WorldManager.WorldAdded += OnWorldAdded;
        }

        public override void OnEngineInit()
        {
            Config = GetConfiguration();
            Config.Save(true);

            Engine.Current.RunPostInit(Setup);
        }

        private static void AddNotification(World world, string userId, string message, Uri thumbnail, color backgroundColor, string mainMessage = "N/A", Uri overrideProfile = null, IAssetProvider<AudioClip> clip = null)
        {
            NotificationPanel.Current?.RunSynchronously(() =>
            { // ;-;
                addNotification.Invoke(NotificationPanel.Current, new object[] { userId, message, thumbnail, backgroundColor, mainMessage, overrideProfile, clip });
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

        private static string GetUserChangeMessage(User user, bool joining, World world, bool focusedWorld)
        {
            return $"{user.UserName} {(joining ? "joined" : "left")}{(Config.GetValue(ShowUnfocusedWorldEvents) ? $" {(focusedWorld ? "this Session" : world.Name)}" : "")}";
        }

        // Async method to fetch thumbnail from user id
        private static async Task<Uri> GetUserThumbnail(string userId)
        {
            // Handle fetching profile, AddNotification only gets profile data for friends
            var cloudUserProfile = (await Engine.Current.Cloud.GetUser(userId))?.Entity?.Profile;
            return CloudX.Shared.CloudXInterface.TryFromString(cloudUserProfile?.IconUrl) ?? NeosAssets.Graphics.Thumbnails.AnonymousHeadset;
        }

        private static void OnUserJoined(SyncBagBase<RefID, User> bag, RefID key, User user, bool isNew)
        {
            var focusedWorld = Engine.Current.WorldManager.FocusedWorld == bag.World;

            if (user.IsLocalUser || NotificationPanel.Current == null
            || (!Config.GetValue(ShowUnfocusedWorldEvents) && !focusedWorld))
                return;

            NotificationPanel.Current.RunInUpdates(3, async () =>
            { // Running immediately results in the getuser to return a BadRequest
                var thumbnail = await GetUserThumbnail(user.UserID);

                AddNotification(bag.World,
                    user.UserID,
                    GetUserChangeMessage(user, true, bag.World, focusedWorld),
                    thumbnail,
                    Config.GetValue(focusedWorld ? JoinFocusedWorldColor : JoinUnfocusedWorldColor),
                    $"User Joined{(focusedWorld ? "" : " Unfocused")}");
            });
        }

        private static async void OnUserLeft(SyncBagBase<RefID, User> bag, RefID key, User user)
        {
            var focusedWorld = Engine.Current.WorldManager.FocusedWorld == bag.World;

            if (user.IsLocalUser || NotificationPanel.Current == null
            || (!Config.GetValue(ShowUnfocusedWorldEvents) && !focusedWorld))
                return;

            var thumbnail = await GetUserThumbnail(user.UserID);

            AddNotification(bag.World,
                user.UserID,
                GetUserChangeMessage(user, false, bag.World, focusedWorld),
                thumbnail,
                Config.GetValue(focusedWorld ? LeaveFocusedWorldColor : LeaveUnfocusedWorldColor),
                $"User Left{(focusedWorld ? "" : " Unfocused")}");
        }

        private static void OnWorldAdded(World world)
        {
            // Store the user bag of the new world
            var userBag = GetUserbag(world);

            // Add the event handler to the new world
            userBag.OnElementAdded += OnUserJoined;
            userBag.OnElementRemoved += OnUserLeft;
        }
    }
}
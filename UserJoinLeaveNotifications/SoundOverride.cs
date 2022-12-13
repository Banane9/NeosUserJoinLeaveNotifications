using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable CS0649 // Fields assigned by Json Deserialization

namespace UserJoinLeaveNotifications
{
    [JsonObject(MemberSerialization.Fields)]
    internal struct SoundOverride
    {
        private Uri join;
        private Uri joinFocused;
        private Uri joinUnfocused;
        private Uri leave;
        private Uri leaveFocused;
        private Uri leaveUnfocused;

        public Uri JoinFocused => joinFocused ?? join;

        public Uri JoinUnfocused => joinUnfocused ?? join;

        public Uri LeaveFocused => leaveFocused ?? leave;

        public Uri LeaveUnfocused => leaveUnfocused ?? leave;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

namespace ViverseWebGLAPI
{
    [Serializable]
    public class LoginResult
    {
	    [Preserve] public string state;
        [Preserve] public string access_token;
        [Preserve] public string account_id;
        [Preserve] public int expire_in;
        [Preserve] public string token_type;
    }

    [Serializable]
    public class AccessTokenResult
    {
        [Preserve] public string access_token;
        [Preserve] public string account_id;
        [Preserve] public int expires_in;
    }

    [Serializable]
    public class LeaderboardResult
    {
	    [Preserve] public LeaderboardEntry[] ranking;
	    [Preserve] public LeaderboardMeta meta;
        [Preserve] public int total_count;
    }
    [Serializable, Preserve]
    public class DisplayName
    {
	    [Preserve] public string lang;
	    [Preserve] public string name;
    }
    [Serializable, Preserve]
    public class LeaderboardMeta
    {
	    [Preserve] public string app_id;
	    [Preserve] public string meta_name;
	    [Preserve] public DisplayName[] display_name;
	    [Preserve] public int sort_type;
	    [Preserve] public int update_type;
	    [Preserve] public int data_type;
    }
    [Serializable,Preserve]
    public class LeaderboardEntry
    {
	    [Preserve] public string uid;
	    [Preserve] public string name;
	    [Preserve] public double value;
	    [Preserve] public int rank;
    }
    [Serializable]
    public class Avatar
    {
	    [Preserve] public string id;
	    [Preserve] public bool isPrivate;
	    [Preserve] public string vrmUrl;
	    [Preserve] public string headIconUrl;
	    [Preserve] public string snapshot;
	    [Preserve] public long createTime;
	    [Preserve] public long updateTime;
    }

    [Serializable]
    public class AvatarListWrapper {
	    [Preserve] public Avatar[] avatars;
    }
    [Serializable]
    public class UserProfile
    {
	    [Preserve] public string name;
	    [Preserve] public Avatar activeAvatar;
    }
}

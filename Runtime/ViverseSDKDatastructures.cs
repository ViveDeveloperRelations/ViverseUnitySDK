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

    /// <summary>
    /// Represents a single achievement with its metadata
    /// </summary>
    [Serializable,Preserve]
    public class Achievement
    {
        [Preserve] public string api_name;
        [Preserve] public bool unlock;
    }

    /// <summary>
    /// Wrapper class for serializing a list of achievements
    /// </summary>
    [Serializable,Preserve]
    public class AchievementsWrapper
    {
        [Preserve] public Achievement[] achievements;
    }

    /// <summary>
    /// Detailed information about a user achievement
    /// </summary>
    [Serializable,Preserve]
    public class UserAchievementInfo
    {
        [Preserve] public string achievement_id;
        [Preserve] public string api_name;
        [Preserve] public string display_name;
        [Preserve] public string description;
        [Preserve] public bool is_achieved;
        [Preserve] public string achieved_icon;
        [Preserve] public string unachieved_icon;
        [Preserve] public int achieved_times;
    }

    /// <summary>
    /// Result of getting user achievements
    /// </summary>
    [Serializable,Preserve]
    public class UserAchievementResult
    {
        [Preserve] public UserAchievementInfo[] achievements;
        [Preserve] public int total;
    }

    /// <summary>
    /// Information about a successful achievement upload
    /// </summary>
    [Serializable,Preserve]
    public class SuccessAchievement
    {
        [Preserve] public string api_name;
        [Preserve] public long time_stamp;
    }

    /// <summary>
    /// Information about a failed achievement upload
    /// </summary>
    [Serializable]
    public class FailureAchievement
    {
        [Preserve] public string api_name;
        [Preserve] public int code;
        [Preserve] public string message;
    }

    /// <summary>
    /// Success information for achievement uploads
    /// </summary>
    [Serializable,Preserve]
    public class SuccessInfo
    {
        [Preserve] public int total;
        [Preserve] public SuccessAchievement[] achievements;
    }

    /// <summary>
    /// Failure information for achievement uploads
    /// </summary>
    [Serializable,Preserve]
    public class FailureInfo
    {
        [Preserve] public int total;
        [Preserve] public FailureAchievement[] achievements;
    }

    /// <summary>
    /// Complete result of an achievement upload operation
    /// </summary>
    [Serializable,Preserve]
    public class AchievementUploadResult
    {
        [Preserve] public SuccessInfo success;
        [Preserve] public FailureInfo failure;
    }
}


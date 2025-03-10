using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace Tests.TestEndpoints
{
    [Serializable]
    public class Cookie
    {
        public string client_id;
        public string access_token;
        public string account_id;
        public int expires_in;
        public string token_type;
        public string scope;
        public string refresh_token;
        public int expires_in_epoch;
    }
    [Serializable]
    public class Asset
    {
        public string Id;
        public int Type;
    }
#if NEWER_AVATAR_DATA_STRUCTURES
    [Serializable]
    public class Avatar
    {
        public int id;
        public string ViveportId;
        public string DataType;
        public string Data;
        public string MetaData;
        public string s3Key_bin;
        public string s3Key_snapshot;
        public string s3Key_headicon;
        public string s3Key_vrmBin;
        public string BinaryDataUrl;
        public string VrmBinaryDataUrl;
        public string SnapshotDataUrl;
        public string HeadIconDataUrl;
        public int UpdateTime;
        public int CreateTime;
        public bool IsEncrypted;
        public bool IsDisabled;
        public List<Asset> Assets;
    }
    [Serializable]
    public class AvatarListData
    {
        public int CurrentAvatarId;
        public List<Avatar> Avatars;
    }

#endif

    [Serializable]
    public class ActiveAvatar
    {
        public string id;
        public string snapshot_url;
        public string avatar_url;
        public string head_icon_url;
        public bool is_half_body;
        public int data_type;
        public int gender;
    }
    [Serializable]
    public class Profile
    {
        public string id;
        public string htc_account_id;
        public string htc_account_email;
        public string htc_account_phone_number;
        public string wallet_address;
        public string display_name;
        public bool is_admin;
        public List<string> liked_rooms;
        public List<string> liked_events;
        public string oursong_account_id;
        public bool is_over_18;
        public string community_user_id;
        public ActiveAvatar active_avatar;
    }
}

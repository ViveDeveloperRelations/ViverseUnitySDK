using System.Collections.Generic;

namespace ViverseWebGLAPI
{
    public class HostConfigLookup
    {
        public static Dictionary<HostConfigUtil.HostType, HostConfig> HostTypeToDefaultHostConfig = new()
            {
                /*
                 {HostConfigUtil.HostType.DEV,
                    new HostConfig() {
                        SSODomain = new SSODomain("https://account.htcvive.com"), //TODO: find sso domain for dev?
                        WorldHost = new WorldHost("https://world-dev.viverse.com/"),
                        ViveSyncHost = new ViveSyncHost("https://world-api-dev.viverse.com/"),
                        AvatarHost = new AvatarHost("https://sync-dev-usw2.vive.com/"),
                        CookieAccessKey = new CookieAccessKey("_htc_refresh_token_dev")}},
                */
                {HostConfigUtil.HostType.STAGE, new HostConfig()
                {
                    SSODomain = new SSODomain("https://www-stage.viverse.com"),
                    WorldHost = new WorldHost("https://world-stage.viverse.com/"),
                    WorldAPIHost = new WorldAPIHost("https://world-api-stage.viverse.com/"),
                    AvatarHost = new AvatarHost("https://avatar-stage.viverse.com/"),
                    CookieAccessKey = new CookieAccessKey("_htc_access_token_stage")
                } },
                {HostConfigUtil.HostType.PROD, new HostConfig()
                {
                    SSODomain = new SSODomain("https://account.htcvive.com"),
                    WorldHost = new WorldHost("https://world.viverse.com/"),
                    WorldAPIHost = new WorldAPIHost("https://world-api.viverse.com/"),
                    AvatarHost = new AvatarHost("https://sdk-api.viverse.com/"),
                    CookieAccessKey = new CookieAccessKey("_htc_access_token_production")
                } }
            };

    }

    public class HostConfig
    {
        public int DefaultTimeoutInSeconds = 10;
        public SSODomain SSODomain;
        public WorldHost WorldHost;
        public CookieAccessKey CookieAccessKey; //For interop with the js library
        //used when constructing urls manually in C#
        public WorldAPIHost WorldAPIHost;
        public AvatarHost AvatarHost;
    }

    //commented out while reconstructing api
    //temp place to put data related to a user, no parallel to this in the actual web code
    public class UserInfo
    {
        public AuthKey AuthKey;
        public UserProfile Profile;
        public Avatar ActiveAvatar;
    }


}

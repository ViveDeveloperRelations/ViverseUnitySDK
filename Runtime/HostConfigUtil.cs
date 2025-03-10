using System;
using System.Collections.Generic;

namespace ViverseWebGLAPI
{
    //random tidbits to get host from relevant data
    public class HostConfigUtil
    {
        public enum HostType
        {
            UNKNOWN=0,
            //DEV=1, //dev disabled as we don't have all the settings for it
            STAGE=2,
            PROD=3
        }
        public HostType GetHostTypeFromPageURLIfPossible(string currentPageURL)
        {
            if(currentPageURL == null) return HostType.UNKNOWN;
            Uri uri = new Uri(currentPageURL);
            switch (uri.Host)
            {
                //case  "world-dev.viverse.com":
                //    return HostType.DEV;
                case "world-stage.viverse.com":
                    return HostType.STAGE;
                case "world.viverse.com":
                    return HostType.PROD;
                default:
                    return HostType.UNKNOWN;
            }
        }
    }
}
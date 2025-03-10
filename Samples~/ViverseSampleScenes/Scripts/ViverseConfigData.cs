using UnityEngine;

[System.Serializable]
public class ViverseConfigData
{
	public string ClientId;
	public string Domain = "account.htcvive.com";
	public string CookieDomain = "";

	public static ViverseConfigData LoadFromPrefs()
	{
		var config = new ViverseConfigData();
		config.ClientId = PlayerPrefs.GetString("ViverseClientId", "");
		return config;
	}

	public void SaveToPrefs()
	{
		PlayerPrefs.SetString("ViverseClientId", ClientId);
		PlayerPrefs.Save();
	}
}

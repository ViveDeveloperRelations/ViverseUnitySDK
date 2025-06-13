using UnityEditor;

//if i keep this up, then move to the standard kevyvaluestore/datastore pattern with generics
public class SessionStateStringValue
{
	public readonly string Key;
	private readonly string _defaultValue;

	public string Value
	{
		get => SessionState.GetString(Key,_defaultValue);
		set => SessionState.SetString(Key,value);
	}

	public SessionStateStringValue(string key, string defaultValue)
	{
		Key = key;
		_defaultValue = defaultValue;
	}
}


public class SessionStateBoolValue
{
	public readonly string Key;
	private readonly bool _defaultValue;

	public bool Value
	{
		get => SessionState.GetBool(Key,_defaultValue);
		set => SessionState.SetBool(Key,value);
	}

	public SessionStateBoolValue(string key, bool defaultValue=false)
	{
		Key = key;
		_defaultValue = defaultValue;
	}

}

public class EditorPrefsBoolValue
{
	public readonly string Key;
	private readonly bool _defaultValue;

	public bool Value
	{
		get => EditorPrefs.GetBool(Key,_defaultValue);
		set => EditorPrefs.SetBool(Key,value);
	}

	public EditorPrefsBoolValue(string key, bool defaultValue=false)
	{
		Key = key;
		_defaultValue = defaultValue;
	}

}

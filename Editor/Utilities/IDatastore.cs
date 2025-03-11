using UnityEditor;

namespace ViverseWebGLAPI
{
	/// <summary>
	/// Interface for data storage handling with session state abstraction
	/// </summary>
	public interface IDatastore
	{
	    void SetBool(string key, bool value);
	    bool GetBool(string key, bool defaultValue);
	    void SetInt(string key, int value);
	    int GetInt(string key, int defaultValue);
	    void SetString(string key, string value);
	    string GetString(string key, string defaultValue);
	}

	/// <summary>
	/// Implementation of IDatastore using SessionState for editor persistence
	/// </summary>
	public class SessionStateDatastore : IDatastore
	{
	    public void SetBool(string key, bool value)
	    {
	        SessionState.SetBool(key, value);
	    }

	    public bool GetBool(string key, bool defaultValue)
	    {
	        return SessionState.GetBool(key, defaultValue);
	    }

	    public void SetInt(string key, int value)
	    {
	        SessionState.SetInt(key, value);
	    }

	    public int GetInt(string key, int defaultValue)
	    {
	        return SessionState.GetInt(key, defaultValue);
	    }

	    public void SetString(string key, string value)
	    {
	        SessionState.SetString(key, value);
	    }

	    public string GetString(string key, string defaultValue)
	    {
	        return SessionState.GetString(key, defaultValue);
	    }
	}

	/// <summary>
	/// Wrapper for a boolean value stored in session state
	/// </summary>
	public class SessionStateVarBoolKey
	{
	    private readonly string key;
	    private readonly IDatastore datastore;

	    public SessionStateVarBoolKey(string key, IDatastore datastore)
	    {
	        this.key = key;
	        this.datastore = datastore;
	    }

	    public bool Value
	    {
	        get => datastore.GetBool(key, false);
	        set => datastore.SetBool(key, value);
	    }
	}

	/// <summary>
	/// Wrapper for an integer value stored in session state
	/// </summary>
	public class SessionStateVarIntKey
	{
	    private readonly string key;
	    private readonly IDatastore datastore;

	    public SessionStateVarIntKey(string key, IDatastore datastore)
	    {
	        this.key = key;
	        this.datastore = datastore;
	    }

	    public int Value
	    {
	        get => datastore.GetInt(key, 0);
	        set => datastore.SetInt(key, value);
	    }
	}

	/// <summary>
	/// Wrapper for a string value stored in session state
	/// </summary>
	public class SessionStateVarStringKey
	{
	    private readonly string key;
	    private readonly IDatastore datastore;

	    public SessionStateVarStringKey(string key, IDatastore datastore)
	    {
	        this.key = key;
	        this.datastore = datastore;
	    }

	    public string Value
	    {
	        get => datastore.GetString(key, string.Empty);
	        set => datastore.SetString(key, value);
	    }
	}

	/// <summary>
	/// Wrapper for an enum value stored in session state
	/// </summary>
	public class SessionStateVarEnumKey<T> where T : System.Enum
	{
	    private readonly string key;
	    private readonly IDatastore datastore;

	    public SessionStateVarEnumKey(string key, IDatastore datastore)
	    {
	        this.key = key;
	        this.datastore = datastore;
	    }

	    public T Value
	    {
	        get => (T)System.Enum.ToObject(typeof(T), datastore.GetInt(key, 0));
	        set => datastore.SetInt(key, System.Convert.ToInt32(value));
	    }
	}
}

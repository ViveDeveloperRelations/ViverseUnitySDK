using System;

namespace ViverseWebGLAPI
{
    public class CookieAccessKey : NamedStringClass
    {
        public string CookieAccessKeyString => Key;
        public CookieAccessKey(string key) : base(key) { }
    }
    public class SSODomain : NamedStringClass
    {
        public string SSODomainString => Key;
        public SSODomain(string key) : base(key) { }
    }
    public class WorldHost : NamedStringClass
    {
        public string HostString => Key;
        public WorldHost(string key) : base(key) { }
    }
    
    public class WorldAPIHost : NamedStringClass
    {
        public string HostString => Key;
        public WorldAPIHost(string key) : base(key) { }
    }
    public class AvatarHost : NamedStringClass
    {
        public string HostString => Key;
        public AvatarHost(string key) : base(key) { }
    }
    public class AuthKey : NamedStringClass
    {
        public string AuthKeyString => Key;
        public AuthKey(string key) : base(key) { }
    }

    public class NamedStringClass : IComparable<NamedStringClass>, IEquatable<NamedStringClass>
    {
        public string Key { get; }

        public NamedStringClass(string key)
        {
            Key = key;
        }

        // IComparable<NamedStringClass> implementation
        public int CompareTo(NamedStringClass other)
        {
            if (other == null) return 1; // This object is greater than null
            return string.Compare(Key, other.Key, StringComparison.Ordinal);
        }

        // IEquatable<NamedStringClass> implementation
        public bool Equals(NamedStringClass other)
        {
            if (other == null) return false;
            return Key == other.Key && GetType() == other.GetType(); // Check type along with the Key
        }

        // Override Equals for object comparison
        public override bool Equals(object obj)
        {
            if (obj is NamedStringClass other)
            {
                return Equals(other);
            }
            return false;
        }

        // Override GetHashCode to ensure consistency with Equals
        public override int GetHashCode()
        {
            return Key?.GetHashCode() ?? 0;
        }

        // Operator overloads for == and != to compare instances directly, including types
        public static bool operator ==(NamedStringClass left, NamedStringClass right)
        {
            if (ReferenceEquals(left, null) && ReferenceEquals(right, null)) return true;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null)) return false;
            return left.Equals(right);
        }

        public static bool operator !=(NamedStringClass left, NamedStringClass right)
        {
            return !(left == right);
        }
    }

}
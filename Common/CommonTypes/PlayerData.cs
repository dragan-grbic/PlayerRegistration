using System;
using System.Runtime.Serialization;

namespace PlayerRegistration.CommonTypes
{
    [Serializable()]
    public struct PlayerData
    {
        public PlayerData(string key, string name)
        {
            Key = key;
            Name = name;
        }
        public string Key { get; }
        public string Name { get; }
    }
}

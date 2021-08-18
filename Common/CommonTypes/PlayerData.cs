using System;
using System.Runtime.Serialization;

namespace Narde.CommonTypes
{
    [Serializable()]
    public struct PlayerData
    {
        public PlayerData(Guid guid, string name)
        {
            UUID = guid;
            Name = name;
        }
        public Guid UUID { get; }
        public string Name { get; }
    }
}

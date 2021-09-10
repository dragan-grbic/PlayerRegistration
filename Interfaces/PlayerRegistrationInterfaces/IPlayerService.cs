using System;
using Microsoft.ServiceFabric.Services.Remoting;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlayerRegistration.CommonTypes;

namespace PlayerRegistration.Interfaces
{
    public interface IPlayerService : IService
    {
        /// <summary>
        /// Get all registered players.
        /// </summary>
        /// <returns>List of all registered p[layers.</returns>
        Task<IEnumerable<PlayerData>> GetPlayers();
        /// <summary>
        /// Add new player.
        /// </summary>
        /// <param name="name">Name of the new player.</param>
        /// <returns>uuid of newly added player</returns>
        Task<string> AddPlayer(string name);
        /// <summary>
        /// Delete the player.
        /// </summary>
        /// <param name="uuid">Unique id od the player to be deleted.</param>
        /// <returns>The name of deleted player.</returns>
        Task<string> DeletePlayer(string uuid);
    }
}

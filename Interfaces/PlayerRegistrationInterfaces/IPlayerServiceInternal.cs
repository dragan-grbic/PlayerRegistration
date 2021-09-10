using Microsoft.ServiceFabric.Services.Remoting;
using PlayerRegistration.CommonTypes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PlayerRegistration.Interfaces
{
    public interface IPlayerServiceInternal : IService
    {
        public const string DefaultServiceUri = "fabric:/PlayerRegistration/PlayerService";
        /// <summary>
        /// Get all registered players.
        /// </summary>
        /// <returns>List of all registered p[layers.</returns>
        Task<IEnumerable<PlayerData>> GetPlayers();
        /// <summary>
        /// Add new player.
        /// </summary>
        /// <param name="key">Unique id of the new player.</param>
        /// <param name="name">Name of the new player.</param>
        /// <returns>Unique od of newly added player (same as guid param).</returns>
        Task<string> AddPlayer(string key, string name);
        /// <summary>
        /// Delete the player.
        /// </summary>
        /// <param name="key">Unique id od the player to be deleted.</param>
        /// <returns>The name of deleted player.</returns>
        Task<string> DeletePlayer(string key);
    }
}

using System;
using Microsoft.ServiceFabric.Services.Remoting;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Narde.Data;

namespace Narde.Interfaces
{
    public interface IPlayerService : IService
    {
        /// <summary>
        /// Get all registered players.
        /// </summary>
        /// <returns>List of PlayerData containing all registered p[layers.</returns>
        Task<List<PlayerData>> GetPlayers();
        /// <summary>
        /// Get all players currently online.
        /// </summary>
        /// <returns>List of PlayerData for all online players.</returns>
        Task<List<PlayerData>> GetPlatersOnline();
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

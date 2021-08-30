using Microsoft.ServiceFabric.Services.Remoting;
using Narde.CommonTypes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Narde.Interfaces
{
    public interface IPlayerServiceInternal : IService
    {
        public const string DefaultServiceUri = "fabric:/Narde/PlayerService";
        /// <summary>
        /// Get all registered players.
        /// </summary>
        /// <returns>List of all registered p[layers.</returns>
        Task<IEnumerable<PlayerData>> GetPlayers();
        /// <summary>
        /// Get all players currently online.
        /// </summary>
        /// <returns>List of all online players.</returns>
        Task<IEnumerable<PlayerData>> GetPlayersOnline();
        /// <summary>
        /// Add new player.
        /// </summary>
        /// <param name="guid">Unique id of the new player.</param>
        /// <param name="name">Name of the new player.</param>
        /// <returns>Unique od of newly added player (same as guid param).</returns>
        Task<Guid> AddPlayer(Guid guid, string name);
        /// <summary>
        /// Delete the player.
        /// </summary>
        /// <param name="guid">Unique id od the player to be deleted.</param>
        /// <returns>The name of deleted player.</returns>
        Task<string> DeletePlayer(Guid guid);
    }
}

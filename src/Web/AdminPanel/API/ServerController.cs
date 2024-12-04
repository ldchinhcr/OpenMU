namespace MUnique.OpenMU.Web.API
{
    using Microsoft.AspNetCore.Mvc;
    using MUnique.OpenMU.DataModel.Entities;
    using MUnique.OpenMU.GameLogic;
    using MUnique.OpenMU.GameServer;
    using MUnique.OpenMU.Interfaces;
    using MUnique.OpenMU.Persistence;
    using System.Text.Json;

    /// <summary>
    /// Bộ điều khiển API máy chủ
    /// </summary>
    [Route("api/")]
    public class ServerController : Controller
    {
        private IDictionary<int, IGameServer> _gameServers;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameServers"></param>
        public ServerController(IDictionary<int, IGameServer> gameServers) => _gameServers = gameServers;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        [Route("send/{id=0}")]
        public async Task<IActionResult> SendGlobalMessage(int id, [FromQuery(Name = "msg")] string msg)
        {
            var server = (GameServer)_gameServers.Values.ElementAt(id);
            if (server is not null)
            {
                await server.Context.SendGlobalNotificationAsync(msg).ConfigureAwait(false);
                return Ok("Hoàn thành");
            }
            return Ok("Máy chủ chưa sẵn sàng");
        }

        /// <summary>
        /// Lấy cờ, nếu tài khoản được chỉ định hiện đang trực tuyến.
        /// </summary>
        /// <param name="accountName">Tên của tài khoản.</param>
        /// <returns>True, khi trực tuyến.</returns>
        [HttpGet]
        [Route("is-online/{accountName=0}")]
        public async Task<bool> GetIsOnlineAsync(string accountName)
        {
            var isOnline = false;

            foreach (var server in this._gameServers.Values.OfType<GameServer>())
            {
                var players = await server.Context.GetPlayersAsync().ConfigureAwait(false);
                if (players.Any(p => p.Account?.LoginName == accountName))
                {
                    isOnline = true;
                    break;
                }
            }

            return isOnline;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("status")]
        public IActionResult ServerState()
        {
            int sum = 0;
            var list = new List<string>();
            _gameServers.Values.ForEach(async item =>
            {
                var server = item as GameServer;
                if(server is not null)
                {
                    await server.Context.ForEachPlayerAsync(player =>
                    {
                        list.Add(player.GetName());
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);
                    sum = sum + server.Context.PlayerCount;
                }
            });

            var item = new
            {
                state = "Trực tuyến",
                players = sum,
                playersList = list
            };

            return Ok(JsonSerializer.Serialize(item));
        }
    }
}
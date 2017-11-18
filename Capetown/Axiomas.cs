using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capetown
{
    /// <summary>
    /// Общие для системы константы
    /// </summary>
    public static class Axiomas
    {
        
        public const int DefaultRedisTransAttemptLimit = 500;

        #region Redis pub\sub queue's
        
        /// <summary>
        /// Канал оповещения о событиях публикации
        /// </summary>
        public const string RedisPublicationsQueueChannelName = "stores";
        
        #endregion

        #region Redis queue's lists
        
        public const string RedisPublicationsQueueListName = "stores_queue";
        
        #endregion

    }
}

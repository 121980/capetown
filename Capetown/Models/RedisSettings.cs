using System;

namespace Capetown.Models
{
    /// <summary>
    /// Настройки Redis
    /// </summary>
    public class RedisSettings
    {
        /// <summary>
        /// Хост к которому подключаемся
        /// </summary>
        public string Config { get; set; }

        public int CacheLiveTime { get; set; }
    }
}

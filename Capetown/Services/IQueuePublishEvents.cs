using System;
using System.Threading.Tasks;
using Capetown.Models;

namespace Capetown.Services
{
    /// <summary>
    /// Интерфейс публикации событий в каналы REDIS
    /// </summary>
    public interface IQueuePublishEvents
    {
        #region публикация событий

        /// <summary>
        /// Событие создания store
        /// </summary>
        /// <param name="store"></param>
        /// <returns></returns>
        Task PublishStoreEventAsync(Store store);
        
        #endregion
    }
}

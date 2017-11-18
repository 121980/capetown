using System;
using System.Threading.Tasks;
using Capetown.Models;

namespace Capetown.Services
{
    /// <summary>
    /// Интерфейсы публикации субъектов-контекстов в очереди REDIS
    /// </summary>
    public interface IQueuePublish
    {
        #region публикации в очереди
        
        /// <summary>
        /// Отправка в очередь контекста события публикации нового store, где он будет ожидать обработки
        /// </summary>
        /// <param name="store"></param>
        /// <returns></returns>
        Task PushPublicationToQueueAsync( Store store );
        
        #endregion
    }
}

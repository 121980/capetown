using System;


namespace Capetown.Services
{
    /// <summary>
    /// Интерфейс сервиса, публикующего в очереди сообщений
    /// </summary>
    public interface IQueueService: IQueuePublish, IQueuePublishEvents
    {
    }
}

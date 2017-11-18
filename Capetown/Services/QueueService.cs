using System;
using System.Threading.Tasks;
using Capetown.Models;
using Newtonsoft.Json;
using NLog;
using StackExchange.Redis;

namespace Capetown.Services
{
    /// <summary>
    /// Сервис очередей заданий
    /// </summary>
    public class QueueService : IQueueService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        private readonly IRedisCacheService _cache;
        
        /// <summary>
        /// Публикатор событий
        /// </summary>
        private readonly ISubscriber _publisher;

        /// <inheritdoc />
        public QueueService( IRedisCacheService cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _publisher = _cache.CreateSubscriber();
        }
        

        #region IQueueService

        

        #region публикация событий
        

        /// <inheritdoc />
        public async Task PublishStoreEventAsync( Store store )
        {
            try
            {
                var json = JsonConvert.SerializeObject(store);
                long receivers = await _publisher.PublishAsync(Axiomas.RedisPublicationsQueueChannelName, json, CommandFlags.None);
                _log.Info($@"Publish new store ( id = {store.Id}) event, receivers: {receivers}");
            }
            catch ( Exception ex )
            {
                _log.Error( $@"Failed to publish new store( id = {store.Id}) event, ex: {ex}" );
            }
        }
        
        #endregion

        #region публикации в очереди
        
        /// <inheritdoc />
        public async Task PushPublicationToQueueAsync( Store store )
        {
            try
            {
                long result = await _cache.ListLeftPushAsync(Axiomas.RedisPublicationsQueueListName, store);
                _log.Info($@"Publish new store ( id = {store.Id}) to queue, it size now: {result}");
                await PublishStoreEventAsync(store);
            }
            catch (Exception ex)
            {
                _log.Error($@"Failed to publish new store( id = {store.Id}) to queue, ex: {ex}");
            }
        }
        
        #endregion

        #endregion
    }
}

using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Capetown.Models;
using Newtonsoft.Json;
using NLog;
using StackExchange.Redis;

namespace Capetown.Services
{
    public class RedisCacheService : IRedisCacheService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        private ConnectionMultiplexer _muxer;

        private ConfigurationOptions _config;

        private IDatabase _db;

        private readonly RedisSettings _settings;

        /// <summary>
        /// Для тестов
        /// </summary>
        public IDatabase Database => _db;


        public RedisCacheService(RedisSettings options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _settings = options;
            _config = ConfigurationOptions.Parse(_settings.Config);
            _log.Debug($@"Connecting to redis...");
            var watch = Stopwatch.StartNew();
            var task = ConnectionMultiplexer.ConnectAsync(_config);
            if (!task.Wait(_config.ConnectTimeout >= (int.MaxValue/2) ? int.MaxValue : _config.ConnectTimeout*2))
            {
                task.ContinueWith(x =>
                {
                    try
                    {
                        GC.KeepAlive(x.Exception);
                    }
                    catch
                    {
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException("Connect timeout");
            }
            watch.Stop();
            _log.Debug($@"Connect to redis took {watch.ElapsedMilliseconds} ms");
            _muxer = task.Result;
            _db = _muxer.GetDatabase();
        }


        #region GENERIC

        /// <summary>
        /// Транзакция записи/сохранения объекта в redis 
        /// Ссылки: https://redis.io/topics/transactions https://github.com/StackExchange/StackExchange.Redis/blob/master/Docs/Transactions.md 
        /// </summary>
        /// <typeparam name="T">Тип объекта</typeparam>
        /// <typeparam name="TKey">Тип идентификатора-ключа</typeparam>
        /// <param name="key">Идентификатор ключ объекта</param>
        /// <param name="expire">Срок хранения в мс</param>
        /// <param name="conditionBuilderAsync">Построитель условия успешного выполнения транзакции. <remarks>Должен вернуть текущую версию объекта из хранилища redis или null, если условие этого не требует, например когда условие NotExist. ВАЖНО!!! нельзя использовать объект транзакции для получения объектов из redis, т.к. все команды транзакции выполнятся атомарно в момент вызова .ExecuteAsync </remarks></param>
        /// <param name="resolverAsync">Синхронизатор состояния объекта, получает в параметре объект, если он существовал(или null) в redis и необходим по условию транзакции, обновляет/сводит объект и возвращает его актуальную версию</param>
        /// <returns></returns>
        public async Task<bool> AddOrUpdateTransAsync<T, TKey>(TKey key, TimeSpan expire,
            Func<IDatabase, TKey, ITransaction, Task<RedisValue>> conditionBuilderAsync, Func<T, Task<T>> resolverAsync)
            where T : class
        {
            var trans = _db.CreateTransaction();
            var json = await conditionBuilderAsync(_db, key, trans);
            T @object = null;
            if (json.HasValue)
            {
                try
                {
                    @object = JsonConvert.DeserializeObject<T>(json);
                }
                catch (Exception sex)
                {
                    _log.Error(
                        $@"Failed deserialize object to type {typeof(T)} in redis transaction for key {key} ex: {sex}");
                }
            }
            var set = trans.StringSetAsync(key.ToString(), JsonConvert.SerializeObject(await resolverAsync(@object)),
                expire);
            bool result = await trans.ExecuteAsync();
            return result;
        }

        #endregion

        #region Implementation of IRedisCacheService

        /// <inheritdoc />
        public async Task<bool> AddOrUpdateAsync<T, TKey>(TKey key, TimeSpan expire, Func<T, Task<T>> updaterAsync,
            int limit = 10) where T : class
        {
            bool result = false;

            // логика транзакции определяется в процессе
            result = await AddOrUpdateTransAsync<T, TKey>(
                key,
                expire,
                async (rdb, k, trans) =>
                {
                    // определяем существует ли ключ в кеше
                    var present = false;
                    present = await rdb.KeyExistsAsync(k.ToString(), CommandFlags.HighPriority);
                    if (present)
                    {
                        var @object = await rdb.StringGetAsync(k.ToString());
                        if (@object.HasValue)
                        {
                            // транзакция завершится успешно, только если в кеше существует ключ
                            trans.AddCondition(Condition.KeyExists(k.ToString()));
                            // и если за время выполнения транзакции объект в кеше не изменится
                            trans.AddCondition(Condition.StringEqual(k.ToString(), @object));
                            return @object;
                        }
                    }
                    // если ключа нет или не удалось получить по нему объект исходим из того что в кеше нет объекта
                    // транзакция завершится успешно, только если в кеше нет такого ключа
                    trans.AddCondition(Condition.KeyNotExists(k.ToString()));
                    return RedisValue.Null;
                },
                updaterAsync);

            if (result) return true;

            // если все транзакции оказались безуспешными, попробуем еще раз
            if (limit > 0)
            {
                return await AddOrUpdateAsync<T, TKey>(key, expire, updaterAsync, --limit);
            }
            // когда все попытки потрачены
            return false;
        }

        /// <inheritdoc />
        public async Task PutAsync<T, TKey>(TKey key, T obj) where T : class
        {
            try
            {
                var json = JsonConvert.SerializeObject(obj);
                await _db.StringSetAsync(key.ToString(), json, null, When.Always, CommandFlags.FireAndForget);
            }
            catch (Exception sex)
            {
                _log.Error($@"Failed put object: {typeof(T)} with id: {key} to redis db, ex: {sex}");
            }
        }

        /// <inheritdoc />
        public async Task PutAsync<T, TKey>(TKey key, T obj, TimeSpan expire) where T : class
        {
            try
            {
                var json = JsonConvert.SerializeObject(obj);
                await _db.StringSetAsync(key.ToString(), json, expire, When.Always, CommandFlags.FireAndForget);
            }
            catch (Exception sex)
            {
                _log.Error($@"Failed put object: {typeof(T)} with id: {key} to redis db, ex: {sex}");
            }
        }

        /// <inheritdoc />
        public async Task<T> GetAsync<T, TKey>(TKey key) where T : class
        {
            T result = null;
            try
            {
                bool exist = await _db.KeyExistsAsync(key.ToString());
                if (!exist) return null; // для скорости, т.к. часто кеш должен быть пуст
                var json = await _db.StringGetAsync(key.ToString());
                if (!json.HasValue) return null;
                result = JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception sex)
            {
                _log.Error($@"Failed get object: {typeof(T)} with id: {key} from redis db, ex: {sex}");
            }
            return result;
        }

        /// <inheritdoc />
        public async Task DeleteAsync<TKey>(TKey key)
        {
            await _db.KeyDeleteAsync(key.ToString(), CommandFlags.FireAndForget);
        }

        
        /// <inheritdoc />
        public async Task<bool> ExistsAsync<TKey>(TKey key)
        {
            return await _db.KeyExistsAsync(key.ToString(), CommandFlags.None);
        }
        

        #region Pub/Sub



        /// <inheritdoc />
        public IServer CreatePublisher()
        {
            EndPoint[] endpoints = _muxer.GetEndPoints();
            IServer result = null;
            foreach (var endpoint in endpoints)
            {
                var server = _muxer.GetServer(endpoint);
                if (server.IsSlave || !server.IsConnected) continue;
                if (result != null)
                    throw new InvalidOperationException("Requires exactly one master endpoint (found " + server.EndPoint +
                                                        " and " + result.EndPoint + ")");
                result = server;
            }
            if (result == null)
                throw new InvalidOperationException("Requires exactly one master endpoint (found none)");
            return result;
        }

        /// <inheritdoc />
        public ISubscriber CreateSubscriber()
        {
            return _muxer.GetSubscriber(true);
        }


        #endregion

        #region Lists


        /// <inheritdoc />
        public async Task<long> ListSizeAsync(string source)
        {
            long result = 0L;
            try
            {
                result = await _db.ListLengthAsync( source );
            }
            catch (Exception sex)
            {
                _log.Error( $@"Failed get size list id: {source} in redis db, ex: {sex}" );
            }
            return result;
        }
        

        /// <inheritdoc />
        public async Task<long> ListLeftPushAsync<T, TKey>(TKey key, T @object) where T : class
        {
            long result = 0;
            try
            {
                var json = JsonConvert.SerializeObject( @object );
                result = await _db.ListLeftPushAsync(key.ToString(), json, When.Always, CommandFlags.None);
            }
            catch ( Exception sex )
            {
                _log.Error( $@"Failed left push object: {typeof( T )} to list id: {key} in redis db, ex: {sex}" );
            }
            return result;
        }

        /// <inheritdoc />
        public async Task<long> ListRightPushAsync<T, TKey>(TKey key, T @object) where T : class
        {
            long result = 0;
            try
            {
                var json = JsonConvert.SerializeObject( @object );
                result = await _db.ListRightPushAsync( key.ToString(), json, When.Always, CommandFlags.None );
            }
            catch ( Exception sex )
            {
                _log.Error( $@"Failed right push object: {typeof( T )} to list id: {key} in redis db, ex: {sex}" );
            }
            return result;
        }

        /// <inheritdoc />
        public async Task<T> ListRightPopLeftPushAsync<T>(string source, string dest) where T : class
        {
            T result = null;
            try
            {
                var json = await _db.ListRightPopLeftPushAsync( source, dest, CommandFlags.None );
                if ( !json.HasValue ) return null;
                result = JsonConvert.DeserializeObject<T>( json );
            }
            catch ( Exception sex )
            {
                _log.Error( $@"Failed get object: {typeof( T )} from list: {source} from redis db, ex: {sex}" );
            }
            return result;
        }

        /// <inheritdoc />
        public async Task<long> ListLastRemoveAsync(string source, int count, RedisValue element)
        {
            long result = 0;
            try
            {
                result = await _db.ListRemoveAsync( source, element, count, CommandFlags.None );
            }
            catch ( Exception sex )
            {
                _log.Error( $@"Failed remove object id: {element} from list id: {source} in redis db, ex: {sex}" );
            }
            return result;
        }
        
        /// <inheritdoc />
        public async Task ListTrimAsync(string source, long start, long stop)
        {
            await _db.ListTrimAsync(source, start, stop, CommandFlags.FireAndForget);
        }
        
        #endregion

        #endregion
    }
}

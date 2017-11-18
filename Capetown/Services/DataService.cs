using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Capetown.Exceptions;
using Capetown.Models;
using Capetown.Models.Filters;
using Capetown.Models.Internal;
using Capetown.Models.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Nest;
using NLog;

namespace Capetown.Services
{
    /// <summary>
    /// Реализация сервиса доступа к данным
    /// </summary>
    public class DataService : IDataService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        
        private readonly IConfigurationRoot _rootConfig;
        private readonly IElasticSearchService _eService;
        private readonly IQueryService _queryService;
        private readonly IRedisCacheService _cache;
        private readonly IQueueService _queue;

        
        public DataService(IConfigurationRoot rootConfig,
            IElasticSearchService eService,
            IQueryService queryService,
            IRedisCacheService cache,
            IQueueService queue )
        {
            if (rootConfig == null) throw new ArgumentNullException(nameof(rootConfig));
            if (eService == null) throw new ArgumentNullException(nameof(eService));
            if (queryService == null) throw new ArgumentNullException(nameof(queryService));
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if ( queue == null ) throw new ArgumentNullException( nameof( queue ) );
            _rootConfig = rootConfig;
            _eService = eService;
            _queryService = queryService;
            _cache = cache;
            _queue = queue;
        }

        #region CRUD generic


        /// <summary>
        /// Получить объект из ES индекса. <remarks>Перед чтением из ES проверяем наличие в REDIS, и если там есть берем из REDIS</remarks>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TId"></typeparam>
        /// <param name="id"></param>
        /// <param name="indexKey"></param>
        /// <param name="ignoreRedis">Не смотреть в кеш, а только в ES</param>
        /// <param name="resolver">Резолвер объекта, если с ним необходимо что-то сделать, например нормализовать URL картинок</param>
        /// <returns></returns>
        public async Task<T> GetAsync<T, TId>(TId id, string indexKey, bool ignoreRedis = false,
            Action<T> resolver = null) where T : class
        {
            T entity = null;
            try
            {
                // в случае если объект только что был создан/обновлен мы получим актуальную версию, в ES near real-time может отдать старую версию
                entity = await _cache.GetAsync<T, TId>(id);
                if (entity != null) return entity;
            }
            catch (Exception sex)
            {
                _log.Error($@"Failed get {typeof(T)} from REDIS cache with id: {id} sex: {sex}");
            }
            try
            {

                entity = await _eService.ReadAsync<T, TId>(id, indexKey);
            }
            catch (Exception sex)
            {
                _log.Error($@"Failed get {typeof(T)} from ES index {indexKey} with id: {id} sex: {sex}");
            }
            resolver?.Invoke(entity);
            return entity;
        }

        /// <summary>
        /// Получить объект в версионной обёртке, напрямую из ES.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TId"></typeparam>
        /// <param name="id"></param>
        /// <param name="indexKey"></param>
        /// <returns></returns>
        public async Task<VersionedObject<T>> GetVersionedAsync<T, TId>(TId id, string indexKey) where T : class
        {
            VersionedObject<T> entity = null;
            try
            {
                entity = await _eService.GetAsync<T, TId>(id, indexKey);
            }
            catch (Exception sex)
            {
                _log.Error($@"Failed get {typeof(T)} from ES index {indexKey} with id: {id} sex: {sex}");
            }
            return entity;
        }


        /// <summary>
        /// Добавляет/обновляет в базе данных и в индексе сущности
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TId"></typeparam>
        /// <param name="entity"></param>
        /// <param name="selector"></param>
        /// <param name="updater"></param>
        /// <param name="id"></param>
        /// <param name="toLogString"></param>
        /// <param name="indexKey"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public async Task SaveOrUpdateAsync<T, TId>(
            T entity,
            Expression<Func<T, bool>> selector,
            Action<T> updater,
            Func<T, TId> id,
            Func<T, string> toLogString,
            string indexKey,
            long? version = null) where T : class
        {
            T exist;

            #region БД

            Stopwatch sw = new Stopwatch();
            sw.Start();
            using (var db = CapetownDbContext.GetInstance(_rootConfig))
            {
                try
                {
                    exist = await db.Set<T>().SingleOrDefaultAsync<T>(selector);
                }
                catch (Exception ex)
                {
                    _log.Error(
                        $@"Some wrong with database, failed select {typeof(T)} ({toLogString}) database error: {ex
                            .ToString()}");
                    throw;
                }
                if (exist == null)
                {
                    try
                    {
                        db.Add<T>(entity);
                        await db.SaveChangesAsync(CancellationToken.None); // не ждем
                    }
                    catch (Exception ex)
                    {
                        _log.Error($@"Failed insert {typeof(T)} ({toLogString}) to database error: {ex.ToString()}");
                    }
                }
                else
                {
                    try
                    {
                        updater(exist);
                        await db.SaveChangesAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _log.Error($@"Failed update {typeof(T)} ({toLogString}) to database error: {ex.ToString()}");
                        throw;
                    }
                }
            }
            sw.Stop();
            _log.Debug(
                $@"Perfomance database SaveOrUpdateAsync<{typeof(T)},{typeof(TId)}> done in {sw.ElapsedMilliseconds} ms");

            #endregion



            #region индекс ES

            exist = null;
            try
            {
                exist = await _eService.ReadAsync<T>(entity, indexKey);
            }
            catch (Exception ex)
            {
                // что-то с индексом, попробуем обновить некрасиво, если не удалось понять есть такой объект или нет
                _log.Error($@"Failed read {typeof(T)} (id = {id(entity)}) from index:{indexKey} error: {ex.ToString()}");

            }
            if (exist == null)
            {
                // add to index
                try
                {
                    await _eService.AddToIndexAsync<T, TId>(entity, indexKey, id(entity), version);
                }
                catch (Exception ex)
                {
                    // что-то с индексом
                    _log.Error($@"Failed add {typeof(T)} ({toLogString}) to index:{indexKey} error: {ex.ToString()}");
                }
            }
            else
            {
                // update in index
                try
                {
                    await _eService.UpdateInIndexAsync<T, TId>(entity, indexKey, id(entity));
                }
                catch (Exception ex)
                {
                    // что-то с индексом
                    _log.Error($@"Failed update {typeof(T)} ({toLogString}) to index:{indexKey} error: {ex.ToString()}");
                    throw;
                }
            }

            #endregion
        }

        /// <summary>
        /// Удалить объект из системы
        /// <remarks>В БД у объекта должна проставиться отметка об удалении в поле IsDelete</remarks>
        /// <remarks>Из индекса объект удаляется полностью</remarks>
        /// </summary>
        /// <typeparam name="T">Тип удаляемого объекта</typeparam>
        /// <typeparam name="TId">Тип идентификатора удаляемого объекта</typeparam>
        /// <param name="id">Идентификатор объекта</param>
        /// <param name="selector">Селектор критерия выбора объекта</param>
        /// <param name="updater">Обновлятор необходимых полей объекта</param>
        /// <param name="toLogString">Генератор строки для логов в контексте объекта</param>
        /// <param name="claimValidator">Валидатор подлинности субъекта запросившего удаление.<remarks>Если инициатор контекста запроса не совпадает с владельцем то выбрасываем исключение</remarks></param>
        /// <param name="indexKey">Ключ индекса ES</param>
        /// <returns></returns>
        public async Task DeleteAsync<T, TId>(TId id, Expression<Func<T, bool>> selector, Action<T> updater,
            Func<T, string> toLogString, Func<T, bool> claimValidator, string indexKey) where T : class
        {
            await DeleteAsync<T, TId>(id, selector, updater, toLogString, claimValidator, indexKey, null, null);
        }

        /// <summary>
        /// Удалить объект из системы
        /// </summary>
        /// <typeparam name="T">Тип удаляемого объекта</typeparam>
        /// <typeparam name="TId">Тип идентификатора удаляемого объекта</typeparam>
        /// <param name="id">Идентификатор объекта</param>
        /// <param name="selector">Селектор критерия выбора объекта</param>
        /// <param name="updater">Обновлятор необходимых полей объекта</param>
        /// <param name="toLogString">Генератор строки для логов в контексте объекта</param>
        /// <param name="claimValidator">Валидатор подлинности субъекта запросившего удаление.<remarks>Если инициатор контекста запроса не совпадает с владельцем то выбрасываем исключение</remarks></param>
        /// <param name="indexKey">Ключ индекса ES</param>
        /// <param name="filesSelector">Селектор медиа файлов объекта, которые необходимо обработать при удалении. Например нужно удалить все его фото из хранилища.</param>
        /// <param name="filesHandler">Обработчик действия с медиафайлами объекта.</param>
        /// <returns></returns>
        public async Task DeleteAsync<T, TId>(TId id, Expression<Func<T, bool>> selector, Action<T> updater,
            Func<T, string> toLogString, Func<T, bool> claimValidator, string indexKey, Action<T> filesSelector,
            Action filesHandler) where T : class
        {
            T exist;

            #region БД

            Stopwatch sw = new Stopwatch();
            sw.Start();
            using (var db = CapetownDbContext.GetInstance(_rootConfig))
            {
                try
                {
                    exist = db.Set<T>().SingleOrDefault<T>(selector); // синхронно
                }
                catch (Exception ex)
                {
                    _log.Error(
                        $@"Some wrong with database, failed select {typeof(T)} ({toLogString}) database error: {ex
                            .ToString()}");
                    throw;
                }
                if (exist != null)
                {
                    if (!claimValidator(exist))
                    {
                        throw new InvalidAuthorizeException($@"Try to unauthorize delete {typeof(T)} {toLogString}");
                    }
                    // обработка файлов объекта
                    if (filesSelector != null) filesSelector(exist);
                    try
                    {
                        updater(exist);
                        await db.SaveChangesAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _log.Error($@"Failed update {typeof(T)} ({toLogString}) to database error: {ex.ToString()}");
                        throw;
                    }
                }
            }
            sw.Stop();
            _log.Debug(
                $@"Perfomance database DeleteAsync<{typeof(T)},{typeof(TId)}> done in {sw.ElapsedMilliseconds} ms");

            #endregion

            #region delete in index

            exist = null;
            try
            {
                exist = await _eService.ReadAsync<T, TId>(id, indexKey);
                if (exist != null)
                {
                    if (!claimValidator(exist))
                    {
                        throw new InvalidAuthorizeException(
                            $@"Try to unauthorize delete {typeof(T)} {toLogString} from index:{indexKey}");
                    }
                    // обработка файлов объекта
                    if (filesSelector != null) filesSelector(exist);
                    // try to delete in index
                    try
                    {
                        await _eService.DeleteFromIndexAsync<T, TId>(exist, indexKey, id);
                    }
                    catch (Exception ex)
                    {
                        // что-то с индексом
                        _log.Error( $@"Failed delete {typeof(T)} ({toLogString}) from index:{indexKey} error: {ex.ToString()}");
                        throw; 
                    }
                }
            }
            catch (Exception ex)
            {
                // что-то с индексом
                _log.Error($@"Failed read {typeof(T)} ({toLogString}) from index:{indexKey} error: {ex.ToString()}");
                throw;
            }

            #endregion

            #region do it with object files

            if (filesHandler != null) filesHandler();

            #endregion
        }

        /// <summary>
        /// Удаляет тотально из БД и из индекса
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TId"></typeparam>
        /// <param name="id"></param>
        /// <param name="selector"></param>
        /// <param name="toLogString"></param>
        /// <param name="claimValidator">Валидатор подлинности субъекта запросившего удаление.<remarks>Если инициатор контекста запроса не совпадает с владельцем то выбрасываем исключение</remarks></param>
        /// <param name="indexKey"></param>
        /// <returns></returns>
        public async Task TotalDeleteAsync<T, TId>(TId id, Expression<Func<T, bool>> selector,
            Func<T, string> toLogString, Func<T, bool> claimValidator, string indexKey) where T : class
        {
            T exist;

            #region БД

            Stopwatch sw = new Stopwatch();
            sw.Start();
            using (var db = CapetownDbContext.GetInstance(_rootConfig))
            {
                try
                {
                    exist = await db.Set<T>().SingleOrDefaultAsync<T>(selector);
                }
                catch (Exception ex)
                {
                    _log.Error(
                        $@"Some wrong with database, failed select {typeof(T)} ({toLogString}) database error: {ex
                            .ToString()}");
                    throw;
                }
                if (exist != null)
                {
                    if (!claimValidator(exist))
                    {
                        throw new InvalidAuthorizeException(
                            $@"Try to unauthorize total delete {typeof(T)} {toLogString}");
                    }
                    try
                    {
                        db.Remove<T>(exist);
                        await db.SaveChangesAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _log.Error($@"Failed update {typeof(T)} ({toLogString}) to database error: {ex.ToString()}");
                        throw;
                    }
                }
            }
            sw.Stop();
            _log.Debug(
                $@"Perfomance database TotalDeleteAsync<{typeof(T)},{typeof(TId)}> done in {sw.ElapsedMilliseconds} ms");

            #endregion

            #region delete in index

            try
            {
                exist = await _eService.ReadAsync<T, TId>(id, indexKey);
                if (exist != null)
                {
                    if (!claimValidator(exist))
                    {
                        throw new InvalidAuthorizeException(
                            $@"Try to unauthorize total delete {typeof(T)} {toLogString} from index:{indexKey}");
                    }
                    // try to delete in index
                    try
                    {
                        await _eService.DeleteFromIndexAsync<T, TId>(exist, indexKey, id);
                    }
                    catch (Exception ex)
                    {
                        // что-то с индексом
                        _log.Error(
                            $@"Failed delete {typeof(T)} ({toLogString}) from index:{indexKey} error: {ex.ToString()}");
                    }
                }
            }
            catch (Exception ex)
            {
                // что-то с индексом
                _log.Error($@"Failed read {typeof(T)} ({toLogString}) from index:{indexKey} error: {ex.ToString()}");
            }

            #endregion
        }

        /// <summary>
        /// Удалет объект только в индексе ES
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TId"></typeparam>
        /// <param name="id"></param>
        /// <param name="toLogString"></param>
        /// <param name="claimValidator">Валидатор подлинности субъекта запросившего удаление.<remarks>Если инициатор контекста запроса не совпадает с владельцем то выбрасываем исключение</remarks></param>
        /// <param name="indexKey"></param>
        /// <returns></returns>
        public async Task DeleteOnlyInIndexAsync<T, TId>(TId id, Func<T, string> toLogString,
            Func<T, bool> claimValidator, string indexKey) where T : class
        {
            // delete in index
            try
            {
                T exist = await _eService.ReadAsync<T, TId>(id, indexKey);
                if (exist != null)
                {
                    if (!claimValidator(exist))
                    {
                        throw new InvalidAuthorizeException(
                            $@"Try to unauthorize delete {typeof(T)} {toLogString} only from index:{indexKey}");
                    }
                    // try to delete in index
                    try
                    {
                        await _eService.DeleteFromIndexAsync<T, TId>(exist, indexKey, id);
                    }
                    catch (Exception ex)
                    {
                        // что-то с индексом
                        _log.Error(
                            $@"Failed delete {typeof(T)} ({toLogString}) from index:{indexKey} error: {ex.ToString()}");
                    }
                }
            }
            catch (Exception ex)
            {
                // что-то с индексом
                _log.Error($@"Failed read {typeof(T)} ({toLogString}) from index:{indexKey} error: {ex.ToString()}");
            }
        }

        /// <summary>
        /// Сохранить или обновить объект только в ES. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TId"></typeparam>
        /// <param name="entity"></param>
        /// <param name="updater"></param>
        /// <param name="id"></param>
        /// <param name="toLogString"></param>
        /// <param name="indexKey"></param>
        /// <param name="expire"></param>
        /// <param name="ignoreRedis"></param>
        /// <param name="version">Версия, когда требуется свой контроль версионности</param>
        /// <returns></returns>
        public async Task SaveOrUpdateOnlyInIndexAsync<T, TId>(
            T entity,
            Func<T, T> updater,
            Func<T, TId> id,
            Func<T, string> toLogString,
            string indexKey,
            TimeSpan expire,
            bool ignoreRedis = false,
            long? version = null) where T : class
        {
            T exist = null;
            // если указана версия, принудительно обновление нужно делать через Index API ES
            if (version == null)
            {
                try
                {
                    // проверка существования в индексе, важно само наличие
                    exist = await _eService.ReadAsync<T>(entity, indexKey);
                }
                catch (Exception ex)
                {
                    // что-то с индексом, попробуем обновить некрасиво, если не удалось понять есть такой объект или нет
                    _log.Error(
                        $@"Failed read {typeof(T)} (id = {id(entity)}) from index:{indexKey} error: {ex.ToString()}");

                }
            }

            #region redis

            // сначала сохраняем в кеш, если в кеше на этот момент уже был объект, возможно его требуется обновить
            if (!ignoreRedis)
            {
                bool result = await _cache.AddOrUpdateAsync<T, TId>(
                    id(entity),
                    expire, // на сколько сохранить в кеше
                    (entityFromCache) =>
                    {
                        if (entityFromCache == null)
                        {
                            // в кеше чисто, просто сохраняем, если требуется можно обновить
                            if (updater != null) entity = updater(exist);
                            return Task.FromResult(entity);
                        }
                        // так, интересно, в кеше есть объект, обновим/синхронизируем по необходимости
                        if (updater != null) entity = updater(entityFromCache);
                        return Task.FromResult(entity);
                    });
                if (!result)
                {
                    _log.Error($@"Failed add {typeof(T)} ({toLogString}) to redis cache");
                    throw new Exception($@"Failed add {typeof(T)} ({toLogString}) to redis cache");
                }
            }

            #endregion

            #region elasticsearch

            // ES
            if (exist == null)
            {
                // add to index
                try
                {
                    await _eService.AddToIndexAsync<T, TId>(entity, indexKey, id(entity), version);
                }
                catch (Exception ex)
                {
                    // что-то с индексом
                    _log.Error($@"Failed add {typeof(T)} ({toLogString}) to index:{indexKey} error: {ex.ToString()}");
                    throw;
                }
            }
            else
            {
                // update in index
                try
                {
                    await _eService.UpdateInIndexAsync<T, TId>(entity, indexKey, id(entity), version);
                }
                catch (Exception ex)
                {
                    // что-то с индексом
                    _log.Error($@"Failed update {typeof(T)} ({toLogString}) to index:{indexKey} error: {ex.ToString()}");
                    throw;
                }
            }

            #endregion
        }


        public ICollection<T> ReturnObjects<T>(ISearchResponse<T> response) where T : class
        {
            return ReturnObjects<T>(response, null);
        }

        /// <summary>
        /// Извлечение коллекции объектов из результата поискового запроса
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="response"></param>
        /// <param name="resolver">Обработчик результатов, когда нужно что-то сделать дополнительно, например сделать красивые URL</param>
        /// <returns></returns>
        public ICollection<T> ReturnObjects<T>(ISearchResponse<T> response, Action<T> resolver) where T : class
        {
            if (response.Documents != null && response.Documents.Count > 0)
            {
                var results = response.Documents.ToList();
                if (resolver != null)
                {
                    foreach (T result in results)
                    {
                        resolver(result);
                    }
                }
                return results;
            }
            return new List<T>();
        }

        #endregion

        #region IDataService


        #region Store

        /// <inheritdoc />
        public async Task SaveOrUpdateStoreAsync(Store store)
        {
            store.RefreshUpdated();
            await SaveOrUpdateAsync<Store, string>(store,
                s => s.Id.Equals(store.Id),
                exist =>
                {
                    // обновление
                    if (!string.IsNullOrEmpty(store.Name)) exist.Name = store.Name;
                    if (store.Latitude != null && store.Longitude != null)
                    {
                        exist.Latitude = store.Latitude;
                        exist.Longitude = store.Longitude;
                    }

                    if (!string.IsNullOrEmpty(store.Description)) exist.Description = store.Description;
                    exist.Updated = DateTime.UtcNow;
                    if (store.IsDelete != null) exist.IsDelete = store.IsDelete;
                },
                s => s.Id,
                s => $@"id: {store.Id} name:{store.Name} owner:{store.OwnerId}",
                Constants.StoresIndexKey);

            await _queue.PushPublicationToQueueAsync(store);
        }
        
        /// <inheritdoc />
        public async Task DeleteStoreAsync(string id, long? subjectId)
        {
            await DeleteAsync<Store, string>(id,
                s => s.Id.Equals(id),
                exist =>
                {
                    exist.Updated = DateTime.UtcNow;
                    exist.IsDelete = DateTime.UtcNow;
                },
                s => $@"id: {id}",
                s => s.OwnerId == subjectId, // проверка что владелец истинный
                Constants.StoresIndexKey);
        }

        public async Task<Store> ReadStoreByIdAsync(string id)
        {
            Store result;
            using (var db = CapetownDbContext.GetInstance(_rootConfig))
            {
                result = await db.Stores.SingleOrDefaultAsync(s => s.Id.Equals(id));
            }
            return result;
        }

        
        /// <inheritdoc />
        public async Task<Store> GetStoreByIdAsync(string id)
        {
            return await GetAsync<Store, string>(id, Constants.StoresIndexKey);
        }
        /// <inheritdoc />
        public async Task<ICollection<Store>> ReadStoresAsync(IStoreFilter filter = null)
        {
            ICollection<Store> stores;
            using (var db = CapetownDbContext.GetInstance(_rootConfig))
            {
                var result = db.Set<Store>().AsQueryable();
                if (result == null) return null;
                if (filter == null) return result.ToList();
                if (filter.OwnerId != null) result = result.Where(s => s.OwnerId.Equals(filter.OwnerId));
                if (filter.Skip > 0) result = result.Skip(filter.Skip);
                if (filter.Limit > 0) result = result.Take(filter.Limit);

                switch (filter.Order)
                {
                    case StoreOrderBy.Name:
                        result = filter.OrderType == OrderType.Ascending
                            ? result.OrderBy(s => s.Name)
                            : result.OrderByDescending(s => s.Name);
                        break;
                    case StoreOrderBy.Updated:
                        result = filter.OrderType == OrderType.Ascending
                            ? result.OrderBy(s => s.Updated)
                            : result.OrderByDescending(s => s.Updated);
                        break;
                    case StoreOrderBy.Created:
                    default:
                        result = filter.OrderType == OrderType.Ascending
                            ? result.OrderBy(s => s.Created)
                            : result.OrderByDescending(s => s.Created);
                        break;
                }
                stores = await result.ToListAsync();
            }

            return stores;
        }
        /// <inheritdoc />
        public async Task<ICollection<Store>> SearchStoresAsync(StoreQuery query)
        {
            return
                ReturnObjects(
                    await
                        _eService.SearchAsync<Store>(Constants.StoresIndexKey,
                            index => _queryService.SimpleStoreQuery(query, index)));
        }
        /// <inheritdoc />
        public async Task<ICollection<Store>> SearchStoresByOwnerAsync(StoreQuery query)
        {
            return
                ReturnObjects(
                    await
                        _eService.SearchAsync<Store>(Constants.StoresIndexKey,
                            index => _queryService.SearchStoreByOwnerQuery(query, index)));
        }
        /// <inheritdoc />
        public async Task<ICollection<Store>> GeoSearchStoresAsync(StoreQuery query)
        {
            return
                ReturnObjects(
                    await
                        _eService.SearchAsync<Store>(Constants.StoresIndexKey,
                            index => _queryService.GeoStoreQuery(query, index)));
        }

        #endregion
        
        #endregion
    }
}

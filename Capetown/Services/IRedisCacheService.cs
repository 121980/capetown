using System;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Capetown.Services
{
    /// <summary>
    /// Кеширующий сервис хранения
    /// </summary>
    public interface IRedisCacheService
    {
        /// <summary>
        /// Когда транзакция нужна по месту использования — сохранить или обновить объект в redis на заданное время, используя транзакцию. После истечения времени хранеиния объект самоуничтожится.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="key"></param>
        /// <param name="expire"></param>
        /// <param name="conditionBuilderAsync"></param>
        /// <param name="resolverAsync"></param>
        /// <returns></returns>
        Task<bool> AddOrUpdateTransAsync<T,TKey>(TKey key, TimeSpan expire, Func<IDatabase, TKey, ITransaction, Task<RedisValue>> conditionBuilderAsync, Func<T, Task<T>> resolverAsync) where T : class;

        /// <summary>
        /// Сохранить или обновить объект в redis на заданное время, используя транзакцию. После истечения времени хранеиния объект самоуничтожится.
        /// </summary>
        /// <typeparam name="T">Тип объекта</typeparam>
        /// <typeparam name="TKey">Тип идентификатора ключа объекта</typeparam>
        /// <param name="key">Идентификатор ключа объекта</param>
        /// <param name="expire">Продолжительность хранения объекта в кеше</param>
        /// <param name="updaterAsync">Синхронизатор состояния объекта, получает в параметре объект, если он существовал в redis, обновляет/сводит объект и возвращает его актуальную версию</param>
        /// <param name="limit">Максимальное количество попыток транзакций, прежде чем вернуть false</param>
        /// <returns>true если successful| false если сохранить не удалось</returns>
        /// <remarks>Если сохранить не удалось – тогда требуется принять решение в вызывающем коде, что сделать, – как вариант запустить операцию повторно</remarks>
        Task<bool> AddOrUpdateAsync<T,TKey>(TKey key, TimeSpan expire, Func<T, Task<T>> updaterAsync, int limit = 10) where T : class;

        /// <summary>
        /// Положить объект в redis по указнному ключу, если объект в redis уже существовал, он перезапишется
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="key"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        Task PutAsync<T,TKey>(TKey key, T obj) where T : class;

        /// <summary>
        /// Положить объект в redis по указнному ключу на заданное время, если объект в redis уже существовал, он перезапишется
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="key"></param>
        /// <param name="obj"></param>
        /// <param name="expire">Время хранения объекта, в мс</param>
        /// <returns></returns>
        Task PutAsync<T,TKey>(TKey key, T obj, TimeSpan expire) where T : class;

        /// <summary>
        /// Получить объект из redis по заданному ключу
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<T> GetAsync<T,TKey>(TKey key) where T : class;

        /// <summary>
        /// Удалить объект по заданному ключу
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        Task DeleteAsync<TKey>( TKey key );
        /// <summary>
        /// Проверка существования объекта с ключом <see cref="key">key</see>
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<bool> ExistsAsync<TKey>(TKey key);

        #region Pub/Sub

        /// <summary>
        /// Создает публикатора
        /// </summary>
        /// <returns></returns>
        IServer CreatePublisher();

        /// <summary>
        /// Создает подписчика
        /// </summary>
        /// <returns></returns>
        ISubscriber CreateSubscriber();

        #endregion

        #region Lists
        /// <summary>
        /// Возвращает длину списка
        /// </summary>
        /// <param name="source">Ключ под которым хранится список</param>
        /// <returns></returns>
        Task<long> ListSizeAsync(string source);

        /// <summary>
        /// Добавить объект в начало списка(слева начало, справа конец)
        /// </summary>
        /// <remarks>https://redis.io/commands/lpush</remarks>
        /// <typeparam name="T">Тип объекта</typeparam>
        /// <typeparam name="TKey">Тип ключа объекта</typeparam>
        /// <param name="key">Список, в который добавляем</param>
        /// <param name="object">Объект</param>
        /// <returns>Длина списка после</returns>
        Task<long> ListLeftPushAsync<T, TKey>(TKey key, T @object) where T : class;

        /// <summary>
        /// Добавить объект в конец списка(слева начало, справа конец)
        /// </summary>
        /// <remarks>https://redis.io/commands/rpush</remarks>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="key"></param>
        /// <param name="object"></param>
        /// <returns>Длина списка после</returns>
        Task<long> ListRightPushAsync<T, TKey>(TKey key, T @object) where T : class;

        /// <summary>
        /// Атомарно получить один объект из конца списка и добавить его же в новый список
        /// </summary>
        /// <remarks>https://redis.io/commands/rpoplpush</remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        /// <returns></returns>
        Task<T> ListRightPopLeftPushAsync<T>(string source, string dest) where T : class;

        /// <summary>
        /// Удаляет из списка <see cref="count">count</see> первых объектов <see cref="element">element</see>
        /// </summary>
        /// <remarks>https://redis.io/commands/lrem</remarks>
        /// <param name="source"></param>
        /// <param name="count">Количество и направление удаления, см. 
        ///     <remarks> 
        ///         count > 0: Remove elements equal to value moving from head to tail.
        ///         count > 0: Remove elements equal to value moving from tail to head.
        ///         count = 0: Remove all elements equal to value.
        ///     </remarks>
        /// </param>
        /// <param name="element"></param>
        /// <returns>Количество удалённых объектов</returns>
        Task<long> ListLastRemoveAsync(string source, int count, RedisValue element);

        /// <summary>
        /// Обрезка списка
        /// <remarks>https://redis.io/commands/ltrim</remarks>
        /// </summary>
        /// <param name="source"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        /// <returns></returns>
        Task ListTrimAsync(string source, long start, long stop);

        #endregion

    }
}

using System;
using System.Threading.Tasks;
using Capetown.Models.Internal;
using Nest;

namespace Capetown.Services
{
    /// <summary>
    /// Интерфейс работы с Elastic Search
    /// </summary>
    public interface IElasticSearchService
    {
        /// <summary>
        /// Резолвер имен индексов по ключам-константам
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        string IndexName(string key);

        #region CRUD generic-операции

        /// <summary>
        /// Получить объект из индекса ES
        /// </summary>
        /// <typeparam name="T">Класс индексируемого объекта</typeparam>
        /// <param name="entity">Искомый объект<remarks>У объекта должно быть поле Id</remarks></param>
        /// <param name="indexKey">Ключ индекса в который добавляется объект</param>
        /// <returns>Объект</returns>
        T Read<T>( T entity, string indexKey ) where T: class;
        /// <summary>
        /// Получить объект из индекса ES. Асинхронная версия.
        /// </summary>
        /// <typeparam name="T">Класс индексируемого объекта</typeparam>
        /// <typeparam name="TKey">Тип данных идентификатора индексируемого объекта</typeparam>
        /// <param name="id">Идентификатор объекта</param>
        /// <param name="indexKey">Ключ индекса в который добавляется объект. </param>
        /// <returns>Объект</returns>
        Task<T> ReadAsync<T, TKey>( TKey id, string indexKey ) where T : class;

        /// <summary>
        /// Получить объект из индекса, в версионной обертке
        /// </summary>
        /// <typeparam name="T">Тип объекта</typeparam>
        /// <typeparam name="TKey">Тип идентификатора ключа</typeparam>
        /// <param name="id">Идентификатор</param>
        /// <param name="indexKey">Ключ индекса в который добавляется объект. </param>
        /// <returns>Объект из индекса, в версионной обертке</returns>
        Task<VersionedObject<T>> GetAsync<T, TKey>(TKey id, string indexKey) where T : class;

        /// <summary>
        /// Получить объект из индекса ES. Асинхронная версия.
        /// </summary>
        /// <typeparam name="T">Класс индексируемого объекта</typeparam>
        /// <param name="entity">Искомый объект<remarks>У объекта должно быть поле Id</remarks></param>
        /// <param name="indexKey">Ключ индекса в который добавляется объект</param>
        /// <returns>Объект T</returns>
        Task<T> ReadAsync<T>( T entity, string indexKey ) where T : class;

        /// <summary>
        /// Добавляет объект в индекс ES
        /// </summary>
        /// <typeparam name="T">Класс индексируемого объекта</typeparam>
        /// <typeparam name="TKey">Тип данных идентификатора индексируемого объекта</typeparam>
        /// <param name="entity">Объект, подлежащий индексированию</param>
        /// <param name="indexKey">Ключ индекса в который добавляется объект</param>
        /// <param name="id">Идентификатор объекта</param>
        /// <param name="version">Версия объекта для soft locking</param>
        void AddToIndex<T,TKey>(T entity, string indexKey, TKey id, long? version = null) where T : class;

        /// <summary>
        /// Добавляет объект в индекс ES, асинхронная версия
        /// </summary>
        /// <typeparam name="T">Класс индексируемого объекта</typeparam>
        /// <typeparam name="TKey">Тип данных идентификатора индексируемого объекта</typeparam>
        /// <param name="entity">Объект, подлежащий индексированию</param>
        /// <param name="indexKey">Ключ индекса в который добавляется объект</param>
        /// <param name="id">Идентификатор объекта</param>
        /// <param name="version">Версия объекта для soft locking</param>
        Task AddToIndexAsync<T, TKey>(T entity, string indexKey, TKey id, long? version = null) where T : class;

        /// <summary>
        /// Обновляет объект в индексе ES
        /// <remarks>Update API не поддерживает external версионирование</remarks>
        /// </summary>
        /// <typeparam name="T">Класс индексируемого объекта</typeparam>
        /// <typeparam name="TKey">Тип данных идентификатора индексируемого объекта</typeparam>
        /// <param name="entity">Объект, подлежащий индексированию</param>
        /// <param name="indexKey">Ключ индекса в который добавляется объект</param>
        /// <param name="id">Идентификатор объекта</param>
        /// <param name="version">Версия объекта для soft locking</param>
        void UpdateInIndex<T, TKey>(T entity, string indexKey, TKey id, long? version = null) where T : class;

        /// <summary>
        /// Обновляет объект в индексе ES, асинхронная версия
        /// <remarks>Update API не поддерживает external версионирование</remarks>
        /// </summary>
        /// <typeparam name="T">Класс индексируемого объекта</typeparam>
        /// <typeparam name="TKey">Тип данных идентификатора индексируемого объекта</typeparam>
        /// <param name="entity">Объект, подлежащий индексированию</param>
        /// <param name="indexKey">Ключ индекса в который добавляется объект</param>
        /// <param name="id">Идентификатор объекта</param>
        /// <param name="version">Версия объекта для soft locking</param>
        Task UpdateInIndexAsync<T, TKey>(T entity, string indexKey, TKey id, long? version = null) where T : class;

        /// <summary>
        /// Удаляет объект из индекса ES
        /// </summary>
        /// <typeparam name="T">Класс индексируемого объекта</typeparam>
        /// <typeparam name="TKey">Тип данных идентификатора индексируемого объекта</typeparam>
        /// <param name="entity">Объект, подлежащий индексированию</param>
        /// <param name="indexKey">Ключ индекса в который добавляется объект</param>
        /// <param name="id">Идентификатор объекта</param>
        /// <param name="version">Версия объекта для soft locking</param>
        void DeleteFromIndex<T, TKey>(T entity, string indexKey, TKey id, long? version = null) where T : class;

        /// <summary>
        /// Удаляет объект из индекса ES, асинхронная версия
        /// </summary>
        /// <typeparam name="T">Класс индексируемого объекта</typeparam>
        /// <typeparam name="TKey">Тип данных идентификатора индексируемого объекта</typeparam>
        /// <param name="entity">Объект, подлежащий индексированию</param>
        /// <param name="indexKey">Ключ индекса в который добавляется объект</param>
        /// <param name="id">Идентификатор объекта</param>
        /// <param name="version">Версия объекта для soft locking</param>
        Task DeleteFromIndexAsync<T, TKey>(T entity, string indexKey, TKey id, long? version = null) where T : class;
        /// <summary>
        /// Выполняет поиск объектов в ES
        /// </summary>
        /// <typeparam name="T">Класс объекта</typeparam>
        /// <param name="indexKey">Ключ индекса в который добавляется объект</param>
        /// <param name="queryBuilder">Генератор запроса</param>
        /// <returns>Коллекция найденных объектов</returns>
        ISearchResponse<T> Search<T>(string indexKey, Func<string, SearchDescriptor<T>> queryBuilder ) where T: class;
        /// <summary>
        /// Выполняет поиск объектов в ES. Асинхронная версия
        /// </summary>
        /// <typeparam name="T">Класс объекта</typeparam>
        /// <param name="indexKey">Ключ индекса в который добавляется объект</param>
        /// <param name="queryBuilder">Генератор запроса</param>
        /// <returns>Коллекция найденных объектов</returns>
        Task<ISearchResponse<T>> SearchAsync<T>( string indexKey, Func<string, SearchDescriptor<T>> queryBuilder ) where T : class;

        #endregion
        
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Capetown.Models;
using Capetown.Models.Queries;


namespace Capetown.Services
{
    public interface IDataService
    {

        #region Stores

        /// <summary>
        /// Сохранить/обновить
        /// </summary>
        /// <param name="store"></param>
        /// <returns></returns>
        Task SaveOrUpdateStoreAsync( Store store );
        
        /// <summary>
        /// Удалить в хранилище
        /// </summary>
        /// <param name="id"></param>
        /// <param name="subjectId"></param>
        /// <returns></returns>
        Task DeleteStoreAsync( string id, long? subjectId );
        
        /// <summary>
        /// Прочитать из БД
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<Store> ReadStoreByIdAsync( string id );

        /// <summary>
        /// Получить из индекса ES
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<Store> GetStoreByIdAsync(string id);
        
        /// <summary>
        /// Найти по запросу
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        Task<ICollection<Store>> SearchStoresAsync( StoreQuery query );
        /// <summary>
        /// Найти по владельцу
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        Task<ICollection<Store>> SearchStoresByOwnerAsync( StoreQuery query );
        /// <summary>
        /// Найти по гео-запросу
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        Task<ICollection<Store>> GeoSearchStoresAsync( StoreQuery query );

        #endregion
        
    }
}

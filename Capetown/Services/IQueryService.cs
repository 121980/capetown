using System;
using System.Collections.Generic;
using Capetown.Models;
using Capetown.Models.Queries;
using Nest;

namespace Capetown.Services
{
    /// <summary>
    /// Интерфейс интерпритатора API запросов в запросы к ES
    /// </summary>
    public interface IQueryService
    {
        
        #region Stores

        SearchDescriptor<Store> SimpleStoreQuery( StoreQuery query, string index );

        SearchDescriptor<Store> SearchStoreByOwnerQuery( StoreQuery query, string index );

        SearchDescriptor<Store> GeoStoreQuery( StoreQuery query, string index );

        #endregion
        
    }
}

using System;
using Newtonsoft.Json;

namespace Capetown.Models.Filters
{
    public interface IStoreFilter
    {
        /// <summary>
        /// Идентификатор продавца
        /// </summary>
        long? OwnerId { get; set; }

        /// <summary>
        /// Сколько возвращать в результате
        /// </summary>
        int Limit { get; set; }
        /// <summary>
        /// Начиная с какой позиции возвращать результаты
        /// </summary>
        int Skip { get; set; }
        /// <summary>
        /// Порядок сортировки результатов
        /// </summary>
        StoreOrderBy Order { get; set; }
        /// <summary>
        /// Направление сортировки
        /// </summary>
        OrderType OrderType { get; set; }
    }

    /// <summary>
    /// Тип сортировки
    /// </summary>
    public enum OrderType
    {
        /// <summary>
        /// По возрастанию
        /// </summary>
        [JsonProperty]
        Ascending = 0,
        /// <summary>
        /// По убыванию
        /// </summary>
        [JsonProperty]
        Descending = 1
    }

    /// <summary>
    /// Тип сортировки
    /// </summary>
    public enum StoreOrderBy
    {
        #region Общие

        /// <summary>
        /// По весу соответствия запросу <remarks>Применимо только для ElasticSearch</remarks>
        /// </summary>
        [JsonProperty]
        Score = 0,
        /// <summary>
        /// По порядку расположения в индексе <remarks>Применимо только для ElasticSearch</remarks>
        /// </summary>
        [JsonProperty]
        Index = 1,
        /// <summary>
        /// По дате создания
        /// </summary>
        [JsonProperty]
        Created = 2,
        /// <summary>
        /// По дате обновления
        /// </summary>
        [JsonProperty]
        Updated = 3,

        #endregion

        /// <summary>
        /// По названию
        /// </summary>
        [JsonProperty]
        Name = 4,
        /// <summary>
        /// По расстоянию от заданной точки
        /// </summary>
        [JsonProperty]
        Distance = 7
    }

}

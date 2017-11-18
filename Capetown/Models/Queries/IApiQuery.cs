using System;
using System.ComponentModel;
using Newtonsoft.Json;

namespace Capetown.Models.Queries
{
    /// <summary>
    /// Интерфейс запроса API
    /// </summary>
    public interface IApiQuery
    {
        /// <summary>
        /// Стандартный запрос
        /// <remarks>Зависит от контекста, в каждом дочернем класе имеет свой смысл</remarks>
        /// </summary>
        [JsonProperty( "query", NullValueHandling = NullValueHandling.Ignore )]
        string Query { get; set; }
        /// <summary>
        /// Сколько пропустить для вывода результатов
        /// </summary>
        [JsonProperty( "skip", DefaultValueHandling = DefaultValueHandling.Populate )]
        [DefaultValue( 0 )]
        int Skip { get; set; }
        /// <summary>
        /// Сколько возвращать
        /// </summary>
        [JsonProperty( "limit", DefaultValueHandling = DefaultValueHandling.Populate )]
        [DefaultValue( Constants.DefaultLimit )]
        int Limit { get; set; }
    }
}

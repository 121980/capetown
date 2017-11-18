using System;
using System.ComponentModel;
using Newtonsoft.Json;

namespace Capetown.Models.Queries
{
    /// <summary>
    /// Базовый класс для запросов в нотации API
    /// </summary>
    [JsonObject]
    public class BaseQuery : IApiQuery
    {
        /// <inheritdoc />
        [JsonProperty( "query", NullValueHandling = NullValueHandling.Ignore )]
        public string Query { get; set; }

        /// <inheritdoc />
        [JsonProperty( "skip", DefaultValueHandling = DefaultValueHandling.Populate )]
        [DefaultValue( 0 )]
        public int Skip { get; set; }
        
        /// <inheritdoc />
        [JsonProperty( "limit", DefaultValueHandling = DefaultValueHandling.Populate )]
        [DefaultValue( Constants.DefaultLimit )]
        public int Limit { get; set; }
    }
}

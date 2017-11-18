using System;
using System.ComponentModel;
using Capetown.Models.Filters;
using Newtonsoft.Json;

namespace Capetown.Models.Queries
{
    [JsonObject]
    public class StoreQuery : BaseQuery
    {

        [JsonProperty( "owner_id", NullValueHandling = NullValueHandling.Ignore )]
        public long? OwnerId { get; set; }
        
        #region Geo

        /// <summary>
        /// Прямоугольник ограничивающий выборку точек по координатам
        /// </summary>
        [JsonProperty("geo_bound")]
        public GeoBound Bound { get; set; }
        /// <summary>
        /// Окружность, ограничивающая выборку точек по координатам
        /// </summary>
        [JsonProperty("geo_circle")]
        public GeoCircle Circle { get; set; }

        #endregion

        
        #region по дате создания

        [JsonProperty( "from", NullValueHandling = NullValueHandling.Ignore )]
        public DateTime? From { get; set; }

        [JsonProperty( "to", NullValueHandling = NullValueHandling.Ignore )]
        public DateTime? To { get; set; }

        #endregion

        /// <summary>
        /// Сортировка выдачи <remarks>Если не указать, значение по умолчанию Created</remarks>
        /// </summary>
        [JsonProperty( "order_by", DefaultValueHandling = DefaultValueHandling.Populate )]
        [DefaultValue( StoreOrderBy.Created)]
        public StoreOrderBy? Sort { get; set; }
        /// <summary>
        /// Направление сортировки <remarks>Если не указать, значение по умолчанию по возрастанию Ascending</remarks>
        /// </summary>
        [JsonProperty( "order_type", DefaultValueHandling = DefaultValueHandling.Populate )]
        [DefaultValue( OrderType.Descending )]
        public OrderType? SortType { get; set; }

    }

    [JsonObject]
    public class GeoBound
    {
        [JsonProperty( "top_left" )]
        public Location TopLeft { get; set; }

        [JsonProperty( "bottom_right" )]
        public Location BottomRight { get; set; }
    }

    [JsonObject]
    public class GeoCircle
    {
        /// <summary>
        /// Центр круга
        /// </summary>
        [JsonProperty( "center" )]
        public Location Center { get; set; }
        /// <summary>
        /// Радиус, км
        /// </summary>
        [JsonProperty( "radius" )]
        public double Radius { get; set; }
    }
    
}

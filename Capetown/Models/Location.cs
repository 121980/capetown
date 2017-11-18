using System;
using Nest;
using Newtonsoft.Json;

namespace Capetown.Models
{
    /// <summary>
    /// Координаты
    /// </summary>
    [JsonObject]
    public class Location
    {
        /// <summary>
        /// GEO Широта
        /// </summary>
        [JsonProperty( "lat" )]
        public double? Latitude { get; set; }

        /// <summary>
        /// GEO Долгота
        /// </summary>
        [JsonProperty( "lon" )]
        public double? Longitude { get; set; }
    }

    public static class LocationHelper
    {
        public static GeoLocation ToGeoLocation(this Location location)
        {
            if (location.Latitude == null || location.Longitude == null) return null;
            return new GeoLocation(location.Latitude.Value, location.Longitude.Value);
        }
    }
}

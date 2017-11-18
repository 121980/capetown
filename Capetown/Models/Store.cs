using System;
using Newtonsoft.Json;

namespace Capetown.Models
{
    /// <summary>
    /// Какой-то объект
    /// </summary>
    [JsonObject]
    public class Store
    {
        [JsonProperty("id")]
        public string Id { get; set; }


        [JsonProperty("oid")]
        public long? OwnerId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        #region Geo location

        private Location _location;

        /// <summary>
        /// GEO Широта
        /// </summary>
        [JsonProperty("lat")]
        public double? Latitude
        {
            get
            {
                return _location?.Latitude;
            }
            set
            {
                if (_location == null) _location = new Location();
                if (value != null) _location.Latitude = value.Value;
            }
        }

        /// <summary>
        /// GEO Долгота
        /// </summary>
        [JsonProperty("lon")]
        public double? Longitude
        {
            get
            {
                return _location?.Longitude;
            }
            set
            {
                if (_location == null) _location = new Location();
                if (value != null) _location.Longitude = value.Value;
            }
        }


        [JsonProperty("location")]
        public Location Location
        {
            get { return _location; }
            set { _location = value; }
        }

        #endregion

        /// <summary>
        /// Отметка+дата удаления
        /// </summary>
        [JsonProperty("is_delete")]
        public DateTime? IsDelete { get; set; }

        /// <summary>
        /// Описание магазина
        /// </summary>
        [JsonProperty("desc")]
        public string Description { get; set; }
        

        /// <summary>
        /// Дата последнего обновления
        /// </summary>
        [JsonProperty("updated")]
        public DateTime? Updated { get; set; }

        /// <summary>
        /// Дата создания в системе
        /// </summary>
        [JsonProperty("created")]
        public DateTime Created { get; set; }
    }

    /// <summary>
    /// Вспомогательные методы
    /// </summary>
    public static class StoreHelpers
    {
        /// <summary>
        /// Обновляет дату последнего обновления
        /// </summary>
        /// <param name="store"></param>
        public static void RefreshUpdated(this Store store)
        {
            store.Updated = DateTime.UtcNow;
        }
    }
}

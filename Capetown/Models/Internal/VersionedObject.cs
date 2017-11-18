using System;
using Newtonsoft.Json;

namespace Capetown.Models.Internal
{
    /// <summary>
    /// Контейнер для хранения объектов, учитывающий версионность состояния объекта
    /// </summary>
    /// <typeparam name="T">Тип объекта</typeparam>
    public interface IVersionedObject<T> where T: class
    {
        /// <summary>
        /// Объект
        /// </summary>
        [JsonProperty( "source" )]
        T Source { get; set; }
        /// <summary>
        /// Версия объекта, для optimistic locking
        /// </summary>
        [JsonProperty( "version" )]
        long Version { get; set; }
    }


    public class VersionedObject<T> : IVersionedObject<T> where T: class
    {
        #region Implementation of ICacheGetResponse<T>

        /// <inheritdoc />
        [JsonProperty( "source" )]
        public T Source { get; set; }

        /// <inheritdoc />
        [JsonProperty( "version" )]
        public long Version { get; set; }

        #endregion
    }
}

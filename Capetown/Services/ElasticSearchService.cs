using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using Capetown.Models;
using Capetown.Models.Internal;
using Elasticsearch.Net;
using Nest;
using NLog;

namespace Capetown.Services
{
    public class ElasticSearchService : IElasticSearchService
    {

        private ElasticClient _elastic;

        private ConnectionSettings _connectionSettings;

        private InstallEsDataSettings _settings;

        private static Logger _log = LogManager.GetCurrentClassLogger();

        public ElasticSearchService(InstallEsDataSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _settings = settings;
            // создаём клиента
            if (_settings.Cluster == null || _settings.Cluster.Length == 0)
            {
                throw new Exception($@"Cluster nodes not specified in elasticsearch application settings" );
            }

            Uri[] nodes = _settings.Cluster.Select(uri => new Uri(uri)).ToArray();
            var pool = new SniffingConnectionPool(nodes);
            _connectionSettings = new ConnectionSettings(pool);
            _connectionSettings.EnableTcpKeepAlive(new TimeSpan(1, 0, 0), new TimeSpan(0, 30, 0));
            _connectionSettings.SniffOnStartup();
            _connectionSettings.MaximumRetries(5);
            _connectionSettings.DisablePing();
            _elastic = new ElasticClient(_connectionSettings);
        }

        /// <summary>
        /// Определяет имя индекса по константам ключам
        /// </summary>
        /// <param name="key">Ключ индекса</param>
        /// <returns></returns>
        public string IndexName(string key)
        {
            var result = _settings.Indices.SingleOrDefault(inx => inx.Key.Equals(key));
            if (result == null)
            {
                throw new Exception($@"Not specified name for index key: {key} in elasticsearch application settings");
            }
            return result.Name;
        }

        #region CRUD операции с индексами

        /// <inheritdoc />
        public T Read<T>(T entity, string indexKey) where T : class
        {
            string index = IndexName(indexKey);
            var response = _elastic.Get<T>(new Id(entity), gr => gr.Index(index).Type<T>());
            if (response == null || !response.Found)
            {
                if ( response != null )
                {
                    throw new Exception( $"Failed get {typeof( T )} (id = {new Id( entity ).GetString( _connectionSettings )}) from index: {index}, debug info: {response.DebugInformation} server error: {response.ServerError}" );
                }
                throw new Exception($@"Failed get {typeof(T)} (id = {new Id( entity ).GetString( _connectionSettings )}) from index {index}");
            }
            return response.Source;
        }

        /// <inheritdoc />
        public async Task<T> ReadAsync<T, TKey>(TKey id, string indexKey) where T : class
        {
            string index = IndexName(indexKey);
            string sid = null;
            long? lid = null;
            Guid? guid = null;
            try
            {
                sid = (string) ((object) id);
            }
            catch (Exception)
            {
                _log.Debug($@"");
            }
            try
            {
                lid = (long) ((object) id);
            }
            catch (Exception)
            {
            }
            try
            {
                guid = (Guid) ((object) id);
            }
            catch (Exception)
            {
            }
            if (string.IsNullOrEmpty(sid) && (lid == null || lid == 0) && (guid == null || guid == Guid.Empty))
            {
                throw new Exception($@"Param TKey must be one of: string, long or Guid types. Cast type {typeof(TKey)} exception.");
            }
            IGetResponse<T> response = null;
            if (!string.IsNullOrEmpty(sid))
            {
                response = await _elastic.GetAsync<T>( new Id( sid ), gr => gr.Index( index ).Type<T>() );
            } else if (lid != null && lid != 0)
            {
                response = await _elastic.GetAsync<T>( new Id( lid.ToString() ), gr => gr.Index( index ).Type<T>() );
            } else if(guid != null && guid != Guid.Empty)
            {
                response = await _elastic.GetAsync<T>( new Id( guid.ToString() ), gr => gr.Index( index ).Type<T>() );
            } else
            {
                response = await _elastic.GetAsync<T>( new Id( id ), gr => gr.Index( index ).Type<T>() );
            }
            if ( response == null || !response.Found )
            {
                if ( response != null )
                {
                    throw new Exception( $"Failed get {typeof( T )} (id = {id}) from index {index}, debug info: {response.DebugInformation}, server error: {response.ServerError}" );
                }
                throw new Exception( $@"Failed get {typeof( T )} (id = {id}) from index {index}" );
            }
            return response.Source;
        }
        
        /// <inheritdoc />
        public async Task<VersionedObject<T>> GetAsync<T, TKey>(TKey id, string indexKey) where T : class
        {
            string index = IndexName( indexKey );
            string sid = null;
            long? lid = null;
            Guid? guid = null;
            try
            {
                sid = ( string )( ( object )id );
            }
            catch ( Exception )
            {
                _log.Debug( $@"" );
            }
            try
            {
                lid = ( long )( ( object )id );
            }
            catch ( Exception )
            {
            }
            try
            {
                guid = ( Guid )( ( object )id );
            }
            catch ( Exception )
            {
            }
            if ( string.IsNullOrEmpty( sid ) && ( lid == null || lid == 0 ) && ( guid == null || guid == Guid.Empty ) )
            {
                throw new Exception( $@"Param TKey must be one of: string, long or Guid types. Cast type {typeof( TKey )} exception." );
            }
            IGetResponse<T> response = null;
            if ( !string.IsNullOrEmpty( sid ) )
            {
                response = await _elastic.GetAsync<T>( new Id( sid ), gr => gr.Index( index ).Type<T>() );
            }
            else if ( lid != null && lid != 0 )
            {
                response = await _elastic.GetAsync<T>( new Id( lid.ToString() ), gr => gr.Index( index ).Type<T>() );
            }
            else if ( guid != null && guid != Guid.Empty )
            {
                response = await _elastic.GetAsync<T>( new Id( guid.ToString() ), gr => gr.Index( index ).Type<T>() );
            }
            else
            {
                response = await _elastic.GetAsync<T>( new Id( id ), gr => gr.Index( index ).Type<T>() );
            }
            if (!response.IsValid)
            {
                throw new Exception($@"Failed get {typeof( T )}(id = {id}) debug info: {response.DebugInformation} server error: {response.ServerError} orig.ex: {response.OriginalException}", response.OriginalException );
            }
            _log.Debug( $@"ES GetAsync<{typeof( T )},{typeof( TKey )}> (id = {id}) from index {index}, debug info: {response.DebugInformation} server error: {response.ServerError}" );
            if (response.Source == null) return null;
            VersionedObject<T> result = new VersionedObject<T>()
            {
                Source = response.Source,
                Version = response.Version
            };
            return result;
        }
        
        /// <inheritdoc />
        public async Task<T> ReadAsync<T>( T entity, string indexKey ) where T : class
        {
            string index = IndexName( indexKey );
            var response = await _elastic.GetAsync<T>( new Id( entity ), gr => gr.Index( index ).Type<T>() );
            if ( response == null || !response.Found )
            {
                if ( response != null )
                {
                    throw new Exception( $"Failed read {typeof( T )} (id = {new Id( entity ).GetString( _connectionSettings )}) from index {index} info: {response.DebugInformation} server error: {response.ServerError}" );
                }
                throw new Exception( $@"Failed read {typeof( T )} (id = {new Id( entity ).GetString(_connectionSettings)}) from index {index}" );
            }
            _log.Debug($@"ES ReadAsync<{typeof( T )}> (id = {new Id( entity ).GetString( _connectionSettings )}) from index {index}, debug info: {response.DebugInformation} server error: {response.ServerError}" );
            return response.Source;
        }

        /// <inheritdoc />
        [Obsolete("Strongly recommended to use Async version for best performance")]
        public void AddToIndex<T,TKey>(T entity, string indexKey, TKey id, long? version = null) where T : class
        {
            string index = IndexName(indexKey);
            IIndexResponse response = null;
            if ( version != null && version > -1 )
            {
                response = _elastic.Index( entity, ir => ir.Index( index ).Type<T>().Version( version.Value ) );
            }
            else
            {
                response = _elastic.Index( entity, ir => ir.Index( index ).Type<T>() );
            }
            if (response == null || (response.Result != Result.Created && response.Result != Result.Updated && response.Result != Result.Noop))
            {
                if ( response != null )
                {
                    throw new Exception( $"Failed index {typeof( T )}(id = {id}) in index {index}, debug info: {response.DebugInformation}, server error: {response.ServerError}" );
                }
                throw new Exception( $"Failed index {typeof( T )}(id = {id}) in index {index}" );
            }
        }

        /// <inheritdoc />
        public async Task AddToIndexAsync<T, TKey>(T entity, string indexKey, TKey id, long? version = null) where T : class
        {
            string index = IndexName( indexKey );
            IIndexResponse response = null;
            if (version != null && version > -1)
            {
                response = await _elastic.IndexAsync( entity, ir => ir.Index( index ).Type<T>().Version(version.Value).VersionType(VersionType.External) );
            }
            else
            {
                response = await _elastic.IndexAsync( entity, ir => ir.Index( index ).Type<T>() );
            }
            if (response == null || (response.Result != Result.Created && response.Result != Result.Updated && response.Result != Result.Noop))
            {
                if ( response != null )
                {
                    throw new Exception( $"Failed index {typeof( T )}(id = {id}) in index {index}, debug info: {response.DebugInformation}, server error: {response.ServerError}" );
                }
                throw new Exception( $"Failed index {typeof( T )}(id = {id}) in index {index}" );
            }
            _log.Debug( $@"ES AddToIndexAsync<{typeof( T )},{typeof( TKey )}> (id = {new Id( entity ).GetString( _connectionSettings )}) from index {index}, debug info: {response.DebugInformation} server error: {response.ServerError}" );
            
        }

        /// <inheritdoc />
        [Obsolete( "Strongly recommended to use Async version for best performance" )]
        public void UpdateInIndex<T, TKey>(T entity, string indexKey, TKey id, long? version = null) where T : class
        {
            string index = IndexName( indexKey );
            IUpdateResponse<T> response = null;
            if (version != null && version > -1)
            {
                response = _elastic.Update<T>( entity, ur => ur.Index( index ).Type<T>().DetectNoop().Doc( entity ).Version(version.Value) );
            }
            else
            {
                response = _elastic.Update<T>( entity, ur => ur.Index( index ).Type<T>().DetectNoop().Doc( entity ) );
            }
            if ( response == null || ( response.Result != Result.Noop && response.Result != Result.Updated ) )
            {
                if (response != null)
                {
                    throw new Exception( $"Failed update {typeof( T )}(id = {id}) in index {index}, debug info: {response.DebugInformation}, server error: {response.ServerError}" );
                }
                throw new Exception( $"Failed update {typeof( T )}(id = {id}) in index {index}" );
            }
        }
        /// <inheritdoc />
        public async Task UpdateInIndexAsync<T, TKey>(T entity, string indexKey, TKey id, long? version = null) where T : class
        {
            string index = IndexName( indexKey );
            IUpdateResponse<T> response = null;
            if (version != null && version > -1)
            {
                response = await _elastic.UpdateAsync<T>( entity, ur => ur.Index( index ).Type<T>().DetectNoop().Doc( entity ).Version(version.Value) );
            }
            else
            {
                response = await _elastic.UpdateAsync<T>( entity, ur => ur.Index( index ).Type<T>().DetectNoop().Doc( entity ) );
            }
            if ( response == null || ( response.Result != Result.Noop && response.Result != Result.Updated ) )
            {
                if ( response != null )
                {
                    throw new Exception( $"Failed update {typeof( T )}(id = {id}) in index {index}, debug info: {response.DebugInformation}, server error: {response.ServerError}" );
                }
                throw new Exception( $"Failed update {typeof( T )}(id = {id}) in index {index}" );
            }
            _log.Debug( $@"ES UpdateInIndexAsync<{typeof( T )},{typeof( TKey )}> (id = {new Id( entity ).GetString( _connectionSettings )}) from index {index}, debug info: {response.DebugInformation} server error: {response.ServerError}" );
        }
        /// <inheritdoc />
        [Obsolete( "Strongly recommended to use Async version for best performance" )]
        public void DeleteFromIndex<T, TKey>(T entity, string indexKey, TKey id, long? version = null) where T : class
        {
            string index = IndexName( indexKey );
            IDeleteResponse response = null;
            if (version != null && version > -1)
            {
                response = _elastic.Delete<T>( entity, d => d.Index( index ).Type<T>().Version(version.Value) );
            }
            else
            {
                response = _elastic.Delete<T>( entity, d => d.Index( index ).Type<T>() );
            }
            if ( response == null || response.Result != Result.Deleted )
            {
                if ( response != null )
                {
                    throw new Exception( $"Failed delete {typeof( T )}(id = {id}) in index {index}, debug info: {response.DebugInformation}, server error: {response.ServerError}" );
                }
                throw new Exception( $"Failed delete {typeof( T )}(id = {id}) in index {index}" );
            }
        }
        /// <inheritdoc />
        public async Task DeleteFromIndexAsync<T, TKey>(T entity, string indexKey, TKey id, long? version = null) where T : class
        {
            string index = IndexName( indexKey );
            IDeleteResponse response = null;
            if (version != null && version > -1)
            {
                response = await _elastic.DeleteAsync<T>( entity, d => d.Index( index ).Type<T>().Version(version.Value) );
            }
            else
            {
                response = await _elastic.DeleteAsync<T>( entity, d => d.Index( index ).Type<T>() );
            }
            if ( response == null || response.Result != Result.Deleted )
            {
                if ( response != null )
                {
                    throw new Exception( $"Failed delete {typeof( T )}(id = {id}) in index {index}, debug info: {response.DebugInformation}, server error: {response.ServerError}" );
                }
                throw new Exception( $"Failed delete {typeof( T )}(id = {id}) in index {index}" );
            }
            _log.Debug( $@"ES DeleteFromIndexAsync<{typeof( T )},{typeof( TKey )}> (id = {new Id( entity ).GetString( _connectionSettings )}) from index {index}, debug info: {response.DebugInformation} server error: {response.ServerError}" );
        }
        /// <inheritdoc />
        [Obsolete( "Strongly recommended to use Async version for best performance" )]
        public ISearchResponse<T> Search<T>( string indexKey, Func<string, SearchDescriptor<T>> queryBuilder ) where T : class
        {
            string index = IndexName(indexKey);
            SearchDescriptor<T> sd = queryBuilder( index );
            ISearchResponse<T> response = null;
            Stopwatch sw = new Stopwatch();
            try
            {
                sw.Start();
                response = _elastic.Search<T, T>( sd );
                sw.Stop();
            }
            catch ( Exception ex )
            {

                throw new Exception( $@"Search for {typeof(T)} failed", ex );
            }
            finally
            {
                string query = _elastic.Serializer.SerializeToString( sd );
                _log.Info( $@"Search for {typeof(T)} done in {sw.ElapsedMilliseconds} ms in index {index} : {query}" );
            }
            
            return response;
        }
        
        /// <inheritdoc />
        public async Task<ISearchResponse<T>> SearchAsync<T>( string indexKey, Func<string, SearchDescriptor<T>> queryBuilder ) where T : class
        {
            string index = IndexName( indexKey );
            SearchDescriptor<T> sd = queryBuilder( index );
            ISearchResponse<T> response = null;
            Stopwatch sw = new Stopwatch();
            try
            {
                sw.Start();
                response = await _elastic.SearchAsync<T, T>( sd );
                sw.Stop();
            }
            catch ( Exception ex )
            {

                throw new Exception( $@"Search for {typeof( T )} failed", ex );
            }
            finally
            {
                string query = _elastic.Serializer.SerializeToString( sd );
                _log.Info( $@"Search for {typeof( T )} done in {sw.ElapsedMilliseconds} ms in index {index} : {query}" );
                if (response != null)
                {
                    _log.Debug( $@"ES SearchAsync<{typeof( T )}> from index {index}, debug info: {response.DebugInformation} server error: {response.ServerError}" );
                }
                else
                {
                    _log.Debug( $@"ES SearchAsync<{typeof( T )}> from index {index}, debug info: response is NULL" );
                }
            }
            return response;
        }

        #endregion
        

    }
}

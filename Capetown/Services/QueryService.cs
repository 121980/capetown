using System;
using Capetown.Models;
using Capetown.Models.Filters;
using Capetown.Models.Queries;
using Nest;

namespace Capetown.Services
{
    /// <summary>
    /// Реализация сервиса интерпритатора запросов к ES, см. доки к NEST
    /// </summary>
    public class QueryService : IQueryService
    {

        #region GENERIC


        protected SearchDescriptor<T> SearchQuery<T, TQuery>( TQuery query,
            string index,
            Func<SearchDescriptor<T>, SearchDescriptor<T>> queryContext,
            Func<SearchDescriptor<T>, SearchDescriptor<T>> sortContext,
            Func<SearchDescriptor<T>, SearchDescriptor<T>> filterContext ) where T : class
            where TQuery : IApiQuery, new()
        {
            return SearchQuery( query, index, queryContext, sortContext, filterContext, sd => sd );
        }


        /// <summary>
        /// Стандартный каркас построение запроса DSL к ElasticSearch
        /// </summary>
        /// <typeparam name="T">Тип объекта, который будем искать</typeparam>
        /// <typeparam name="TQuery">Тип api поискового запроса</typeparam>
        /// <param name="query">api запрос</param>
        /// <param name="index">Индекс</param>
        /// <param name="queryContext">Контекст запроса</param>
        /// <param name="sortContext">Контекст сортировки</param>
        /// <param name="filterContext">Контекст фильтра</param>
        /// <param name="aggregationContext">Контекст аггрегаций</param>
        /// <returns>DSL дискриптор поискового запроса</returns>
        protected SearchDescriptor<T> SearchQuery<T, TQuery>( TQuery query,
            string index,
            Func<SearchDescriptor<T>, SearchDescriptor<T>> queryContext,
            Func<SearchDescriptor<T>, SearchDescriptor<T>> sortContext,
            Func<SearchDescriptor<T>, SearchDescriptor<T>> filterContext,
            Func<SearchDescriptor<T>, SearchDescriptor<T>> aggregationContext ) where T : class where TQuery : IApiQuery, new()
        {
            SearchDescriptor<T> sd = new SearchDescriptor<T>();

            #region Индекс
            // индекс
            sd = sd.Index( index );
            #endregion

            #region Постранично
            // постранично
            sd = sd.Skip( query.Skip ).Take( query.Limit );
            #endregion

            #region Контекст запроса
            // контекст запроса
            sd = queryContext( sd );
            #endregion

            #region Сортировка
            // сортировка
            sd = sortContext( sd );
            #endregion

            #region Контекст фильтра
            // контекст фильтра
            sd = filterContext( sd );
            #endregion

            #region Контекст аггрегаций

            sd = aggregationContext( sd );

            #endregion

            return sd;
        }


        #endregion
        
        #region Магазины

        public SearchDescriptor<Store> SimpleStoreQuery( StoreQuery query, string index )
        {
            return SearchQuery<Store, StoreQuery>( query,
                index,
                // контекст запроса
                ( sd ) =>
                {
                    return sd.Query( qc =>
                    {
                        if ( !string.IsNullOrEmpty( query.Query ) )
                        {
                            return qc.Match( mm => mm.Field( p => p.Description ).Fuzziness( Fuzziness.Auto ).Query( query.Query ) );
                        }
                        return qc.MatchNone();
                    } );
                },
                // контекст сортировки
                ( sd ) =>
                {
                    if ( query.Sort != null )
                    {
                        return sd.Sort( s => StoreSort( query, s ) );
                    }
                    return sd;
                },
                // контекст фильтра
                ( sd ) =>
                {
                    return sd.PostFilter( pf => StoreFilter( query, pf ) );
                }
            );
        }

        public SearchDescriptor<Store> SearchStoreByOwnerQuery( StoreQuery query, string index )
        {
            return SearchQuery<Store, StoreQuery>( query,
                index,
                // контекст запроса
                ( sd ) =>
                {
                    return sd.Query( qc =>
                    {
                        if ( query.OwnerId != null )
                        {
                            return qc.Bool( b => b
                                .Must( m => m
                                    .Term( t => t
                                         .Field( s => s.OwnerId )
                                         .Value( query.OwnerId )
                                         .Boost( 1.1 )
                                         .Strict()
                                    )
                                )
                            );
                        }
                        return qc.MatchNone();
                    } );
                },
                // контекст сортировки
                ( sd ) =>
                {
                    if ( query.Sort != null )
                    {
                        return sd.Sort( s => StoreSort( query, s ) );
                    }
                    return sd;
                },
                // контекст фильтра
                ( sd ) =>
                {
                    return sd.PostFilter( pf => StoreFilter( query, pf ) );
                }
            );
        }

        public SearchDescriptor<Store> GeoStoreQuery( StoreQuery query, string index )
        {
            return SearchQuery<Store, StoreQuery>( query,
                index,
                // контекст запроса
                ( sd ) =>
                {
                    return sd.Query( qgc =>
                    {
                        // если задан прямоугольник
                        if ( query.Bound != null )
                        {
                            return qgc.GeoBoundingBox( b => b
                                 .Field( s => s.Location )
                                 .BoundingBox( bbd => bbd
                                      .BottomRight( query.Bound.BottomRight.ToGeoLocation() )
                                      .TopLeft( query.Bound.TopLeft.ToGeoLocation() ) )
                                 .ValidationMethod( GeoValidationMethod.Strict )
                                 .Type( GeoExecution.Indexed ) );
                        }
                        // если задана дистанция от центра
                        if ( query.Circle != null )
                        {
                            return qgc.GeoDistance( b => b
                                 .Field( s => s.Location )
                                 .DistanceType( GeoDistanceType.Arc )
                                 .Location( query.Circle.Center.ToGeoLocation() )
                                 .Distance( query.Circle.Radius, DistanceUnit.Kilometers )
                                 .Optimize( GeoOptimizeBBox.Memory )
                                 .ValidationMethod( GeoValidationMethod.IgnoreMalformed ) );
                        }
                        return qgc.MatchNone();
                    } );
                },
                // контекст сортировки
                ( sd ) =>
                {
                    if ( query.Sort != null )
                    {
                        return sd.Sort( s => StoreSort( query, s ) );
                    }
                    return sd;
                },
                // контекст фильтра
                ( sd ) =>
                {
                    return sd.PostFilter( pf => StoreFilter( query, pf ) );
                }
            );
        }

        #region общие методы Stores

        /// <summary>
        /// Стандартная сортировка
        /// </summary>
        /// <param name="query">Запрос из API</param>
        /// <param name="s">Дискриптор сортировки</param>
        /// <returns></returns>
        public SortDescriptor<Store> StoreSort( StoreQuery query, SortDescriptor<Store> s )
        {
            if ( query.SortType == OrderType.Descending )
            {
                #region по убыванию
                switch ( query.Sort )
                {
                    case StoreOrderBy.Name:
                        s.Descending( p => p.Name );
                        break;
                    case StoreOrderBy.Updated:
                        s.Descending( p => p.Updated );
                        break;
                    case StoreOrderBy.Score:
                        s.Descending( SortSpecialField.Score );
                        break;
                    case StoreOrderBy.Index:
                        s.Descending( SortSpecialField.DocumentIndexOrder );
                        break;
                    case StoreOrderBy.Created:
                    default:
                        s.Descending( p => p.Created );
                        break;
                }
                return s;
                #endregion
            }
            #region по возрастанию
            switch ( query.Sort )
            {
                case StoreOrderBy.Name:
                    s.Ascending( p => p.Name );
                    break;
                case StoreOrderBy.Updated:
                    s.Ascending( p => p.Updated );
                    break;
                case StoreOrderBy.Score:
                    s.Ascending( SortSpecialField.Score );
                    break;
                case StoreOrderBy.Index:
                    s.Ascending( SortSpecialField.DocumentIndexOrder );
                    break;
                case StoreOrderBy.Created:
                default:
                    s.Ascending( p => p.Created );
                    break;
            }
            return s;
            #endregion
        }

        protected QueryContainer StoreFilter( StoreQuery query, QueryContainerDescriptor<Store> pf )
        {
            QueryContainer result = null;
            // фильтр по датам
            if ( query.From != null || query.To != null )
            {
                result = result & StoreFilterByDate( query, pf );
            }
            // фильтр по владельцу товаров
            if ( query.OwnerId != null )
            {
                result = result & StoreFilterByOwner( query, pf );
            }

            return result;
        }

        /// <summary>
        /// Стандартный фильтр по id владельца
        /// </summary>
        /// <param name="query">Запрос из API</param>
        /// <param name="pf">Дискриптор запроса</param>
        /// <returns></returns>
        protected QueryContainer StoreFilterByOwner( StoreQuery query, QueryContainerDescriptor<Store> pf )
        {
            return pf.Term( t => t.Field( f => f.OwnerId ).Value( query.OwnerId ) );
        }

        /// <summary>
        /// Фильтр по дате
        /// </summary>
        /// <param name="query"></param>
        /// <param name="pf"></param>
        /// <returns></returns>
        protected QueryContainer StoreFilterByDate( StoreQuery query, QueryContainerDescriptor<Store> pf )
        {
            return pf.DateRange( r =>
            {
                r.Field( fp => fp.Created );
                if ( query.From != null )
                {
                    r = r.GreaterThanOrEquals( query.From );
                }
                if ( query.To != null )
                {
                    r = r.LessThanOrEquals( query.To );
                }
                return r;
            } );
        }
        
        #endregion

        #endregion
        
    }
}

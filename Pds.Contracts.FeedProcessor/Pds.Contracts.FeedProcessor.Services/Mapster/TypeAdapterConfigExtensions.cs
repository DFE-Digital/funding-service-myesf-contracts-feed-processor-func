using Mapster;
using Pds.Contracts.FeedProcessor.Services.Models;
using System;
using System.ServiceModel.Syndication;

namespace Pds.Contracts.FeedProcessor.Services.Mapster
{
    /// <summary>
    /// Class to extend Mapster TypeAdapterConfig to add mappings.
    /// </summary>
    public static class TypeAdapterConfigExtensions
    {
        /// <summary>
        /// Adds mappings to the TypeAdapterConfig.
        /// </summary>
        /// <param name="config">The TypeAdapter config.</param>
        /// <returns>TypeAdapterConfig.</returns>
        public static TypeAdapterConfig Configure(this TypeAdapterConfig config)
        {
            TypeAdapterConfig.GlobalSettings.AllowImplicitSourceInheritance = true;
            config.Default.PreserveReference(true);
            config.Default.AddDestinationTransform(DestinationTransform.EmptyCollectionIfNull);

            config.NewConfig<SyndicationItem, FeedEntry>()
                .Map(dest => dest.Id, src => Guid.Parse(src.Id.Replace("uuid:", string.Empty)))
                .Map(dest => dest.Updated, src => src.LastUpdatedTime.UtcDateTime)
                .Map(dest => dest.Content, src => new SyndicationContentToXmlStringConverter().Convert(src.Content));

            return config;
        }
    }
}

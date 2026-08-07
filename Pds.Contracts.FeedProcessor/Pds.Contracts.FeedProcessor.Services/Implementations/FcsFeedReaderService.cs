using MapsterMapper;
using Microsoft.Extensions.Options;
using Pds.Contracts.FeedProcessor.Services.Configuration;
using Pds.Contracts.FeedProcessor.Services.Interfaces;
using Pds.Contracts.FeedProcessor.Services.Models;
using Pds.Core.ApiClient;
using Pds.Core.ApiClient.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceModel.Syndication;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Pds.Contracts.FeedProcessor.Services.Implementations
{
    /// <summary>
    /// FCS contract events ATOM feed reader.
    /// </summary>
    public class FcsFeedReaderService : BaseApiClient<FeedReaderOptions>, IFcsFeedReaderService
    {
        private readonly FeedReaderOptions _feedReaderOptions;
        private readonly IMapper _mapper;

        /// <summary>
        /// Initializes a new instance of the <see cref="FcsFeedReaderService" /> class.
        /// </summary>
        /// <param name="authenticationService">The authentication service.</param>
        /// <param name="httpClient">The HTTP client.</param>
        /// <param name="mapper">The mapper.</param>
        /// <param name="configurationOptions">The configuration options.</param>
        public FcsFeedReaderService(
            IAuthenticationService<FeedReaderOptions> authenticationService,
            HttpClient httpClient,
            IMapper mapper,
            IOptions<FeedReaderOptions> configurationOptions) : base(authenticationService, httpClient, configurationOptions)
        {
            httpClient.DefaultRequestHeaders.Accept?.Clear();
            httpClient.DefaultRequestHeaders.Accept?.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));

            _feedReaderOptions = configurationOptions.Value;
            _mapper = mapper;
        }

        /// <inheritdoc/>
        public FeedPage ExtractContractEventsFromFeedPageAsync(string payload)
        {
            var formatter = new Atom10FeedFormatter();
            var doc = XDocument.Parse(payload);
            using (var reader = doc.CreateReader())
            {
                formatter.ReadFrom(reader);
            }

            var previousPageLink = formatter.Feed.Links.SingleOrDefault(l => l.RelationshipType.Equals("prev-archive", StringComparison.OrdinalIgnoreCase));
            var nextPageLink = formatter.Feed.Links.SingleOrDefault(l => l.RelationshipType.Equals("next-archive", StringComparison.OrdinalIgnoreCase));
            var currentPageLink = formatter.Feed.Links.SingleOrDefault(l => l.RelationshipType.Equals("current", StringComparison.OrdinalIgnoreCase));

            int.TryParse(previousPageLink?.Uri.Segments.Last(), out var prevPage);
            int.TryParse(nextPageLink?.Uri.Segments.Last(), out var nextPage);
            int.TryParse(currentPageLink?.Uri.Segments.Last(), out var currentPage);

            return new FeedPage
            {
                Entries = _mapper.Map<IList<FeedEntry>>(formatter.Feed.Items),
                CurrentPageNumber = currentPage,
                IsSelfPage = nextPageLink is null && currentPageLink is null,
                NextPageNumber = nextPage,
                PreviousPageNumber = prevPage
            };
        }

        /// <inheritdoc/>
        public async Task<FeedPage> ReadPageAsync(int pageNumber)
        {
            if (pageNumber == -1)
            {
                return await ReadSelfPageAsync();
            }
            else
            {
                var result = await Get<string>($"{_feedReaderOptions.FcsAtomFeedSelfPageEndpoint}/{pageNumber}");
                var feedPage = ExtractContractEventsFromFeedPageAsync(result);
                feedPage.IsSelfPage = false;
                feedPage.CurrentPageNumber = feedPage.CurrentPageNumber == 0 ? pageNumber : feedPage.CurrentPageNumber;

                return feedPage;
            }
        }

        /// <inheritdoc/>
        public async Task<FeedPage> ReadSelfPageAsync()
        {
            var result = await Get<string>(_feedReaderOptions.FcsAtomFeedSelfPageEndpoint);
            var selfPage = ExtractContractEventsFromFeedPageAsync(result);
            selfPage.IsSelfPage = true;
            selfPage.CurrentPageNumber = selfPage.PreviousPageNumber + 1;

            return selfPage;
        }
    }
}
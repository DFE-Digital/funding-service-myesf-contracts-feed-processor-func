using FluentAssertions;
using MapsterMapper;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Pds.Contracts.FeedProcessor.Services.Configuration;
using Pds.Contracts.FeedProcessor.Services.Models;
using Pds.Core.ApiClient.Interfaces;
using RichardSzalay.MockHttp;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Threading.Tasks;

namespace Pds.Contracts.FeedProcessor.Services.Implementations.Tests
{
    [TestClass, TestCategory("Unit")]
    public class FcsFeedReaderServiceTests
    {
        private readonly IAuthenticationService<FeedReaderOptions> _mockAuthService;
        private readonly HttpClient _mockHttpClient;
        private readonly IMapper _mockMapper;
        private readonly IOptions<FeedReaderOptions> _mockOptions;
        private readonly MockHttpMessageHandler _mockHttpMessageHandler;

        public FcsFeedReaderServiceTests()
        {
            _mockHttpMessageHandler = new MockHttpMessageHandler();
            _mockHttpClient = _mockHttpMessageHandler.ToHttpClient();
            _mockAuthService = Mock.Of<IAuthenticationService<FeedReaderOptions>>(MockBehavior.Strict);
            _mockMapper = Mock.Of<IMapper>(MockBehavior.Strict);
            _mockOptions = Options.Create(new FeedReaderOptions
            {
                ApiBaseAddress = "http://localhost",
                ShouldSkipAuthentication = true,
                FcsAtomFeedSelfPageEndpoint = "/notifications"
            });
        }

        [TestMethod, TestCategory("Unit")]
        public void ExtractContractEventsFromFeedPageAsyncTest()
        {
            //Arrange
            var dummyPayload =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
                <feed xmlns=""http://www.w3.org/2005/Atom"">
                    <title type=""text"">Test</title>
                    <id>uuid:d2619398-19dc-44e8-b4a9-917796baf6c2</id>
                    <updated>2021-01-01T01:01:01Z</updated>
                    <author>
                        <name>Contract Management Service</name>
                    </author>
                    <link rel=""next-archive"" type=""application/vnd.test.v1+atom+xml"" href=""https://localhost/notifications/3"" />
                    <link rel=""prev-archive"" type=""application/vnd.test.v1+atom+xml"" href=""https://localhost/notifications/1"" />
                    <link rel=""self"" type=""application/vnd.test.v1+atom+xml"" href=""https://localhost/notifications"" />
                    <link rel=""current"" type=""application/vnd.test.v1+atom+xml"" href=""https://localhost/notifications/2"" />
                    <archive xmlns=""http://purl.org/syndication/history/1.0""></archive>
                    <entry>
                        <id>uuid:d2619398-19dc-44e8-b4a9-917796baf6c2</id>
                        <title type=""text"">Test title</title>
                        <summary type=""text"">Test summary</summary>
                        <published>2021-01-01T01:01:01Z</published>
                        <updated>2021-01-01T01:01:01Z</updated>
                        <category term=""Test category"" label=""A test category"" />
                        <content type=""application/vnd.test.v1+atom+xml"">Test content string</content>
                   </entry>
                </feed>";

            var expectedFeedPage = new FeedPage
            {
                CurrentPageNumber = 2,
                PreviousPageNumber = 1,
                NextPageNumber = 3,
                IsSelfPage = false,
                Entries = new[]
                {
                    new FeedEntry
                    {
                        Id = new Guid("d2619398-19dc-44e8-b4a9-917796baf6c2"),
                        Updated = DateTime.Parse("2021-01-01T01:01:01Z"),
                        Content = @"<Content type=""application/vnd.test.v1+atom+xml"">Test content string</Content>"
                    }
                }
            };

            Mock.Get(_mockMapper)
                .Setup(m => m.Map<IList<FeedEntry>>(It.IsAny<IEnumerable<SyndicationItem>>()))
                .Returns(expectedFeedPage.Entries);

            //Act
            var reader = new FcsFeedReaderService(_mockAuthService, _mockHttpClient, _mockMapper, _mockOptions);
            var results = reader.ExtractContractEventsFromFeedPageAsync(dummyPayload);

            //Assert
            results.Should().BeEquivalentTo(expectedFeedPage);
        }

        [TestMethod, TestCategory("Unit")]
        public async Task ReadPageAsyncTestAsync()
        {
            //Arrange
            var dummyPayload =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
                <feed xmlns=""http://www.w3.org/2005/Atom"">
                    <title type=""text"">Test</title>
                    <id>uuid:d2619398-19dc-44e8-b4a9-917796baf6c2</id>
                    <updated>2021-01-01T01:01:01Z</updated>
                    <author>
                        <name>Contract Management Service</name>
                    </author>
                    <link rel=""next-archive"" type=""application/vnd.test.v1+atom+xml"" href=""https://localhost/notifications/11"" />
                    <link rel=""prev-archive"" type=""application/vnd.test.v1+atom+xml"" href=""https://localhost/notifications/9"" />
                    <link rel=""self"" type=""application/vnd.test.v1+atom+xml"" href=""https://localhost/notifications"" />
                    <link rel=""current"" type=""application/vnd.test.v1+atom+xml"" href=""https://localhost/notifications/10"" />
                    <archive xmlns=""http://purl.org/syndication/history/1.0""></archive>
                    <entry>
                        <id>uuid:d2619398-19dc-44e8-b4a9-917796baf6c2</id>
                        <title type=""text"">Test title</title>
                        <summary type=""text"">Test summary</summary>
                        <published>2021-01-01T01:01:01Z</published>
                        <updated>2021-01-01T01:01:01Z</updated>
                        <category term=""Test category"" label=""A test category"" />
                        <content type=""application/vnd.test.v1+atom+xml"">Test content string</content>
                   </entry>
                    <entry>
                        <id>uuid:b1ca5999-f34f-405c-84fd-a6e7d94bd1ac</id>
                        <title type=""text"">Test title 2</title>
                        <summary type=""text"">Test summary 2</summary>
                        <published>2021-02-02T02:02:02Z</published>
                        <updated>2021-02-02T02:02:02Z</updated>
                        <category term=""Test category 2"" label=""A test category 2"" />
                        <content type=""application/vnd.test.v1+atom+xml"">Test content string 2</content>
                   </entry>
                </feed>";

            var currentPageNumber = 10;
            var expectedFeedPage = new FeedPage
            {
                CurrentPageNumber = currentPageNumber,
                PreviousPageNumber = currentPageNumber - 1,
                NextPageNumber = currentPageNumber + 1,
                IsSelfPage = false,
                Entries = new[]
                {
                    new FeedEntry
                    {
                        Id = new Guid("d2619398-19dc-44e8-b4a9-917796baf6c2"),
                        Updated = DateTime.Parse("2021-01-01T01:01:01Z"),
                        Content = @"<Content type=""application/vnd.test.v1+atom+xml"">Test content string</Content>"
                    },
                    new FeedEntry
                    {
                        Id = new Guid("b1ca5999-f34f-405c-84fd-a6e7d94bd1ac"),
                        Updated = DateTime.Parse("2021-02-02T02:02:02Z"),
                        Content = @"<Content type=""application/vnd.test.v1+atom+xml"">Test content string 2</Content>"
                    }
                }
            };

            SetupMocks(dummyPayload, expectedFeedPage, currentPageNumber);

            //Act
            var reader = new FcsFeedReaderService(_mockAuthService, _mockHttpClient, _mockMapper, _mockOptions);
            var results = await reader.ReadPageAsync(currentPageNumber);

            //Assert
            results.Should().BeEquivalentTo(expectedFeedPage);
            VerifyMocks();
        }

        [TestMethod, TestCategory("Unit")]
        public async Task ReadPageAsync_ReturnsSelfPage_TestAsync()
        {
            //Arrange
            var dummyPayload =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
                <feed xmlns=""http://www.w3.org/2005/Atom"">
                    <title type=""text"">Test</title>
                    <id>uuid:d2619398-19dc-44e8-b4a9-917796baf6c2</id>
                    <updated>2021-01-01T01:01:01Z</updated>
                    <author>
                        <name>Contract Management Service</name>
                    </author>
                    <link rel=""prev-archive"" type=""application/vnd.test.v1+atom+xml"" href=""https://localhost/notifications/9"" />
                    <link rel=""self"" type=""application/vnd.test.v1+atom+xml"" href=""https://localhost/notifications"" />
                    <archive xmlns=""http://purl.org/syndication/history/1.0""></archive>
                    <entry>
                        <id>uuid:d2619398-19dc-44e8-b4a9-917796baf6c2</id>
                        <title type=""text"">Test title</title>
                        <summary type=""text"">Test summary</summary>
                        <published>2021-01-01T01:01:01Z</published>
                        <updated>2021-01-01T01:01:01Z</updated>
                        <category term=""Test category"" label=""A test category"" />
                        <content type=""application/vnd.test.v1+atom+xml"">Test content string</content>
                   </entry>
                    <entry>
                        <id>uuid:b1ca5999-f34f-405c-84fd-a6e7d94bd1ac</id>
                        <title type=""text"">Test title 2</title>
                        <summary type=""text"">Test summary 2</summary>
                        <published>2021-02-02T02:02:02Z</published>
                        <updated>2021-02-02T02:02:02Z</updated>
                        <category term=""Test category 2"" label=""A test category 2"" />
                        <content type=""application/vnd.test.v1+atom+xml"">Test content string 2</content>
                   </entry>
                </feed>";

            var currentPageNumber = 10;
            var expectedFeedPage = new FeedPage
            {
                CurrentPageNumber = currentPageNumber,
                PreviousPageNumber = currentPageNumber - 1,
                NextPageNumber = 0,
                IsSelfPage = true,
                Entries = new[]
                {
                    new FeedEntry
                    {
                        Id = new Guid("d2619398-19dc-44e8-b4a9-917796baf6c2"),
                        Updated = DateTime.Parse("2021-01-01T01:01:01Z"),
                        Content = @"<Content type=""application/vnd.test.v1+atom+xml"">Test content string</Content>"
                    },
                    new FeedEntry
                    {
                        Id = new Guid("b1ca5999-f34f-405c-84fd-a6e7d94bd1ac"),
                        Updated = DateTime.Parse("2021-02-02T02:02:02Z"),
                        Content = @"<Content type=""application/vnd.test.v1+atom+xml"">Test content string 2</Content>"
                    }
                }
            };

            SetupMocks(dummyPayload, expectedFeedPage);

            //Act
            var reader = new FcsFeedReaderService(_mockAuthService, _mockHttpClient, _mockMapper, _mockOptions);
            var results = await reader.ReadPageAsync(-1);

            //Assert
            results.Should().BeEquivalentTo(expectedFeedPage);
            VerifyMocks();
        }

        [TestMethod, TestCategory("Unit")]
        public async Task ReadSelfPageAsyncTestAsync()
        {
            //Arrange
            var dummyPayload =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
                <feed xmlns=""http://www.w3.org/2005/Atom"">
                    <title type=""text"">Test</title>
                    <id>uuid:d2619398-19dc-44e8-b4a9-917796baf6c2</id>
                    <updated>2021-01-01T01:01:01Z</updated>
                    <author>
                        <name>Contract Management Service</name>
                    </author>
                    <link rel=""prev-archive"" type=""application/vnd.test.v1+atom+xml"" href=""https://localhost/notifications/1"" />
                    <link rel=""self"" type=""application/vnd.test.v1+atom+xml"" href=""https://localhost/notifications"" />
                    <archive xmlns=""http://purl.org/syndication/history/1.0""></archive>
                    <entry>
                        <id>uuid:d2619398-19dc-44e8-b4a9-917796baf6c2</id>
                        <title type=""text"">Test title</title>
                        <summary type=""text"">Test summary</summary>
                        <published>2021-01-01T01:01:01Z</published>
                        <updated>2021-01-01T01:01:01Z</updated>
                        <category term=""Test category"" label=""A test category"" />
                        <content type=""application/vnd.test.v1+atom+xml"">Test content string</content>
                   </entry>
                </feed>";

            var expectedFeedPage = new FeedPage
            {
                CurrentPageNumber = 2,
                PreviousPageNumber = 1,
                IsSelfPage = true,
                Entries = new[]
                {
                    new FeedEntry
                    {
                        Id = new Guid("d2619398-19dc-44e8-b4a9-917796baf6c2"),
                        Updated = DateTime.Parse("2021-01-01T01:01:01Z"),
                        Content = @"<Content type=""application/vnd.test.v1+atom+xml"">Test content string</Content>"
                    }
                }
            };

            SetupMocks(dummyPayload, expectedFeedPage);

            //Act
            var reader = new FcsFeedReaderService(_mockAuthService, _mockHttpClient, _mockMapper, _mockOptions);
            var results = await reader.ReadSelfPageAsync();

            //Assert
            results.Should().BeEquivalentTo(expectedFeedPage);
            VerifyMocks();
        }

        /// <summary>
        /// Setups the mocks.
        /// </summary>
        /// <param name="xmlContent">Content of the XML.</param>
        /// <param name="expectedFeedPage">The expected feed page.</param>
        /// <param name="page">The page.</param>
        private void SetupMocks(string xmlContent, FeedPage expectedFeedPage, int page = -1)
        {
            Mock.Get(_mockMapper)
                .Setup(m => m.Map<IList<FeedEntry>>(It.IsAny<IEnumerable<SyndicationItem>>()))
                .Returns(expectedFeedPage.Entries);

            _mockHttpMessageHandler
                .Expect($"{_mockOptions.Value.ApiBaseAddress}{_mockOptions.Value.FcsAtomFeedSelfPageEndpoint}{(page >= 0 ? "/" + page.ToString() : string.Empty)}")
                .Respond("application/atom+xml", xmlContent);
        }

        private void VerifyMocks()
        {
            Mock.Get(_mockMapper).VerifyAll();
            _mockHttpMessageHandler.VerifyNoOutstandingExpectation();
            _mockHttpMessageHandler.VerifyNoOutstandingRequest();
        }
    }
}
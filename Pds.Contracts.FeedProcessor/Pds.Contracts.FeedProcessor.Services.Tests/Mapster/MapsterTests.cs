using Mapster;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Pds.Contracts.FeedProcessor.Services.Mapster.Tests
{
    [TestClass, TestCategory("Unit")]
    public class MapsterTests
    {
        [TestMethod, TestCategory("Unit")]
        public void MapsterTypeAdapterConfigExtensionsMeetsExpectation()
        {
            // arrange
            TypeAdapterConfig config = new TypeAdapterConfig();
            config.Configure();

            // act / assert
            TypeAdapterConfig.GlobalSettings.Compile();
        }
    }
}
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;

namespace Pds.Contracts.FeedProcessor.Services.Mapster
{
    /// <summary>
    /// A value converter to conver syndication content to xml string.
    /// </summary>
    public class SyndicationContentToXmlStringConverter
    {
        /// <summary>
        /// Perform conversion from source member value to destination member value.
        /// </summary>
        /// <param name="sourceMember">Source member object.</param>
        /// <returns>
        /// Destination member value.
        /// </returns>
        public string Convert(SyndicationContent sourceMember)
        {
            var contentBuilder = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings { Indent = true };
            using var writer = XmlWriter.Create(contentBuilder, settings);
            sourceMember.WriteTo(writer, "Content", string.Empty);
            writer.Flush();
            return contentBuilder.ToString();
        }
    }
}

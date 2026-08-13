using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Pds.Contracts.FeedProcessor.Services.Models
{
    /// <summary>
    /// Represents an contract event extracted from the Atom feed.
    /// </summary>
    public class ContractEvent
    {
        /// <summary>
        /// Gets or sets the contract event bookmark id.
        /// </summary>
        public Guid BookmarkId { get; set; }

        /// <summary>
        /// Gets or sets the UKPRN.
        /// </summary>
        public int Ukprn { get; set; }

        /// <summary>
        /// Gets or sets the contract number.
        /// </summary>
        public string ContractNumber { get; set; }

        /// <summary>
        /// Gets or sets the parent contract number.
        /// </summary>
        public string ParentContractNumber { get; set; }

        /// <summary>
        /// Gets or sets the contract version.
        /// </summary>
        public int ContractVersion { get; set; }

        /// <summary>
        /// Gets or sets the contract status.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ContractStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the contract parent status.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ContractParentStatus ParentStatus { get; set; }

        /// <summary>
        /// Gets or sets the amendment type.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ContractAmendmentType AmendmentType { get; set; }

        /// <summary>
        /// Gets or sets the funding type.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ContractFundingType FundingType { get; set; }

        /// <summary>
        /// Gets or sets the start date.
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Gets or sets the end date.
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Gets or sets the signed on date.
        /// </summary>
        public DateTime? SignedOn { get; set; }

        /// <summary>
        /// Gets or sets the contract type.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the contract allocations collection.
        /// </summary>
        public IEnumerable<ContractAllocation> ContractAllocations { get; set; }

        /// <summary>
        /// Gets or sets the contract value.
        /// </summary>
        public decimal Value { get; set; }

        /// <summary>
        /// Gets or sets the contract period value.
        /// </summary>
        public string ContractPeriodValue { get; set; }

        /// <summary>
        /// Gets or sets the contract xml file name.
        /// </summary>
        public string ContractEventXml { get; set; }
    }
}

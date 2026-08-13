using Pds.Audit.Api.Client.Interfaces;
using Pds.Contracts.FeedProcessor.Services.Extensions;
using Pds.Contracts.FeedProcessor.Services.Interfaces;
using Pds.Contracts.FeedProcessor.Services.Models;
using Pds.Core.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace Pds.Contracts.FeedProcessor.Services.Implementations
{
    /// <summary>
    /// Provides functionality to deserialize a contract conforming to v11.09 schema.
    /// </summary>
    public class Deserializer_v1109 : IDeserilizationService<ContractProcessResult>
    {
        /// <summary>
        /// The xml namespace used by conrtacts.
        /// </summary>
        public const string _contractEvent_Namespace = "urn:sfa:schemas:contract";

        private const string _auditApiUser = "System - Contract.FeedProcessor.Func";

        private readonly IContractEventValidationService _validationService;
        private readonly IAuditService _auditService;
        private readonly ILoggerAdapter<Deserializer_v1109> _loggerAdapter;

        /// <summary>
        /// Initializes a new instance of the <see cref="Deserializer_v1109"/> class.
        /// </summary>
        /// <param name="validationService">Service for validation of contract event.</param>
        /// /// <param name="auditService">Service for creating audit entries.</param>
        /// <param name="loggerAdapter">Logger adapter for internal process logging.</param>
        public Deserializer_v1109(
            IContractEventValidationService validationService,
            IAuditService auditService,
            ILoggerAdapter<Deserializer_v1109> loggerAdapter)
        {
            _validationService = validationService;
            _auditService = auditService;
            _loggerAdapter = loggerAdapter;
        }

        /// <inheritdoc/>
        public async Task<IList<ContractProcessResult>> DeserializeAsync(string xml)
        {
            _loggerAdapter.LogInformation($"[{nameof(Deserializer_v1109)}.{nameof(DeserializeAsync)}] - Called to deserilise xml string.");

            var contractList = new List<ContractProcessResult>();

            // Ensure xml is valid.
            var document = _validationService.ValidateXmlWithSchema(xml);
            document = LoadXml(xml);

            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("c", _contractEvent_Namespace);

            // The content element may be in either case.
            var details = (document["content"] ?? document["Content"])["contract"];
            var feedContracts = details.SelectNodesIgnoreCase("c:contracts/c:contract", ns);
            foreach (XmlElement feedContract in feedContracts)
            {
                var evt = DeserializeContractEvent(feedContract, ns);

                contractList.Add(new ContractProcessResult
                {
                    Result = await GetProcessedResultType(feedContract, ns, evt),
                    ContractEvent = evt,
                    ContractXml = document
                });
            }

            _loggerAdapter.LogInformation($"[{nameof(Deserializer_v1109)}.{nameof(DeserializeAsync)}] - Deserialistion completed.");
            return contractList;
        }

        /// <summary>
        /// Load XML document with no validation.
        /// </summary>
        /// <param name="contents">Content to be loaded.</param>
        /// <returns>An XML document.</returns>
        private XmlDocument LoadXml(string contents)
        {
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(contents);
            return xmlDocument;
        }

        private async Task<ContractProcessResultType> GetProcessedResultType(XmlElement feedItem, XmlNamespaceManager ns, ContractEvent evt)
        {
            var contractStatus = feedItem.GetValue<string>("c:contractStatus/c:status", ns);
            var parentStatus = feedItem.GetValue<string>("c:contractStatus/c:parentStatus", ns);

            var amendmentType = feedItem.GetValue("c:amendmentType", ns, true, "None");
            var fundingType = feedItem.GetValue<string>("c:fundingType/c:fundingTypeCode", ns);

            var resultCode = !await _validationService.ValidateContractStatusAsync(parentStatus, contractStatus, amendmentType)
                                ? ContractProcessResultType.StatusValidationFailed
                                : !await _validationService.ValidateFundingTypeAsync(fundingType)
                                    ? ContractProcessResultType.FundingTypeValidationFailed
                                    : ContractProcessResultType.Successful;

            if (resultCode != ContractProcessResultType.Successful)
            {
                string msg = $"Contract event for Contract [{evt.ContractNumber}] Version [{evt.ContractVersion}]";
                msg += resultCode switch
                {
                    ContractProcessResultType.StatusValidationFailed => $" with parent status [{parentStatus}], status [{contractStatus}], and amendment type [{amendmentType}] has been ignored.",
                    ContractProcessResultType.FundingTypeValidationFailed => $" with funding type [{fundingType}] has been ignored.",
                    _ => throw new NotImplementedException(),
                };

                _loggerAdapter.LogWarning(msg);
                await _auditService.TrySendAuditAsync(new Audit.Api.Client.Models.Audit()
                {
                    Action = Audit.Api.Client.Enumerations.ActionType.ContractFeedEventFilteredOut,
                    Message = msg,
                    Severity = Audit.Api.Client.Enumerations.SeverityLevel.Information,
                    Ukprn = evt.Ukprn,
                    User = _auditApiUser
                });
            }

            return resultCode;
        }

        private ContractEvent DeserializeContractEvent(XmlElement contractElement, XmlNamespaceManager ns)
        {
            var evt = new ContractEvent();

            evt.Ukprn = contractElement.GetValue<int>("c:contractor/c:ukprn", ns);

            var contractNumber = contractElement.GetValue<string>("c:contractNumber", ns);
            evt.ContractNumber = contractNumber;

            evt.ContractVersion = contractElement.GetValue<int>("c:contractVersionNumber", ns);
            evt.ParentContractNumber = contractElement.GetValue<string>("c:parentContractNumber", ns);
            evt.Status = ParseContractStatus(contractElement.GetValue<string>("c:contractStatus/c:status", ns));
            evt.ParentStatus = Enum.Parse<ContractParentStatus>(contractElement.GetValue<string>("c:contractStatus/c:parentStatus", ns));
            evt.ContractPeriodValue = contractElement.GetValue<string>("c:period/c:period", ns);
            evt.Value = contractElement.GetValue<decimal>("c:contractValue", ns, true);
            evt.AmendmentType = Enum.Parse<ContractAmendmentType>(contractElement.GetValue<string>("c:amendmentType", ns, true, "None"));
            evt.Type = contractElement.GetValue<string>("c:contractType", ns, true);

            var fundingType = contractElement.GetValue<string>("c:fundingType/c:fundingTypeCode", ns);
            evt.FundingType = ParseContractFundingType(fundingType, contractNumber, evt.ContractPeriodValue);

            // Start date can be null
            evt.StartDate = contractElement.GetValue<DateTime?>("c:startDate", ns, true);

            // End date can be null
            evt.EndDate = contractElement.GetValue<DateTime?>("c:endDate", ns, true);

            evt.SignedOn = contractElement.GetValue<DateTime?>("c:ContractApprovalDate", ns, true);

            evt.ContractAllocations = ExtractAllocations(contractElement, ns);

            return evt;
        }

        private List<ContractAllocation> ExtractAllocations(XmlElement mainNode, XmlNamespaceManager ns)
        {
            var allocations = new List<ContractAllocation>();
            var allocationElements = mainNode.SelectNodesIgnoreCase("c:contractAllocations/c:contractAllocation", ns);

            foreach (XmlElement allocationElement in allocationElements)
            {
                var allocation = new ContractAllocation()
                {
                    ContractAllocationNumber = allocationElement.GetValue<string>($"c:contractAllocationNumber", ns, true),
                    FundingStreamPeriodCode = allocationElement.GetValue<string>($"c:fundingStreamPeriodCode", ns, true),
                    LEPArea = allocationElement.GetValue<string>($"c:ProcurementAttrs/c:LEPName", ns, true),
                    TenderSpecTitle = allocationElement.GetValue<string>($"c:ProcurementAttrs/c:TenderSpecTitle", ns, true)
                };

                allocations.Add(allocation);
            }

            return allocations;
        }

        private ContractStatus ParseContractStatus(string status)
        {
            return status.ToLower() switch
            {
                "draft" => ContractStatus.Draft,
                "approved" => ContractStatus.Approved,
                "unassigned" => ContractStatus.Unassigned,
                "in review" => ContractStatus.InReview,
                "awaiting internal approval" => ContractStatus.AwaitingInternalApproval,
                "published to provider" => ContractStatus.PublishedToProvider,
                "withdrawn by provider" => ContractStatus.WithdrawnByProvider,
                "withdrawn by agency" => ContractStatus.WithdrawnByAgency,
                "closed" => ContractStatus.Closed,
                "under termination" => ContractStatus.UnderTermination,
                "terminated" => ContractStatus.Terminated,
                "modified" => ContractStatus.Modified,

                _ => throw new InvalidOperationException($"Status [{status}] is not valid."),
            };
        }

        private ContractFundingType ParseContractFundingType(string fundingType, string contractNumber, string contractPeriodValue)
        {
            return string.IsNullOrEmpty(fundingType) ? ContractFundingType.Unknown : fundingType.ToLower() switch
            {
                "main" => ContractFundingType.Mainstream,
                "esf" => ContractFundingType.Esf,
                "24+loans" => ContractFundingType.TwentyFourPlusLoan,
                "age" => ContractFundingType.Age,
                "eop" => ContractFundingType.Eop,
                "eof" => ContractFundingType.Eof,
                "levy" => ContractFundingType.Levy,
                "ncs" => ContractFundingType.Ncs,
                "1619fund" => ContractFundingType.SixteenNineteenFunding,
                "aeb" => ContractFundingType.Aebp,
                "nla" => ContractFundingType.Nla,
                "loans" => ContractFundingType.AdvancedLearnerLoans,
                "edsk" => ContractFundingType.EducationAndSkillsFunding,
                "nlg" => ContractFundingType.NonLearningGrant,
                "16-18fu" => ContractFundingType.SixteenEighteenForensicUnit,
                "dada" => ContractFundingType.DanceAndDramaAwards,
                "ccf" => ContractFundingType.CollegeCollaborationFund,
                "feca" => ContractFundingType.FurtherEducationConditionAllocation,
                "19trn2020" => ContractFundingType.ProcuredNineteenToTwentyFourTraineeship,
                "aeb2021" => ContractFundingType.AdultEducationBudgetContractForService,
                "hte-pgf" => ContractFundingType.HigherTechnicalEducation,
                "sadf" => ContractFundingType.SkillsAcceleratorDevelopmentFund,
                "fe-pdgp" => ContractFundingType.FurtherEducationProfessionalDevelopmentGrant,
                "sdfii" => ContractFundingType.StrategicDevelopmentFund2,
                "sb" => ContractFundingType.SkillsBootcamps,
                "mult" => ContractFundingType.Multiply,
                "fe-aca" => ContractFundingType.AdditionalCapitalAllocations,
                "hte-sif" => ContractFundingType.HigherTechnicalEducationSkillsInjectionFund,
                "fe-rca" => ContractFundingType.FEReclassificationCapitalAllocation,
                "fe-ctf" => ContractFundingType.FECapitalTransformationFundAllocation,
                "aeb2023" => DetermineContractFundingType(contractNumber, contractPeriodValue),
                "sbd" => ContractFundingType.SkillsBootcampsDPS,
                "hte-sif2" => ContractFundingType.HigherTechnicalEducationSkillsInjectionFund2,
                "ttf" => ContractFundingType.TakingTeachingFurther,
                "ttfy2" => ContractFundingType.TakingTeachingFurtherYear2,
                "ctec" => ContractFundingType.ConstructionTechnicalExcellenceColleges,
                "csp-ipf" => ContractFundingType.ConstructionSkillsPackageIndustryPlacementsFund,
                _ => ContractFundingType.Unknown
            };
        }

        private ContractFundingType DetermineContractFundingType(string contractNumber, string contractPeriodValue)
        {
            if (!contractNumber.ToLower().StartsWith("asfp23"))
            {
                return ContractFundingType.AdultEducationBudgetProcured2023;
            }

            return int.Parse(contractPeriodValue) switch
            {
                2627 => ContractFundingType.DWPAdultSkillsFund,
                2526 => ContractFundingType.AdultSkillsFund,
                _ => ContractFundingType.AdultSkillsFundProcured2023
            };
        }
    }
}
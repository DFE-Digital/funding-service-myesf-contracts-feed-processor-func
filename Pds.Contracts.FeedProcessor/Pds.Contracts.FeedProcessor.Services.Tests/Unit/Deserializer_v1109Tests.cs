using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Pds.Audit.Api.Client.Interfaces;
using Pds.Contracts.FeedProcessor.Services.Implementations;
using Pds.Contracts.FeedProcessor.Services.Interfaces;
using Pds.Contracts.FeedProcessor.Services.Models;
using Pds.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;

namespace Pds.Contracts.FeedProcessor.Services.Tests.Unit
{
    [TestClass, TestCategory("Unit")]
    public class Deserializer_v1109Tests
    {
        private readonly string _baseDirectory = AppDomain.CurrentDomain.BaseDirectory + "/Documents/11_09";

        private readonly IContractEventValidationService _validationService
            = Mock.Of<IContractEventValidationService>(MockBehavior.Strict);

        private readonly ILoggerAdapter<Deserializer_v1109> _loggerAdapter
            = Mock.Of<ILoggerAdapter<Deserializer_v1109>>(MockBehavior.Strict);


        private readonly IAuditService _auditService
            = Mock.Of<IAuditService>(MockBehavior.Strict);

        #region Deserilize

        #region Base Result

        [TestMethod, TestCategory("Unit")]
        public async Task Deserialize_ReturnsExpected()
        {
            // Arrange
            string xml = LoadPartialXMLFile();
            var document = new XmlDocument();
            document.LoadXml(xml);

            ILoggerAdapter_SetupLogInformation();
            IContractValidationService_Setup_ValidateContractStatus();
            IContractValidationService_Setup_ValidateFundingType();
            IContractValidationService_Setup_ValidateXmlWithSchema(document);

            var expected = GeneratePocoForESIF9999(document);

            var sut = GetDeserializer();

            // Act
            var actual = await sut.DeserializeAsync(xml);

            // Assert
            actual.Should().BeEquivalentTo(expected, "Because the input XML is valid and all checks pass.");
            Verify_All();
        }
        #endregion


        #region Validation Errors

        [TestMethod, TestCategory("Unit")]
        public void Deserialize_SchemaValidationError_RaisesException()
        {
            // Arrange
            var xml = "<some>input</some>";

            ILoggerAdapter_SetupLogInformation();

            Mock.Get(_validationService)
                .Setup(p => p.ValidateXmlWithSchema(It.IsAny<string>()))
                .Throws<XmlSchemaValidationException>()
                .Verifiable();

            var sut = GetDeserializer();

            // Act
            Func<Task> act = async () => await sut.DeserializeAsync(xml);

            // Assert
            act.Should().ThrowAsync<XmlSchemaValidationException>("Because input xml is not compatible with schema.");
            Verify_All();
        }

        [TestMethod, TestCategory("Unit")]
        public async Task Deserialize_StatusValidationFailure_ReturnsErrorResult()
        {
            // Arrange
            string xml = LoadPartialXMLFile();
            var document = new XmlDocument();
            document.LoadXml(xml);

            ILoggerAdapter_SetupLogInformation();
            ILoggerAdapter_SetupLogWarning();
            IAuditService_Setup_TrySend();
            IContractValidationService_Setup_ValidateXmlWithSchema(document);

            Mock.Get(_validationService)
                .Setup(p => p.ValidateContractStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(false));

            var expected = GeneratePocoForESIF9999(document, ContractProcessResultType.StatusValidationFailed);

            var sut = GetDeserializer();

            // Act
            var actual = await sut.DeserializeAsync(xml);

            // Assert
            actual.Should().BeEquivalentTo(expected, "Because the status validation check failed, but xml is still deserializable.");
            Verify_All();
        }

        [TestMethod, TestCategory("Unit")]
        public async Task Deserialize_FundingTypeValidationFailure_ReturnsErrorResult()
        {
            // Arrange
            string xml = LoadPartialXMLFile();
            var document = new XmlDocument();
            document.LoadXml(xml);

            ILoggerAdapter_SetupLogInformation();
            ILoggerAdapter_SetupLogWarning();
            IAuditService_Setup_TrySend();
            IContractValidationService_Setup_ValidateXmlWithSchema(document);
            IContractValidationService_Setup_ValidateContractStatus();

            Mock.Get(_validationService)
                .Setup(p => p.ValidateFundingTypeAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(false));

            var expected = GeneratePocoForESIF9999(document, ContractProcessResultType.FundingTypeValidationFailed);

            var sut = GetDeserializer();

            // Act
            var actual = await sut.DeserializeAsync(xml);

            // Assert
            actual.Should().BeEquivalentTo(expected, "Because the funding type validation check failed, but xml is still deserializable.");
            Verify_All();
        }

        #endregion


        #region Nullable values

        [TestMethod, TestCategory("Unit")]
        public void Deserialize_NullUKPRN_RaisesException()
        {
            // Arrange
            string xml = LoadPartialXMLFile();
            var document = new XmlDocument();
            document.LoadXml(xml);

            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("c", Deserializer_v1109._contractEvent_Namespace);

            var node = document.SelectSingleNode("/content/c:contract/c:contracts/c:contract/c:contractor/c:ukprn", ns);
            node.ParentNode.RemoveChild(node);

            xml = SaveXMLChangesToXmlString(document);

            ILoggerAdapter_SetupLogInformation();
            IContractValidationService_Setup_ValidateContractStatus();
            IContractValidationService_Setup_ValidateFundingType();
            IContractValidationService_Setup_ValidateXmlWithSchema(document);

            var sut = GetDeserializer();

            // Act
            Func<Task> act = async () => await sut.DeserializeAsync(xml);

            // Assert
            act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Ukprn*", "Because UKPRN is a required value.");
        }

        [TestMethod, TestCategory("Unit")]
        public void Deserialize_MissingParentContractNumber_RaisesException()
        {
            // Arrange
            string xml = LoadPartialXMLFile();
            var document = new XmlDocument();
            document.LoadXml(xml);

            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("c", Deserializer_v1109._contractEvent_Namespace);

            var node = document.SelectSingleNode("/content/c:contract/c:contracts/c:contract/c:parentContractNumber", ns);
            node.ParentNode.RemoveChild(node);

            xml = SaveXMLChangesToXmlString(document);

            ILoggerAdapter_SetupLogInformation();
            IContractValidationService_Setup_ValidateContractStatus();
            IContractValidationService_Setup_ValidateFundingType();
            IContractValidationService_Setup_ValidateXmlWithSchema(document);

            var sut = GetDeserializer();

            // Act
            Func<Task> act = async () => await sut.DeserializeAsync(xml);

            // Assert
            act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*parentContractNumber*", "Because Parent Contract Number is a required value.");
        }

        [TestMethod, TestCategory("Unit")]
        public void Deserialize_MissingFundingStreamPeriodCode_ShouldNotRaiseException()
        {
            // Arrange
            string xml = LoadPartialXMLFile();
            var document = new XmlDocument();
            document.LoadXml(xml);

            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("c", Deserializer_v1109._contractEvent_Namespace);

            var node = document.SelectSingleNode("/content/c:contract/c:contracts/c:contract/c:contractAllocations/c:contractAllocation/c:fundingStreamPeriodCode", ns);
            node.ParentNode.RemoveChild(node);

            ILoggerAdapter_SetupLogInformation();
            IContractValidationService_Setup_ValidateContractStatus();
            IContractValidationService_Setup_ValidateFundingType();
            IContractValidationService_Setup_ValidateXmlWithSchema(document);

            var sut = GetDeserializer();

            // Act
            Func<Task> act = async () => await sut.DeserializeAsync(xml);

            // Assert
            act.Should().NotThrowAsync();
        }

        [TestMethod, TestCategory("Unit")]
        public void Deserialize_Missing_ContractAllocationNumber_ShouldNotRaiseException()
        {
            // Arrange
            string xml = LoadPartialXMLFile();
            var document = new XmlDocument();
            document.LoadXml(xml);

            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("c", Deserializer_v1109._contractEvent_Namespace);

            var node = document.SelectSingleNode("/content/c:contract/c:contracts/c:contract/c:contractAllocations/c:contractAllocation/c:contractAllocationNumber", ns);
            node.ParentNode.RemoveChild(node);

            ILoggerAdapter_SetupLogInformation();
            IContractValidationService_Setup_ValidateContractStatus();
            IContractValidationService_Setup_ValidateFundingType();
            IContractValidationService_Setup_ValidateXmlWithSchema(document);

            var sut = GetDeserializer();

            // Act
            Func<Task> act = async () => await sut.DeserializeAsync(xml);

            // Assert
            act.Should().NotThrowAsync();
        }

        [TestMethod, TestCategory("Unit")]
        public async Task Deserialize_MissingStartDate_ReturnsNullStartDate()
        {
            // Arrange
            string xml = LoadPartialXMLFile();
            var document = new XmlDocument();
            document.LoadXml(xml);

            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("c", Deserializer_v1109._contractEvent_Namespace);

            var node = document.SelectSingleNode("/content/c:contract/c:contracts/c:contract/c:startDate", ns);
            node.ParentNode.RemoveChild(node);

            xml = SaveXMLChangesToXmlString(document);

            ILoggerAdapter_SetupLogInformation();
            IContractValidationService_Setup_ValidateContractStatus();
            IContractValidationService_Setup_ValidateFundingType();
            IContractValidationService_Setup_ValidateXmlWithSchema(document);

            var expected = GeneratePocoForESIF9999(document);
            expected.First().ContractEvent.StartDate = null;

            var sut = GetDeserializer();

            // Act
            var actual = await sut.DeserializeAsync(xml);

            // Assert
            actual.Should().BeEquivalentTo(expected, "Because the start date can be null.");
        }

        [TestMethod, TestCategory("Unit")]
        public async Task Deserialize_MissingEndDate_ReturnsNullEndDate()
        {
            // Arrange
            string xml = LoadPartialXMLFile();
            var document = new XmlDocument();
            document.LoadXml(xml);

            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("c", Deserializer_v1109._contractEvent_Namespace);

            var node = document.SelectSingleNode("/content/c:contract/c:contracts/c:contract/c:endDate", ns);
            node.ParentNode.RemoveChild(node);

            xml = SaveXMLChangesToXmlString(document);

            ILoggerAdapter_SetupLogInformation();
            IContractValidationService_Setup_ValidateContractStatus();
            IContractValidationService_Setup_ValidateFundingType();
            IContractValidationService_Setup_ValidateXmlWithSchema(document);

            var expected = GeneratePocoForESIF9999(document);
            expected.First().ContractEvent.EndDate = null;

            var sut = GetDeserializer();

            // Act
            var actual = await sut.DeserializeAsync(xml);

            // Assert
            actual.Should().BeEquivalentTo(expected, "Because the end date can be null.");
        }

        [TestMethod, TestCategory("Unit")]
        public async Task Deserialize_MissingApprovalDate_ReturnsNullSignedOnDate()
        {
            // Arrange
            string xml = LoadPartialXMLFile();
            var document = new XmlDocument();
            document.LoadXml(xml);

            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("c", Deserializer_v1109._contractEvent_Namespace);

            var node = document.SelectSingleNode("/content/c:contract/c:contracts/c:contract/c:ContractApprovalDate", ns);
            node.ParentNode.RemoveChild(node);

            xml = SaveXMLChangesToXmlString(document);

            ILoggerAdapter_SetupLogInformation();
            IContractValidationService_Setup_ValidateContractStatus();
            IContractValidationService_Setup_ValidateFundingType();
            IContractValidationService_Setup_ValidateXmlWithSchema(document);

            var expected = GeneratePocoForESIF9999(document);
            expected.First().ContractEvent.SignedOn = null;

            var sut = GetDeserializer();

            // Act
            var actual = await sut.DeserializeAsync(xml);

            // Assert
            actual.Should().BeEquivalentTo(expected, "Because the SignedOn date can be null.");
        }
        #endregion


        #region XML Samples

        [TestMethod, TestCategory("Unit")]
        public async Task Deserialize_PartialXML_ReturnsExpectedResult()
        {
            // Arrange
            string xml = LoadPartialXMLFile();
            var document = new XmlDocument();
            document.LoadXml(xml);

            ILoggerAdapter_SetupLogInformation();
            IContractValidationService_Setup_ValidateContractStatus();
            IContractValidationService_Setup_ValidateFundingType();
            IContractValidationService_Setup_ValidateXmlWithSchema(document);

            var expected = GeneratePocoForESIF9999(document);

            var sut = GetDeserializer();

            // Act
            var actual = await sut.DeserializeAsync(xml);

            // Assert
            actual.Should().BeEquivalentTo(expected, "Because the input XML has all required fields.");
            Verify_All();
        }

        [DataRow("main", ContractFundingType.Mainstream)]
        [DataRow("esf", ContractFundingType.Esf)]
        [DataRow("24+loans", ContractFundingType.TwentyFourPlusLoan)]
        [DataRow("age", ContractFundingType.Age)]
        [DataRow("eop", ContractFundingType.Eop)]
        [DataRow("eof", ContractFundingType.Eof)]
        [DataRow("levy", ContractFundingType.Levy)]
        [DataRow("ncs", ContractFundingType.Ncs)]
        [DataRow("1619fund", ContractFundingType.SixteenNineteenFunding)]
        [DataRow("aeb", ContractFundingType.Aebp)]
        [DataRow("nla", ContractFundingType.Nla)]
        [DataRow("loans", ContractFundingType.AdvancedLearnerLoans)]
        [DataRow("edsk", ContractFundingType.EducationAndSkillsFunding)]
        [DataRow("nlg", ContractFundingType.NonLearningGrant)]
        [DataRow("16-18fu", ContractFundingType.SixteenEighteenForensicUnit)]
        [DataRow("dada", ContractFundingType.DanceAndDramaAwards)]
        [DataRow("ccf", ContractFundingType.CollegeCollaborationFund)]
        [DataRow("feca", ContractFundingType.FurtherEducationConditionAllocation)]
        [DataRow("19trn2020", ContractFundingType.ProcuredNineteenToTwentyFourTraineeship)]
        [DataRow("hte-pgf", ContractFundingType.HigherTechnicalEducation)]
        [DataRow("sadf", ContractFundingType.SkillsAcceleratorDevelopmentFund)]
        [DataRow("fe-pdgp", ContractFundingType.FurtherEducationProfessionalDevelopmentGrant)]
        [DataRow("sdfii", ContractFundingType.StrategicDevelopmentFund2)]
        [DataRow("sb", ContractFundingType.SkillsBootcamps)]
        [DataRow("mult", ContractFundingType.Multiply)]
        [DataRow("fe-aca", ContractFundingType.AdditionalCapitalAllocations)]
        [DataRow("hte-sif", ContractFundingType.HigherTechnicalEducationSkillsInjectionFund)]
        [DataRow("fe-rca", ContractFundingType.FEReclassificationCapitalAllocation)]
        [DataRow("fe-ctf", ContractFundingType.FECapitalTransformationFundAllocation)]
        [DataRow("aeb2023", ContractFundingType.AdultEducationBudgetProcured2023, "AEBP23-1001", "AEB2023-AS2324", "AEBA23-1020", "AEBP23-1001-v1-Partial.xml", "2425")]
        [DataRow("sbd", ContractFundingType.SkillsBootcampsDPS)]
        [DataRow("hte-sif2", ContractFundingType.HigherTechnicalEducationSkillsInjectionFund2)]
        [DataRow("aeb2023", ContractFundingType.AdultSkillsFundProcured2023, "ASFP23-1001", "ASF2023-AS2425", "ASFA23-1000", "ASFP23-1001-v1-Partial.xml", "2425")]
        [DataRow("aeb2023", ContractFundingType.AdultSkillsFund, "ASFP23-1002", "ASF2023-AS2526", "ASFA23-1001", "ASFP23-1002-v1-Partial.xml", "2526", "2025-05-01")]
        [DataRow("aeb2023", ContractFundingType.DWPAdultSkillsFund, "ASFP23-1003", "ASF2023-AS2627", "ASFA23-1002", "ASFP23-1003-v1-Partial.xml", "2627", "2026-06-01")]
        [DataRow("ttf", ContractFundingType.TakingTeachingFurther)]
        [DataRow("ttfy2", ContractFundingType.TakingTeachingFurtherYear2)]
        [DataRow("ctec", ContractFundingType.ConstructionTechnicalExcellenceColleges)]
        [DataRow("csp-ipf", ContractFundingType.ConstructionSkillsPackageIndustryPlacementsFund)]
        [DataRow("SomeOtherValue", ContractFundingType.Unknown)]
        [TestMethod, TestCategory("Unit")]
        public async Task Deserialize_PartialXML_ValidateFundingTypeEnum_ReturnsExpectedResult(string fundingType, ContractFundingType expectedType, string contractNumber = null, string fspCode = null, string contractAllocationNumber = null, string xmlFileName = "ESIF-9999-v1-Partial.xml", string period = "1426", string date = "2024-06-01")
        {
            // Arrange
            string xml = LoadPartialXMLFile(xmlFileName);
            var document = new XmlDocument();
            document.LoadXml(xml);

            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("c", Deserializer_v1109._contractEvent_Namespace);

            document.SelectSingleNode("/content/c:contract/c:contracts/c:contract/c:fundingType/c:fundingTypeCode", ns).InnerText = fundingType;

            var expected = GeneratePocoForESIF9999(document);

            if (!string.IsNullOrWhiteSpace(contractNumber))
            {
                document.SelectSingleNode("/content/c:contract/c:contracts/c:contract/c:contractNumber", ns).InnerText = contractNumber;
                expected = GeneratePocoForESIF9999(document, ContractProcessResultType.Successful, contractNumber, contractAllocationNumber, expectedType, period, fspCode, date);
            }

            xml = SaveXMLChangesToXmlString(document);

            ILoggerAdapter_SetupLogInformation();
            IContractValidationService_Setup_ValidateContractStatus();
            IContractValidationService_Setup_ValidateFundingType();
            IContractValidationService_Setup_ValidateXmlWithSchema(document);

            expected.First().ContractEvent.FundingType = expectedType;

            var sut = GetDeserializer();

            // Act
            var actual = await sut.DeserializeAsync(xml);

            // Assert
            actual.Should().BeEquivalentTo(expected, "Because the input XML has all required fields.");
            Verify_All();
        }

        [DataRow("Draft", ContractParentStatus.Draft)]
        [DataRow("Approved", ContractParentStatus.Approved)]
        [DataRow("Withdrawn", ContractParentStatus.Withdrawn)]
        [DataRow("Closed", ContractParentStatus.Closed)]
        [TestMethod, TestCategory("Unit")]
        public async Task Deserialize_PartialXML_ValidateParentContractEnum_ReturnsExpectedResult(string parentStatus, ContractParentStatus expectedParentStatus)
        {
            // Arrange
            string xml = LoadPartialXMLFile();
            var document = new XmlDocument();
            document.LoadXml(xml);

            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("c", Deserializer_v1109._contractEvent_Namespace);

            document.SelectSingleNode("/content/c:contract/c:contracts/c:contract/c:contractStatus/c:parentStatus", ns).InnerText = parentStatus;

            xml = SaveXMLChangesToXmlString(document);

            ILoggerAdapter_SetupLogInformation();
            IContractValidationService_Setup_ValidateContractStatus();
            IContractValidationService_Setup_ValidateFundingType();
            IContractValidationService_Setup_ValidateXmlWithSchema(document);

            var expected = GeneratePocoForESIF9999(document);
            expected.First().ContractEvent.ParentStatus = expectedParentStatus;

            var sut = GetDeserializer();

            // Act
            var actual = await sut.DeserializeAsync(xml);

            // Assert
            actual.Should().BeEquivalentTo(expected, "Because the input XML has all required fields.");
            Verify_All();
        }

        [DataRow("draft", ContractStatus.Draft)]
        [DataRow("approved", ContractStatus.Approved)]
        [DataRow("unassigned", ContractStatus.Unassigned)]
        [DataRow("in review", ContractStatus.InReview)]
        [DataRow("awaiting internal approval", ContractStatus.AwaitingInternalApproval)]
        [DataRow("published to provider", ContractStatus.PublishedToProvider)]
        [DataRow("withdrawn by provider", ContractStatus.WithdrawnByProvider)]
        [DataRow("withdrawn by agency", ContractStatus.WithdrawnByAgency)]
        [DataRow("closed", ContractStatus.Closed)]
        [DataRow("under termination", ContractStatus.UnderTermination)]
        [DataRow("terminated", ContractStatus.Terminated)]
        [DataRow("modified", ContractStatus.Modified)]
        [TestMethod, TestCategory("Unit")]
        public async Task Deserialize_PartialXML_ValidateContractStatusEnum_ReturnsExpectedResult(string contractStatus, ContractStatus expectedStatus)
        {
            // Arrange
            string xml = LoadPartialXMLFile();
            var document = new XmlDocument();
            document.LoadXml(xml);

            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("c", Deserializer_v1109._contractEvent_Namespace);

            document.SelectSingleNode("/content/c:contract/c:contracts/c:contract/c:contractStatus/c:status", ns).InnerText = contractStatus;

            xml = SaveXMLChangesToXmlString(document);

            ILoggerAdapter_SetupLogInformation();
            IContractValidationService_Setup_ValidateContractStatus();
            IContractValidationService_Setup_ValidateFundingType();
            IContractValidationService_Setup_ValidateXmlWithSchema(document);

            var expected = GeneratePocoForESIF9999(document);
            expected.First().ContractEvent.Status = expectedStatus;

            var sut = GetDeserializer();

            // Act
            var actual = await sut.DeserializeAsync(xml);

            // Assert
            actual.Should().BeEquivalentTo(expected, "Because the input XML has all required fields.");
            Verify_All();
        }

        [DataRow("None", ContractAmendmentType.None)]
        [DataRow("Variation", ContractAmendmentType.Variation)]
        [DataRow("Notification", ContractAmendmentType.Notification)]
        [TestMethod, TestCategory("Unit")]
        public async Task Deserialize_PartialXML_ValidateAmendmentTypeEnum_ReturnsExpectedResult(string amendment, ContractAmendmentType expectedAmendment)
        {
            // Arrange
            string xml = LoadPartialXMLFile();
            var document = new XmlDocument();
            document.LoadXml(xml);

            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("c", Deserializer_v1109._contractEvent_Namespace);

            document.SelectSingleNode("/content/c:contract/c:contracts/c:contract/c:amendmentType", ns).InnerText = amendment;

            xml = SaveXMLChangesToXmlString(document);

            ILoggerAdapter_SetupLogInformation();
            IContractValidationService_Setup_ValidateContractStatus();
            IContractValidationService_Setup_ValidateFundingType();
            IContractValidationService_Setup_ValidateXmlWithSchema(document);

            var expected = GeneratePocoForESIF9999(document);
            expected.First().ContractEvent.AmendmentType = expectedAmendment;

            var sut = GetDeserializer();

            // Act
            var actual = await sut.DeserializeAsync(xml);

            // Assert
            actual.Should().BeEquivalentTo(expected, "Because the input XML has all required fields.");
            Verify_All();
        }

        #endregion

        #endregion

        #region Arrange Helpers

        /// <summary>
        /// Generates the poco for eds K341.
        /// </summary>
        /// <returns>Returns a dummy <see cref="ContractProcessResult"/>.</returns>
        /// <remarks>
        /// TODO: This method is not used yet, but will be in the next PR.
        /// </remarks>
        private static ContractProcessResult GeneratePOCOForEDSK3417()
        {
            // Contract Event matching the data from the XML file.
            return new ContractProcessResult()
            {
                ContractEvent = new ContractEvent()
                {
                    AmendmentType = ContractAmendmentType.Notification,
                    ContractAllocations = new List<ContractAllocation>()
                    {
                        new ContractAllocation()
                        {
                            ContractAllocationNumber = "16TR-1565",
                            FundingStreamPeriodCode = "16-18TRN2021",
                            LEPArea = null,
                            TenderSpecTitle = null
                        },
                        new ContractAllocation()
                        {
                            ContractAllocationNumber = "AECS-1252",
                            FundingStreamPeriodCode = "AEBC-CSO2021",
                            LEPArea = null,
                            TenderSpecTitle = null
                        },
                        new ContractAllocation()
                        {
                            ContractAllocationNumber = "AECT-1190",
                            FundingStreamPeriodCode = "AEBC-19TRN2021",
                            LEPArea = null,
                            TenderSpecTitle = null
                        }
                    },
                    ContractEventXml = null,
                    ContractNumber = "EDSK-3417",
                    ContractPeriodValue = "2021",
                    ContractVersion = 3,
                    EndDate = new DateTime(2021, 7, 31),
                    FundingType = ContractFundingType.EducationAndSkillsFunding,
                    ParentContractNumber = "ESFA-20219",
                    ParentStatus = ContractParentStatus.Draft,
                    StartDate = new DateTime(2020, 8, 1),
                    Status = ContractStatus.PublishedToProvider,
                    Type = "Conditions of Funding (Grant) (Trust)",
                    Ukprn = 10038183,
                    Value = 206967m
                },
                Result = ContractProcessResultType.Successful
            };
        }

        private static IList<ContractProcessResult> GeneratePocoForESIF9999(
            XmlDocument document = null,
            ContractProcessResultType result = ContractProcessResultType.Successful,
            string contractNumber = "ESIF-9999",
            string contractAllocationNumber = "ESF-9999",
            ContractFundingType contractFundingType = ContractFundingType.Esf,
            string period = "1426",
            string fspCode = "ESF1420",
            string date = "2024-06-01")
        => new List<ContractProcessResult>
        {
            new ContractProcessResult
            {
                ContractEvent = new ContractEvent()
                {
                    AmendmentType = ContractAmendmentType.Variation,
                    BookmarkId = Guid.Empty,
                    ContractAllocations = new List<ContractAllocation>()
                    {
                        new ContractAllocation()
                        {
                            ContractAllocationNumber = contractAllocationNumber,
                            FundingStreamPeriodCode = fspCode,
                            LEPArea = "LEP Name",
                            TenderSpecTitle = "SSW"
                        }
                    },
                    ContractEventXml = null,
                    ContractNumber = contractNumber,
                    ContractPeriodValue = period,
                    ContractVersion = 1,
                    EndDate = DateTime.Parse(date).AddDays(303),
                    FundingType = contractFundingType,
                    ParentContractNumber = "ESFA-10001",
                    ParentStatus = ContractParentStatus.Draft,
                    StartDate = DateTime.Parse(date),
                    SignedOn = DateTime.Parse(date).AddDays(1),
                    Status = ContractStatus.PublishedToProvider,
                    Type = "Contract for Services",
                    Ukprn = 10000001,
                    Value = 9099922.0000m
                },
                Result = result,
                ContractXml = document
            }
        };

        private static string SaveXMLChangesToXmlString(XmlDocument document)
        {
            var stringWriter = new StringWriter();
            var xmlTextWriter = new XmlTextWriter(stringWriter);
            document.WriteTo(xmlTextWriter);
            return stringWriter.ToString();
        }

        private Deserializer_v1109 GetDeserializer()
            => new Deserializer_v1109(_validationService, _auditService, _loggerAdapter);

        private string LoadPartialXMLFile(string filename = "ESIF-9999-v1-Partial.xml")
        {
            string filePath = Path.Combine(_baseDirectory, filename);
            var document = new XmlDocument();
            document.Load(filePath);
            var xml = document.SelectSingleNode("/entry/content").OuterXml;
            return xml;
        }

        private void ILoggerAdapter_SetupLogInformation()
        {
            Mock.Get(_loggerAdapter)
                .Setup(p => p.LogInformation(It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();
        }

        private void ILoggerAdapter_SetupLogWarning()
        {
            Mock.Get(_loggerAdapter)
                .Setup(p => p.LogWarning(It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();
        }

        private void IContractValidationService_Setup_ValidateXmlWithSchema(XmlDocument document = null)
        {
            Mock.Get(_validationService)
                .Setup(p => p.ValidateXmlWithSchema(It.IsAny<string>()))
                .Returns(document)
                .Verifiable();
        }

        private void IContractValidationService_Setup_ValidateContractStatus()
        {
            Mock.Get(_validationService)
                .Setup(p => p.ValidateContractStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(true))
                .Verifiable();
        }

        private void IContractValidationService_Setup_ValidateFundingType()
        {
            Mock.Get(_validationService)
                .Setup(p => p.ValidateFundingTypeAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(true))
                .Verifiable();
        }

        private void IAuditService_Setup_TrySend()
        {
            Mock.Get(_auditService)
                .Setup(p => p.TrySendAuditAsync(It.IsAny<Audit.Api.Client.Models.Audit>()))
                .Returns(Task.CompletedTask)
                .Verifiable();
        }

        #endregion


        #region Verify All

        private void Verify_All()
        {
            Mock.Get(_loggerAdapter)
                .VerifyAll();
            Mock.Get(_validationService)
                .VerifyAll();

            Mock.Get(_auditService)
                .VerifyAll();
        }

        #endregion
    }
}
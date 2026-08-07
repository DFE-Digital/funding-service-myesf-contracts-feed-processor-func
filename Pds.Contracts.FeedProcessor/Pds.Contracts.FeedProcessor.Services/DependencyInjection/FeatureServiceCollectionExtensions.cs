using Azure.Storage.Blobs;
using Mapster;
using MapsterMapper;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pds.Audit.Api.Client.Registrations;
using Pds.Contracts.FeedProcessor.Services.Configuration;
using Pds.Contracts.FeedProcessor.Services.Extensions;
using Pds.Contracts.FeedProcessor.Services.Implementations;
using Pds.Contracts.FeedProcessor.Services.Interfaces;
using Pds.Contracts.FeedProcessor.Services.Mapster;
using Pds.Contracts.FeedProcessor.Services.Models;
using Pds.Core.ApiClient.Interfaces;
using Pds.Core.ApiClient.Services;
using Pds.Core.Logging;
using Pds.Core.Utils.Implementations;
using Pds.Core.Utils.Interfaces;

namespace Pds.Contracts.FeedProcessor.Services.DependencyInjection
{
    /// <summary>
    /// Extensions class for <see cref="IServiceCollection"/> for registering the feature's services.
    /// </summary>
    public static class FeatureServiceCollectionExtensions
    {
        /// <summary>
        /// Adds services for the current feature to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add the feature's services to.</param>
        /// <param name="config">The configuration.</param>
        /// <returns>
        /// A reference to this instance after the operation has completed.
        /// </returns>
        public static IServiceCollection AddFeatureServices(this IServiceCollection services, IConfiguration config)
        {
            var feedReaderConfig = new FeedReaderOptions();
            config.GetSection(nameof(FeedReaderOptions)).Bind(feedReaderConfig);

            var storageSettings = new AzureStorageAccountOptions();
            config.GetSection(nameof(AzureStorageAccountOptions)).Bind(storageSettings);

            services.Configure<AzureStorageAccountOptions>(config.GetSection(nameof(AzureStorageAccountOptions)));
            services.Configure<SchemaValidationSettings>(config.GetSection(nameof(SchemaValidationSettings)));
            services.Configure<AzureStorageAccountOptions>(config.GetSection(nameof(AzureStorageAccountOptions)));

            if (CloudStorageAccount.TryParse(storageSettings.ConnectionString, out var cloudStorageAccount))
            {
                services.AddScoped(s => CreateCloudTableClient(cloudStorageAccount, storageSettings));
                services.AddScoped(s => s.GetRequiredService<CloudTableClient>()?.GetTableReference(storageSettings.TableAccessOptions.ConfigTableName));
            }

            services.AddScoped<IFeedProcessor, Implementations.FeedProcessor>();
            services.AddScoped<IContractEventSessionQueuePopulator, ContractEventSessionQueuePopulator>();
            services.AddScoped<IContractEventProcessor, ContractEventProcessor>();
            services.AddScoped<IFeedProcessorConfiguration, FeedProcessorConfiguration>();
            services.AddScoped<IConfigReader, AzureTableStorageConfigReader>();

            var policies = new Configuration.PolicyType[] { Configuration.PolicyType.Retry, Configuration.PolicyType.CircuitBreaker };
            var policyRegistry = services.AddPolicyRegistry();
            services
                .AddPolicies<IFcsFeedReaderService>(feedReaderConfig.HttpPolicyOptions, policyRegistry)
                .AddHttpClient<IFcsFeedReaderService, FcsFeedReaderService, FeedReaderOptions>(config, policies);

            services.AddTransient(typeof(IAuthenticationService<>), typeof(AuthenticationService<>));
            services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

            services.AddAuditApiClient(config, policyRegistry);

            services.AddScoped<IDeserilizationService<ContractProcessResult>, Deserializer_v1109>();
            services.AddScoped<IBlobStorageService, BlobStorageService>((serviceProvider) =>
            {
                return new BlobStorageService(GetBlobContainerClient(storageSettings), serviceProvider.GetRequiredService<ILoggerAdapter<BlobStorageService>>());
            });
            services.AddScoped<IContractEventValidationService, ContractEventValidationService>();

            services.AddSingleton(new TypeAdapterConfig().Configure());
            services.AddSingleton<IMapper, ServiceMapper>();

            return services;
        }

        private static BlobContainerClient GetBlobContainerClient(AzureStorageAccountOptions storageSettings)
        {
            var blobClientOptions = new BlobClientOptions();
            blobClientOptions.Retry.MaxRetries = storageSettings.BlobAccessOptions.RetryCount;
            blobClientOptions.Retry.Mode = Azure.Core.RetryMode.Exponential;
            blobClientOptions.Retry.Delay = storageSettings.BlobAccessOptions.Delay;

            return new BlobContainerClient(storageSettings.ConnectionString, storageSettings.BlobAccessOptions.XmlStorageContainer, blobClientOptions);
        }

        private static CloudTableClient CreateCloudTableClient(CloudStorageAccount cloudStorageAccount, AzureStorageAccountOptions storageSettings)
        {
            var tableClient = cloudStorageAccount.CreateCloudTableClient();
            tableClient.DefaultRequestOptions = new TableRequestOptions
            {
                RetryPolicy = new ExponentialRetry(storageSettings.TableAccessOptions.DeltaBackOff, storageSettings.TableAccessOptions.MaxAttempts)
            };

            return tableClient;
        }
    }
}
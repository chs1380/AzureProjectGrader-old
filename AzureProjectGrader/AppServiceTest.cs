﻿
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.Storage.Models;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using Azure.Data.Tables;

namespace AzureProjectGrader
{
    class AppServiceTest
    {
        private IAppServiceManager client;
        private IAppServicePlan appServicePlan;
        private IFunctionApp functionApp;
        private StorageAccount storageAccount;
        private Table messageTable;
        private StorageQueue jobQueue;

        private static readonly HttpClient httpClient = new HttpClient();

        [SetUp]
        public void Setup()
        {
            var config = new Config();
            client = AppServiceManager.Configure().Authenticate(config.Credentials, config.SubscriptionId);
            appServicePlan = client.AppServicePlans.ListByResourceGroup(Constants.ResourceGroupName).FirstOrDefault(c => c.Tags.ContainsKey("key") && c.Tags["key"] == "AppServicePlan");
            functionApp = client.FunctionApps.ListByResourceGroup(Constants.ResourceGroupName).FirstOrDefault(c => c.Tags.ContainsKey("key") && c.Tags["key"] == "FunctionApp");

            var storageAccountTest = new StorageAccountTest();
            storageAccount = storageAccountTest.GetLogicStorageAccount(storageAccountTest.GetStorageAccounts());
            messageTable = storageAccountTest.GetMessageTable();
            jobQueue = storageAccountTest.GetJobQueue();
                 
            storageAccountTest.TearDown();
        }

        [Test]
        public void Test01_AppServicePlanWithTag()
        {
            Assert.IsNotNull(appServicePlan, "AppService Plans with tag {key:AppServicePlan}.");
        }

        [Test]
        public void Test02_FunctionAppsWithTag()
        {
            Assert.IsNotNull(functionApp, "Function App Plans with tag {key:FunctionApp}.");
        }

        [Test]
        public void Test03_AppServicePlanSettings()
        {
            Assert.AreEqual("southeastasia", appServicePlan.Region.Name);
            Assert.AreEqual("Dynamic", appServicePlan.PricingTier.SkuDescription.Tier);
            Assert.AreEqual("Y1", appServicePlan.PricingTier.SkuDescription.Size);
            Assert.AreEqual("Windows", appServicePlan.OperatingSystem.ToString());
        }

        [Test]
        public void Test04_FunctionAppSettings()
        {
            Assert.AreEqual("southeastasia", functionApp.Region.Name);
            IReadOnlyDictionary<string, IAppSetting> appSettings = functionApp.GetAppSettings();
            Assert.AreEqual("~3", appSettings["FUNCTIONS_EXTENSION_VERSION"].Value.ToString());
            Assert.AreEqual("node", appSettings["FUNCTIONS_WORKER_RUNTIME"].Value.ToString());
            Assert.AreEqual("~14", appSettings["WEBSITE_NODE_DEFAULT_VERSION"].Value.ToString());
            Assert.That(appSettings["WEBSITE_RUN_FROM_PACKAGE"].Value.ToString(), Does.StartWith($"https://{storageAccount.Name}.blob.core.windows.net/code/"));
            Assert.That(appSettings["WEBSITE_RUN_FROM_PACKAGE"].Value.ToString(), Does.EndWith("app.zip"));
            Assert.That(appSettings["StorageConnectionAppSetting"].Value.ToString(), Does.StartWith($"DefaultEndpointsProtocol=https;AccountName={storageAccount.Name};AccountKey="));
            Assert.That(appSettings["WEBSITE_CONTENTAZUREFILECONNECTIONSTRING"].Value.ToString(), Does.StartWith($"DefaultEndpointsProtocol=https;AccountName={storageAccount.Name};AccountKey="));
        }

        [Test]
        public void Test05_FunctionAppSettingsInstrumentationKey()
        {
            var applicationInsightTest = new ApplicationInsightTest();
            IReadOnlyDictionary<string, IAppSetting> appSettings = functionApp.GetAppSettings();
            Assert.AreEqual(applicationInsightTest.GetApplicationInsights().InstrumentationKey, appSettings["APPINSIGHTS_INSTRUMENTATIONKEY"].Value);
        }

        [Test]
        public void Test04_AzureFunctionBinding()
        {
            var helloFunction = functionApp.ListFunctions()[0];
            string functionjs = "{\"disabled\":false,\"bindings\":[{\"type\":\"httpTrigger\",\"name\":\"req\",\"direction\":\"in\",\"dataType\":\"string\",\"authLevel\":\"anonymous\",\"methods\":[\"get\"]},{\"type\":\"http\",\"direction\":\"out\",\"name\":\"res\"},{\"type\":\"queue\",\"name\":\"jobQueue\",\"queueName\":\"job\",\"direction\":\"out\",\"connection\":\"StorageConnectionAppSetting\"},{\"tableName\":\"message\",\"name\":\"messageTable\",\"type\":\"table\",\"direction\":\"out\",\"connection\":\"StorageConnectionAppSetting\"}]}";
            var configJsonString = JsonConvert.SerializeObject(helloFunction.Config);
            dynamic actural = JsonConvert.DeserializeObject(configJsonString);
            dynamic expected = JsonConvert.DeserializeObject(functionjs);
            Assert.AreEqual(expected, actural);
        }

        [Test]
        public async Task Test05_AzureFunctionCallWithHttpResponse()
        {
            var helloFunction = functionApp.ListFunctions()[0];
            var message = DateTime.Now.ToString("yyyy’-‘MM’-‘dd’T’HH’:’mm’:’ss");
            var url = helloFunction.Inner.InvokeUrlTemplate + "?user=tester&message=" + message;
            Console.WriteLine(url);
            var helloResponse = await httpClient.GetStringAsync(url);
            var expected = $@"Hello, tester and I received your message: {message}";
            Assert.AreEqual(expected, helloResponse);   
        }
    }

}

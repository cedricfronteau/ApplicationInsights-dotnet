﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Net.Http;
using System.Threading.Tasks;
using E2ETests.Helpers;
using AI;
using Microsoft.ApplicationInsights.DataContracts;
using System.Collections;
using System.Collections.Generic;

namespace E2ETests
{
    public abstract class Test452Base
    {
        internal const string WebAppInstrumentationKey = "e45209bb-49ab-41a0-8065-793acb3acc56";
        internal const string WebApiInstrumentationKey = "0786419e-d901-4373-902a-136921b63fb2";
        internal const string ContainerNameWebApp = "e2etests_e2etestwebapp_1";
        internal const string ContainerNameWebApi = "e2etests_e2etestwebapi_1";
        internal const string ContainerNameIngestionService = "e2etests_ingestionservice_1";
        private const int AISDKBufferFlushTime = 2000;
        internal static string testwebAppip;
        internal static string testwebApiip;
        internal static string ingestionServiceIp;
        internal static string DockerComposeFileName = "docker-compose.yml";
        internal static string DockerComposeBaseCommandFormat = "/c docker-compose";
        internal static string DockerComposeFileNameFormat;


        internal static DataEndpointClient dataendpointClient;
        internal static ProcessStartInfo DockerPSProcessInfo = new ProcessStartInfo("cmd", "/c docker ps -a");

        public static void MyClassInitializeBase()
        {
            Trace.WriteLine("Starting ClassInitialize:" + DateTime.UtcNow.ToLongTimeString());
            Assert.IsTrue(File.Exists(".\\" + DockerComposeFileName));

            DockerComposeFileNameFormat = string.Format("-f {0}", DockerComposeFileName);
            DockerComposeGenericCommandExecute("up -d --build");

            PrintDockerProcessStats("Docker-Compose -build");
            Thread.Sleep(1000);

            // Inspect Docker containers to get IP addresses
            testwebAppip = DockerInspectIPAddress(ContainerNameWebApp, 3);
            testwebApiip = DockerInspectIPAddress(ContainerNameWebApi, 3);
            ingestionServiceIp = DockerInspectIPAddress(ContainerNameIngestionService, 3);

            HealthCheckAndRestartIfNeeded("WebApi", testwebApiip, "/api/values", true);
            HealthCheckAndRestartIfNeeded("WebApp", testwebAppip, "/Default", true);                        
            
            dataendpointClient = new DataEndpointClient(new Uri("http://" + ingestionServiceIp));

            Thread.Sleep(5000);
            Trace.WriteLine("Completed ClassInitialize:" + DateTime.UtcNow.ToLongTimeString());
        }
        
        public static void MyClassCleanupBase()
        {
            Trace.WriteLine("Started Class Cleanup:" + DateTime.UtcNow.ToLongTimeString());
            DockerComposeGenericCommandExecute("down");
            Trace.WriteLine("Completed Class Cleanup:" + DateTime.UtcNow.ToLongTimeString());

            PrintDockerProcessStats("Docker-Compose down");
        }

        public void MyTestInitialize()
        {
            Trace.WriteLine("Started Test Initialize:" + DateTime.UtcNow.ToLongTimeString());
            RemoveIngestionItems();
            PrintDockerProcessStats("After MyTestInitialize" + DateTime.UtcNow.ToLongTimeString());
            Trace.WriteLine("Completed Test Initialize:" + DateTime.UtcNow.ToLongTimeString());
        }
        
        public void MyTestCleanup()
        {
            Trace.WriteLine("Started Test Cleanup:" + DateTime.UtcNow.ToLongTimeString());
            PrintDockerProcessStats("After MyTestCleanup" + DateTime.UtcNow.ToLongTimeString());
            Trace.WriteLine("Completed Test Cleanup:" + DateTime.UtcNow.ToLongTimeString());
        }
        
        public void TestBasicRequestWebApp()
        {
            var expectedRequestTelemetry = new RequestTelemetry();
            expectedRequestTelemetry.ResponseCode = "200";

            ValidateBasicRequestAsync(testwebAppip, "/Default", expectedRequestTelemetry, WebAppInstrumentationKey).Wait();
        }

        public void TestXComponentWebAppToWebApi()
        {
            var expectedRequestTelemetryWebApp = new RequestTelemetry();
            expectedRequestTelemetryWebApp.ResponseCode = "200";            

            var expectedDependencyTelemetryWebApp = new DependencyTelemetry();
            expectedDependencyTelemetryWebApp.Type = "Http";
            expectedDependencyTelemetryWebApp.Success = true;

            var expectedRequestTelemetryWebApi = new RequestTelemetry();
            expectedRequestTelemetryWebApi.ResponseCode = "200";

            ValidateXComponentWebAppToWebApi(testwebAppip, "/Dependencies?type=http", 
                expectedRequestTelemetryWebApp, expectedDependencyTelemetryWebApp, expectedRequestTelemetryWebApi,
                WebAppInstrumentationKey, WebApiInstrumentationKey).Wait();
        }

        public void TestBasicHttpDependencyWebApp()
        {
            var expectedDependencyTelemetry = new DependencyTelemetry();
            expectedDependencyTelemetry.Type = "Http";
            expectedDependencyTelemetry.Success = true;

            ValidateBasicDependencyAsync(testwebAppip, "/Dependencies.aspx?type=http", expectedDependencyTelemetry,
                WebAppInstrumentationKey).Wait();
        }

        public void TestBasicSqlDependencyWebApp()
        {
            var expectedDependencyTelemetry = new DependencyTelemetry();
            expectedDependencyTelemetry.Type = "SQL";
            expectedDependencyTelemetry.Success = true;

            ValidateBasicDependencyAsync(testwebAppip, "/Dependencies.aspx?type=sql", expectedDependencyTelemetry,
                WebAppInstrumentationKey).Wait();
        }

        private async Task ValidateXComponentWebAppToWebApi(string sourceInstanceIp, string sourcePath,
            RequestTelemetry expectedRequestTelemetrySource,
            DependencyTelemetry expectedDependencyTelemetrySource,
            RequestTelemetry expectedRequestTelemetryTarget,
            string sourceIKey, string targetIKey)
        {
            HttpClient client = new HttpClient();
            string url = "http://" + sourceInstanceIp + sourcePath;
            Trace.WriteLine("Hitting the target url:" + url);
            var response = await client.GetAsync(url);
            Trace.WriteLine("Actual Response code: " + response.StatusCode);
            Thread.Sleep(AISDKBufferFlushTime);
            var requestsSource = dataendpointClient.GetItemsOfType<TelemetryItem<AI.RequestData>>(sourceIKey);
            var dependenciesSource = dataendpointClient.GetItemsOfType<TelemetryItem<AI.RemoteDependencyData>>(sourceIKey);
            var requestsTarget = dataendpointClient.GetItemsOfType<TelemetryItem<AI.RequestData>>(targetIKey);

            Trace.WriteLine("RequestCount for Source:" + requestsSource.Count);
            Assert.IsTrue(requestsSource.Count == 1);

            Trace.WriteLine("RequestCount for Target:" + requestsTarget.Count);
            Assert.IsTrue(requestsTarget.Count == 1);

            Trace.WriteLine("Dependencies count for Source:" + dependenciesSource.Count);
            Assert.IsTrue(dependenciesSource.Count == 1);

            var requestSource = requestsSource[0];
            var requestTarget = requestsTarget[0];
            var dependencySource = dependenciesSource[0];

            Assert.IsTrue(requestSource.tags["ai.operation.id"].Equals(requestTarget.tags["ai.operation.id"]), 
                "Operation id for request telemetry in source and target must be same.");

            Assert.IsTrue(requestSource.tags["ai.operation.id"].Equals(dependencySource.tags["ai.operation.id"]),
                "Operation id for request telemetry dependency telemetry in source must be same.");

        }

        private async Task ValidateBasicRequestAsync(string targetInstanceIp, string targetPath,
            RequestTelemetry expectedRequestTelemetry, string ikey)
        {
            HttpClient client = new HttpClient();
            string url = "http://" + targetInstanceIp + targetPath;
            Trace.WriteLine("Hitting the target url:" + url);
            var response = await client.GetAsync(url);
            Trace.WriteLine("Actual Response code: " + response.StatusCode);
            Thread.Sleep(AISDKBufferFlushTime);
            var requestsWebApp = dataendpointClient.GetItemsOfType<TelemetryItem<AI.RequestData>>(ikey);

            Trace.WriteLine("RequestCount for WebApp:" + requestsWebApp.Count);
            Assert.IsTrue(requestsWebApp.Count == 1);
            var request = requestsWebApp[0];
            Assert.AreEqual(expectedRequestTelemetry.ResponseCode, request.data.baseData.responseCode, "Response code is incorrect");
        }

        private async Task ValidateBasicDependencyAsync(string targetInstanceIp, string targetPath,
            DependencyTelemetry expectedDependencyTelemetry, string ikey)
        {
            HttpClient client = new HttpClient();
            string url = "http://" + targetInstanceIp + targetPath;
            Trace.WriteLine("Hitting the target url:" + url);
            try
            {
                var response = await client.GetStringAsync(url);
                Trace.WriteLine("Actual Response text: " + response.ToString());
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Exception occured:" + ex);
            }
            Thread.Sleep(AISDKBufferFlushTime);
            var dependenciesWebApp = dataendpointClient.GetItemsOfType<TelemetryItem<AI.RemoteDependencyData>>(ikey);
            PrintDependencies(dependenciesWebApp);

            Trace.WriteLine("Dependencies count for WebApp:" + dependenciesWebApp.Count);
            Assert.IsTrue(dependenciesWebApp.Count == 1);
            var dependency = dependenciesWebApp[0];
            Assert.AreEqual(expectedDependencyTelemetry.Type, dependency.data.baseData.type, "Dependency Type is incorrect");
            Assert.AreEqual(expectedDependencyTelemetry.Success, dependency.data.baseData.success, "Dependency success is incorrect");
        }

        private void PrintDependencies(IList<TelemetryItem<AI.RemoteDependencyData>> dependencies)
        {
            foreach (var deps in dependencies)
            {
                Trace.WriteLine("Dependency Item Details");
                Trace.WriteLine("deps.time: " + deps.time);
                Trace.WriteLine("deps.iKey: " + deps.iKey);
                Trace.WriteLine("deps.data.baseData.name:" + deps.data.baseData.name);
                Trace.WriteLine("deps.data.baseData.type:" + deps.data.baseData.type);
                Trace.WriteLine("deps.data.baseData.target:" + deps.data.baseData.target);
                Trace.WriteLine("--------------------------------------");
            }
        }

        private static void PrintDockerProcessStats(string message)
        {
            Trace.WriteLine("Docker PS Stats at " + message);
            Process process = new Process { StartInfo = DockerPSProcessInfo };
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            Trace.WriteLine("Docker ps -a" + output);
            process.WaitForExit();
        }

        private static void DockerComposeGenericCommandExecute(string action)
        {
            string dockerComposeFullCommandFormat = string.Format("{0} {1} {2}", DockerComposeBaseCommandFormat, DockerComposeFileNameFormat, action);
            Trace.WriteLine("Docker compose using command: " + dockerComposeFullCommandFormat);
            ProcessStartInfo DockerComposeStop = new ProcessStartInfo("cmd", dockerComposeFullCommandFormat);

            Process process = new Process { StartInfo = DockerComposeStop };
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            Trace.WriteLine("Docker Compose output:" + output);
            process.WaitForExit();
        }

        private static string DockerInspectIPAddress(string containerName, int maxCount)
        {
            int i = 1;
            string ip = DockerInspectIPAddress(containerName);
            while(i <= maxCount && string.IsNullOrWhiteSpace(ip))
            {
                i++;
                Trace.WriteLine(string.Format("Attempt {0} to get ip adress for {1}.", i, containerName));
                ip = DockerInspectIPAddress(containerName);
            }
            return ip;
        }
        private static string DockerInspectIPAddress(string containerName)
        {
            string dockerInspectIpBaseCommand = "/c docker inspect -f \"{{.NetworkSettings.Networks.nat.IPAddress}}\" ";
            string dockerInspectIpCommand = dockerInspectIpBaseCommand + containerName;
            ProcessStartInfo DockerInspectIp = new ProcessStartInfo("cmd", dockerInspectIpCommand);
            Trace.WriteLine("DockerInspectIp done using command:" + dockerInspectIpCommand);

            Process process = new Process { StartInfo = DockerInspectIp };
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string ip = output.Trim();
            Trace.WriteLine(string.Format("DockerInspect IP for {0} is {1}", containerName, ip));
            process.WaitForExit();
            return ip;
        }

        private void RemoveIngestionItems()
        {
            Trace.WriteLine("Deleting items started:" + DateTime.UtcNow.ToLongTimeString());
            dataendpointClient.DeleteItems(WebAppInstrumentationKey);
            dataendpointClient.DeleteItems(WebApiInstrumentationKey);
            Trace.WriteLine("Deleting items completed:" + DateTime.UtcNow.ToLongTimeString());
        }

        private static void HealthCheckAndRestartIfNeeded(string displayName, string ip, string path, bool restartDockerCompose)
        {
            string url = "http://" + ip + path;
            Trace.WriteLine(string.Format("Request fired against {0} using url: {1}", displayName, url));
            try
            {                
                var response = new HttpClient().GetAsync(url);
                Trace.WriteLine(string.Format("Response from {0} : {1}", url, response.Result.StatusCode));
            }
            catch(Exception ex)
            {
                Trace.WriteLine(string.Format("Exception occuring hitting {0} : {1}", url, ex.Message));
                if(restartDockerCompose)
                {
                    PrintDockerProcessStats("Before Attempting To Repair.");
                    DockerComposeGenericCommandExecute("restart");
                    PrintDockerProcessStats("Aftet Attempting To Repair by restart.");

                    Trace.WriteLine("Rechecking health. Failure here will abort test run.");
                    var response = new HttpClient().GetAsync(url);
                    Trace.WriteLine(string.Format("Response from {0} : {1}", url, response.Result.StatusCode));
                }
            }
        }
    }
}

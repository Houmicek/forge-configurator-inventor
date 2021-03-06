﻿/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Design Automation team for Inventor
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using Autodesk.Forge.Core;
using Autodesk.Forge.DesignAutomation;
using Autodesk.Forge.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using WebApplication.Definitions;
using WebApplication.Middleware;
using WebApplication.Processing;
using WebApplication.Services;
using WebApplication.State;
using WebApplication.Utilities;
using Xunit;
using Project = WebApplication.State.Project;

namespace WebApplication.Tests
{
    public class InitializerIntegrationTest : IAsyncLifetime
    {
        const string testZippedIamUrl = "http://testipt.s3-us-west-2.amazonaws.com/Basic.zip";
        const string testIamPathInZip = "iLogicBasic1.iam";

        readonly ForgeOSS forgeOSS;
        readonly string projectsBucketKey;
        readonly Initializer initializer;
        readonly DirectoryInfo testFileDirectory;
        readonly HttpClient httpClient;

        public InitializerIntegrationTest()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false)
                .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddForgeAlternativeEnvironmentVariables()
                .Build();

            IServiceCollection services = new ServiceCollection();
            services.AddHttpClient();
            var serviceProvider = services.BuildServiceProvider();

            ForgeConfiguration forgeConfiguration = configuration.GetSection("Forge").Get<ForgeConfiguration>();
            IOptions<ForgeConfiguration> forgeConfigOptions = Options.Create(forgeConfiguration);

            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

            forgeOSS = new ForgeOSS(httpClientFactory, forgeConfigOptions, new NullLogger<ForgeOSS>());

            var httpMessageHandler = new ForgeHandler(Options.Create(forgeConfiguration))
            {
                InnerHandler = new HttpClientHandler()
            };
            var forgeService = new ForgeService(new HttpClient(httpMessageHandler));
            var designAutomationClient = new DesignAutomationClient(forgeService);

            projectsBucketKey = Guid.NewGuid().ToString();
            
            var resourceProvider = new ResourceProvider(forgeConfigOptions, designAutomationClient, null, projectsBucketKey);
            var localCache = new LocalCache();
            var postProcessing = new PostProcessing(httpClientFactory, new NullLogger<PostProcessing>(), localCache, Options.Create(new ProcessingOptions()));
            var publisher = new Publisher(designAutomationClient, new NullLogger<Publisher>(), resourceProvider, postProcessing);

            var appBundleZipPathsConfiguration = new AppBundleZipPaths
            {
                EmptyExe = "../../../../WebApplication/AppBundles/EmptyExePlugin.bundle.zip",
                CreateSVF = "../../../../WebApplication/AppBundles/CreateSVFPlugin.bundle.zip",
                CreateThumbnail = "../../../../WebApplication/AppBundles/CreateThumbnailPlugin.bundle.zip",
                ExtractParameters = "../../../../WebApplication/AppBundles/ExtractParametersPlugin.bundle.zip",
                UpdateParameters = "../../../../WebApplication/AppBundles/UpdateParametersPlugin.bundle.zip",
                CreateSAT = "../../../../WebApplication/AppBundles/SatExportPlugin.bundle.zip",
                CreateRFA = "../../../../WebApplication/AppBundles/RFAExportPlugin.bundle.zip",
                CreateBOM = "../../../../WebApplication/AppBundles/ExportBOMPlugin.bundle.zip",
                ExportDrawing = "../../../../WebApplication/AppBundles/ExportDrawingAsPdfPlugin.bundle.zip"
            };
            IOptions<AppBundleZipPaths> appBundleZipPathsOptions = Options.Create(appBundleZipPathsConfiguration);

            var fdaClient = new FdaClient(publisher, appBundleZipPathsOptions);
            var defaultProjectsConfiguration = new DefaultProjectsConfiguration
            {
                Projects = new [] { new DefaultProjectConfiguration { Url = testZippedIamUrl, TopLevelAssembly = testIamPathInZip, Name = "Basic" } }
            };
            IOptions<DefaultProjectsConfiguration> defaultProjectsOptions = Options.Create(defaultProjectsConfiguration);
            var userResolver = new UserResolver(resourceProvider, forgeOSS, forgeConfigOptions, localCache, NullLogger<UserResolver>.Instance, null);
            var arranger = new Arranger(httpClientFactory, userResolver);

            // TODO: linkGenerator should be mocked
            var dtoGenerator = new DtoGenerator(linkGenerator: null, localCache);
            var projectWork = new ProjectWork(new NullLogger<ProjectWork>(), arranger, fdaClient, dtoGenerator, userResolver);
            initializer = new Initializer(new NullLogger<Initializer>(), fdaClient, 
                                            defaultProjectsOptions, projectWork, userResolver, localCache);

            testFileDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            httpClient = new HttpClient();
        }

        public async Task InitializeAsync()
        {
            await initializer.ClearAsync(false);
        }

        public async Task DisposeAsync()
        {
            testFileDirectory.Delete(true);
            httpClient.Dispose();
            await initializer.ClearAsync(false);
        }

        private async Task<string> DownloadTestComparisonFile(string url, string name)
        {
            HttpResponseMessage response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            string path = Path.Combine(testFileDirectory.FullName, name);
            using (var fs = new FileStream(path, FileMode.CreateNew))
            {
                await response.Content.CopyToAsync(fs);
            }
            return path;
        }

        private async Task CompareOutputFileBytes(string expectedResultFileName, string outputFileUrl)
        {
            // download result, compare it to expected value
            byte[] expectedBytes = await File.ReadAllBytesAsync(expectedResultFileName);
            byte[] generatedOutputFileBytes = await httpClient.GetByteArrayAsync(outputFileUrl);

            // first confirm they are the same number of bytes
            Assert.Equal(expectedBytes.Length, generatedOutputFileBytes.Length);

            // then confirm each byte is the same
            for (int i = 0; i < expectedBytes.Length; i++)
            {
                Assert.True(expectedBytes[i] == generatedOutputFileBytes[i], $"unequal bytes at index {i}");
            }
        }

        [Fact]
        public async Task InitializeTestAsync()
        {
            await initializer.InitializeAsync();

            var project = new Project("Basic", Path.GetTempPath());

            // check thumbnail generated
            List<ObjectDetails> objects = await forgeOSS.GetBucketObjectsAsync(projectsBucketKey, project.OssAttributes.Thumbnail);
            Assert.Single(objects);
            string signedOssUrl = await forgeOSS.CreateSignedUrlAsync(projectsBucketKey, objects[0].ObjectKey);
            string testComparisonFilePath = await DownloadTestComparisonFile("http://testipt.s3-us-west-2.amazonaws.com/iLogicBasic1IamThumbnail.png", "iLogicBasic1IamThumbnail.png");
            await CompareOutputFileBytes(testComparisonFilePath, signedOssUrl);

            // check parameters generated with hashed name
            var ossNames = project.OssNameProvider("13B8EF6A8506CC3ECB08FF6F0B09ACD194DE6A55");
            objects = await forgeOSS.GetBucketObjectsAsync(projectsBucketKey, ossNames.Parameters);
            Assert.Single(objects);
            signedOssUrl = await forgeOSS.CreateSignedUrlAsync(projectsBucketKey, objects[0].ObjectKey);
            testComparisonFilePath = await DownloadTestComparisonFile("http://testipt.s3-us-west-2.amazonaws.com/iLogicBasic1IamDocumentParams.json", "iLogicBasic1IamDocumentParams.json");
            await CompareOutputFileBytes(testComparisonFilePath, signedOssUrl);

            // check model view generated with hashed name (zip of SVF size/content varies slightly each time so we can only check if it was created)
            objects = await forgeOSS.GetBucketObjectsAsync(projectsBucketKey, ossNames.ModelView);
            Assert.Single(objects);
        }
    }
}

/////////////////////////////////////////////////////////////////////
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

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Autodesk.Forge.Client;
using Microsoft.AspNetCore.Http;
using Shared;
using WebApplication.Definitions;
using WebApplication.Services;
using WebApplication.State;
using WebApplication.Utilities;

namespace WebApplication.Processing
{
    /// <summary>
    /// Class to place generated data files to expected places.
    /// </summary>
    public class Arranger
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly UserResolver _userResolver;

        // generate unique names for files. The files will be moved to correct places after hash generation.
        public readonly string Parameters = $"{Guid.NewGuid():N}.json";
        public readonly string Thumbnail = $"{Guid.NewGuid():N}.png";
        public readonly string SVF = $"{Guid.NewGuid():N}.zip";
        public readonly string InputParams = $"{Guid.NewGuid():N}.json";
        public readonly string OutputModelIAM = $"{Guid.NewGuid():N}.zip";
        public readonly string OutputModelIPT = $"{Guid.NewGuid():N}.ipt";
        public readonly string OutputSAT = $"{Guid.NewGuid():N}.sat";
        public readonly string OutputRFA = $"{Guid.NewGuid():N}.rfa";
        public readonly string BomJson = $"{Guid.NewGuid():N}.bom.json";
        public readonly string OutputDrawingViewables = $"{Guid.NewGuid():N}.drawing.pdf";

        /// <summary>
        /// Constructor.
        /// </summary>
        public Arranger(IHttpClientFactory clientFactory, UserResolver userResolver)
        {
            _clientFactory = clientFactory;
            _userResolver = userResolver;
        }

        /// <summary>
        /// Create adoption data.
        /// </summary>
        /// <param name="docUrl">URL to the input Inventor document (IPT or zipped IAM)</param>
        /// <param name="tlaFilename">Top level assembly in the ZIP. (if any)</param>
        public async Task<AdoptionData> ForAdoptionAsync(string docUrl, string tlaFilename)
        {
            var bucket = await _userResolver.GetBucketAsync();

            var urls = await Task.WhenAll(bucket.CreateSignedUrlAsync(Thumbnail, ObjectAccess.Write), 
                                            bucket.CreateSignedUrlAsync(SVF, ObjectAccess.Write), 
                                            bucket.CreateSignedUrlAsync(Parameters, ObjectAccess.Write),
                                            bucket.CreateSignedUrlAsync(OutputModelIAM, ObjectAccess.Write),
                                            bucket.CreateSignedUrlAsync(OutputModelIPT, ObjectAccess.Write),
                                            bucket.CreateSignedUrlAsync(BomJson, ObjectAccess.Write));

            return new AdoptionData
                    {
                        InputDocUrl         = docUrl,
                        ThumbnailUrl        = urls[0],
                        SvfUrl              = urls[1],
                        ParametersJsonUrl   = urls[2],
                        OutputIAMModelUrl   = urls[3],
                        OutputIPTModelUrl   = urls[4],
                        BomUrl              = urls[5],
                        TLA                 = tlaFilename
                    };
        }

        /// <summary>
        /// Create adoption data.
        /// </summary>
        /// <param name="docUrl">URL to the input Inventor document (IPT or zipped IAM)</param>
        /// <param name="tlaFilename">Top level assembly in the ZIP. (if any)</param>
        /// <param name="parameters">Inventor parameters.</param>
        public async Task<UpdateData> ForUpdateAsync(string docUrl, string tlaFilename, InventorParameters parameters)
        {
            var bucket = await _userResolver.GetBucketAsync();

            var urls = await Task.WhenAll(
                                            bucket.CreateSignedUrlAsync(OutputModelIAM, ObjectAccess.Write),
                                            bucket.CreateSignedUrlAsync(OutputModelIPT, ObjectAccess.Write),
                                            bucket.CreateSignedUrlAsync(SVF, ObjectAccess.Write),
                                            bucket.CreateSignedUrlAsync(Parameters, ObjectAccess.Write),
                                            bucket.CreateSignedUrlAsync(InputParams, ObjectAccess.ReadWrite),
                                            bucket.CreateSignedUrlAsync(BomJson, ObjectAccess.Write)
                                            );

            await using var jsonStream = Json.ToStream(parameters);
            await bucket.UploadObjectAsync(InputParams, jsonStream);

            return new UpdateData
                    {
                        InputDocUrl         = docUrl,
                        OutputIAMModelUrl   = urls[0],
                        OutputIPTModelUrl   = urls[1],
                        SvfUrl              = urls[2],
                        ParametersJsonUrl   = urls[3],
                        InputParamsUrl      = urls[4],
                        BomUrl              = urls[5],
                        TLA                 = tlaFilename
                    };
        }

        /// <summary>
        /// Move project OSS objects to correct places.
        /// NOTE: it's expected that the data is generated already.
        /// </summary>
        /// <returns>Parameters hash.</returns>
        public async Task<string> MoveProjectAsync(Project project, string tlaFilename)
        {
            var hashString = await GenerateParametersHashAsync();
            var attributes = new ProjectMetadata { Hash = hashString, TLA = tlaFilename };

            var ossNames = project.OssNameProvider(hashString);

            var bucket = await _userResolver.GetBucketAsync();

            // move data to expected places
            await Task.WhenAll(bucket.RenameObjectAsync(Thumbnail, project.OssAttributes.Thumbnail),
                                bucket.RenameObjectAsync(SVF, ossNames.ModelView),
                                bucket.RenameObjectAsync(BomJson, ossNames.Bom),
                                bucket.RenameObjectAsync(Parameters, ossNames.Parameters),
                                bucket.RenameObjectAsync(attributes.IsAssembly ? OutputModelIAM : OutputModelIPT, ossNames.GetCurrentModel(attributes.IsAssembly)),
                                bucket.UploadObjectAsync(project.OssAttributes.Metadata, Json.ToStream(attributes, writeIndented: true)));

            return hashString;
        }

        /// <summary>
        /// Move temporary OSS files to the correct places.
        /// </summary>
        internal async Task MoveRfaAsync(Project project, string hash)
        {
            var bucket = await _userResolver.GetBucketAsync();

            var ossNames = project.OssNameProvider(hash);
            await Task.WhenAll(bucket.RenameObjectAsync(OutputRFA, ossNames.Rfa),
                                bucket.DeleteObjectAsync(OutputSAT));
        }

        /// <summary>
        /// Move temporary OSS files to the correct places.
        /// </summary>
        internal async Task MoveDrawingViewablesAsync(Project project, string hash)
        {
            var bucket = await _userResolver.GetBucketAsync();

            var ossNames = project.OssNameProvider(hash);
            await bucket.RenameObjectAsync(OutputDrawingViewables, ossNames.DrawingViewables, true);
        }

        internal async Task<ProcessingArgs> ForSatAsync(string inputDocUrl, string topLevelAssembly)
        {
            var bucket = await _userResolver.GetBucketAsync();

            // SAT file is intermediate and will be used later for further conversion (to RFA),
            // so request both read and write access to avoid extra calls to OSS
            var satUrl = await bucket.CreateSignedUrlAsync(OutputSAT, ObjectAccess.ReadWrite);

            return new ProcessingArgs
            {
                InputDocUrl = inputDocUrl,
                TLA = topLevelAssembly,
                SatUrl = satUrl
            };
        }

        internal async Task<ProcessingArgs> ForRfaAsync(string inputDocUrl)
        {
            var bucket = await _userResolver.GetBucketAsync();
            var rfaUrl = await bucket.CreateSignedUrlAsync(OutputRFA, ObjectAccess.Write);

            return new ProcessingArgs
            {
                InputDocUrl = inputDocUrl,
                RfaUrl = rfaUrl
            };
        }

        internal async Task<ProcessingArgs> ForDrawingViewablesAsync(string inputDocUrl, string topLevelAssembly)
        {
            var bucket = await _userResolver.GetBucketAsync();
            var drawingViewablesUrl = await bucket.CreateSignedUrlAsync(OutputDrawingViewables, ObjectAccess.Write);

            return new ProcessingArgs
            {
                InputDocUrl = inputDocUrl,
                DrawingViewablesUrl = drawingViewablesUrl,
                TLA = topLevelAssembly
            };
        }

        /// <summary>
        /// Move viewables OSS objects to correct places.
        /// NOTE: it's expected that the data is generated already.
        /// </summary>
        /// <returns>Parameters hash.</returns>
        public async Task<string> MoveViewablesAsync(Project project, bool isAssembly)
        {
            var hashString = await GenerateParametersHashAsync();

            var ossNames = project.OssNameProvider(hashString);

            var bucket = await _userResolver.GetBucketAsync();

            // move data to expected places
            await Task.WhenAll(bucket.RenameObjectAsync(SVF, ossNames.ModelView),
                                bucket.RenameObjectAsync(BomJson, ossNames.Bom),
                                bucket.RenameObjectAsync(Parameters, ossNames.Parameters),
                                bucket.RenameObjectAsync(isAssembly ? OutputModelIAM : OutputModelIPT, ossNames.GetCurrentModel(isAssembly)),
                                bucket.DeleteObjectAsync(InputParams));

            return hashString;
        }

        /// <summary>
        /// Generate hash string for the _temporary_ parameters json.
        /// </summary>
        private async Task<string> GenerateParametersHashAsync()
        {
            var client = _clientFactory.CreateClient();

            // rearrange generated data according to the parameters hash
            var bucket = await _userResolver.GetBucketAsync();
            var url = await bucket.CreateSignedUrlAsync(Parameters);
            using var response = await client.GetAsync(url); // TODO: find
            response.EnsureSuccessStatusCode();

            // generate hash for parameters
            var stream = await response.Content.ReadAsStreamAsync();
            var parameters = await JsonSerializer.DeserializeAsync<InventorParameters>(stream);
            return Crypto.GenerateObjectHashString(parameters);
        }
    }
}

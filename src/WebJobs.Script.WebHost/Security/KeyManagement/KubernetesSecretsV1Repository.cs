﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs.Script.IO;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest.Azure;

namespace Microsoft.Azure.WebJobs.Script.WebHost
{
    /// <summary>
    /// An <see cref="ISecretsRepository"/> implementation that uses the key vault as the backing store.
    /// </summary>
    public class KubernetesSecretsRepository : BaseSecretsRepository
    {
        private const string kubernetesSecretPath = "/run/secrets/kubernetes.io/serviceaccount";
        private const string HostPrefix = "host.";
        private const string MasterKey = "host.master";
        private const string FunctionKeyPrefix = "host.functions.";
        private const string SystemKeyPrefix = "host.systemKey.";
        private const string FunctionPrefix = "functions.";
        private readonly string _secretName;
        private readonly IKubernetesClient _kubernetesClient;

        public KubernetesSecretsRepository(string secretsSentinelFilePath, string secretName, IKubernetesClient kubernetesClient) : base(secretsSentinelFilePath)
        {
            if (secretsSentinelFilePath == null)
            {
                throw new ArgumentNullException(nameof(secretsSentinelFilePath));
            }

            _secretName = secretName ?? throw new ArgumentNullException(nameof(secretName));

            _kubernetesClient = kubernetesClient;
        }

        public override bool IsEncryptionSupported
        {
            get
            {
                return false;
            }
        }

        public override async Task<ScriptSecrets> ReadAsync(ScriptSecretsType type, string functionName)
        {
            return type == ScriptSecretsType.Host ? await ReadHostSecrets() : await ReadFunctionSecrets(functionName);
        }

        public override async Task WriteAsync(ScriptSecretsType type, string functionName, ScriptSecrets secrets)
        {
            // TODO
            string filePath = GetSecretsSentinelFilePath(type, functionName);
            await FileUtility.WriteAsync(filePath, DateTime.UtcNow.ToString());
        }

        public override async Task PurgeOldSecretsAsync(IList<string> currentFunctions, ILogger logger)
        {
            // no-op - allow stale secrets to remain
            await Task.Yield();
        }

        public override Task WriteSnapshotAsync(ScriptSecretsType type, string functionName, ScriptSecrets secrets)
        {
            //Runtime is not responsible for encryption so this code will never be executed.
            throw new NotSupportedException();
        }

        public override Task<string[]> GetSecretSnapshots(ScriptSecretsType type, string functionName)
        {
            //Runtime is not responsible for encryption so this code will never be executed.
            throw new NotSupportedException();
        }

        private async Task<ScriptSecrets> ReadHostSecrets()
        {
            IDictionary<string, string> secrets = await _kubernetesClient.GetSecret(_secretName);

            HostSecrets hostSecrets = new HostSecrets()
            {
                FunctionKeys = new List<Key>(),
                SystemKeys = new List<Key>()
            };

            foreach (var pair in secrets)
            {
                if (pair.Key.StartsWith(MasterKey))
                {
                    hostSecrets.MasterKey = new Key("master", pair.Value);
                }
                else if (pair.Key.StartsWith(FunctionKeyPrefix))
                {
                    hostSecrets.FunctionKeys.Add(ParseHostFunctionKey(pair.Key, pair.Value));
                }
                else if (pair.Key.StartsWith(SystemKeyPrefix))
                {
                    hostSecrets.SystemKeys.Add(ParseHostSystemKey(pair.Key, pair.Value));
                }
            }

            return hostSecrets;
        }
        private async Task<ScriptSecrets> ReadFunctionSecrets(string functionName)
        {
            IDictionary<string, string> secrets = await _kubernetesClient.GetSecret(_secretName);

            return new FunctionSecrets()
            {
                Keys = secrets
                    .Select(p => ParseFunctionKey(p.Key, p.Value))
                    .ToList()
            };
        }

        private Key ParseHostSystemKey(string key, string value)
        {
            throw new NotImplementedException();
        }

        private Key ParseHostFunctionKey(string key, string value)
        {
            throw new NotImplementedException();
        }

        private Key ParseFunctionKey(string key, string value)
        {
            throw new NotImplementedException();
        }
    }
}
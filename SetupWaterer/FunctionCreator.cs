using Ionic.Zip;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SetupWaterer
{
    public class FunctionCreator

    {
        #region fields
        private string clientId = "***REMOVED***";
        private string clientSecret = "***REMOVED***";
        private string tenant = "***REMOVED***";
        private string subscription = "***REMOVED***";
        #endregion

        public string ClientId { get { return clientId; }}
        public string ClientSecret { get { return clientSecret; } }
        public string Tenant { get { return tenant; } }
        public string Subscription { get { return subscription; } }

        public void DeleteSchedule(string oldId)
        {
            IAzure azure = GetAzure();
            var app = azure.WebApps.GetById(oldId);
            if (app != null)
            {
                var storageAccountId = azure.StorageAccounts
                    .List()
                    .Where(sa => sa.Name == app.Name)
                    .First().Id;

                azure.WebApps.DeleteById(oldId);
                azure.AppServices.AppServicePlans.DeleteById(app.AppServicePlanId);
                azure.StorageAccounts.DeleteById(storageAccountId);
            }
        }

        public string CreateSchedule(
            Guid id, string schedule, string functionUrlToInvoke,
            string fnNamePrefix,
            string resourceGroup,
            string storageAccountConn)
        {
            try
            {
                string indexJs = CreateIndex(functionUrlToInvoke);
                string functionJson = ApplySchedule();
                indexJs = indexJs.Replace("[id]", id.ToString());
                functionJson = functionJson.Replace("[schedule]", schedule);

                IAzure azure = GetAzure();

                var newName = (fnNamePrefix + id.ToString()).Substring(0, 19)
                    .ToLower().Replace("-", "");

                var currentRG = azure.ResourceGroups.GetByName(resourceGroup);

                MemoryStream stream = CreateZip(indexJs, functionJson);

                var functionUrlZip = UploadZip(storageAccountConn, newName, stream);
                stream.Position = 0;
                var skus = new Microsoft.Azure.Management.Storage.Fluent.Models.SkuName[] {
                    Microsoft.Azure.Management.Storage.Fluent.Models.SkuName.PremiumLRS,
                    Microsoft.Azure.Management.Storage.Fluent.Models.SkuName.StandardGRS,
                    Microsoft.Azure.Management.Storage.Fluent.Models.SkuName.StandardLRS,
                    Microsoft.Azure.Management.Storage.Fluent.Models.SkuName.StandardRAGRS,
                    Microsoft.Azure.Management.Storage.Fluent.Models.SkuName.StandardZRS };

                var sku = skus[2];

                var asm = azure.AppServices.GetType().Assembly.FullName;
                var asmP = azure.AppServices.GetType().Assembly.Location;
                var websiteApp =
                    azure.AppServices.FunctionApps.Define(newName)
                    .WithRegion(currentRG.Region)
                    .WithExistingResourceGroup(resourceGroup)
                    .WithNewStorageAccount(newName, sku)
                    .WithAppSetting("WEBSITE_USE_ZIP", functionUrlZip)
                    .WithAppSetting("TimerInterval", schedule)
                    .Create()
                    ;
                
                return websiteApp.Id;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
            return null;
        }

        private string ApplySchedule()
        {
            return @"
                {
                  ""disabled"": false,
                  ""bindings"": [
                    {
                      ""name"": ""myTimer"",
                      ""type"": ""timerTrigger"",
                      ""direction"": ""in"",
                      ""schedule"": ""[schedule]""
                    }
                  ]
                }
            ";
        }

        private string CreateIndex(string functionToInvoke)
        {
            return @"      
                module.exports = function (context, myTimer) {
                if(myTimer.isPastDue)
                {
                    context.log('JavaScript is running late!');
                }

                var timeStamp = new Date().toISOString();
                var functionUrl = '"+functionToInvoke+@"';
                getContent(functionUrl)
                    .then((html) => context.done())
                    .catch((err) => 
                    {
                        context.log(err);
                        context.done();
                    });
            };

            const getContent = function(url) {
              // return new pending promise
              return new Promise((resolve, reject) => {
                // select http or https module, depending on reqested url
                const lib = url.startsWith('https') ? require('https') : require('http');
                const request = lib.get(url, (response) => {
                  // handle http errors
                  if (response.statusCode < 200 || response.statusCode > 299) {
                     reject(new Error('Failed to load page, status code: ' + response.statusCode));
                   }
                  // temporary data holder
                  const body = [];
                  // on every content chunk, push it to the data array
                  response.on('data', (chunk) => body.push(chunk));
                  // we are done, resolve promise with those joined chunks
                  response.on('end', () => resolve(body.join('')));
                });
                // handle connection errors of the request
                request.on('error', (err) => reject(err))
                })
            };
            ";
        }

        private IAzure GetAzure()
        {
            var azureCredentials = new AzureCredentials(new
                ServicePrincipalLoginInformation
            {
                ClientId = ClientId,
                ClientSecret = ClientSecret
            }, Tenant, AzureEnvironment.AzureGlobalCloud);

            var azure = Azure
               .Configure()
               .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
               .Authenticate(azureCredentials)
               .WithSubscription(Subscription);
            return azure;
        }

        private string UploadZip(string connStr,
            string zipName, MemoryStream stream)
        {
            zipName = zipName + ".zip";
            CloudStorageAccount storageAccount = CloudStorageAccount
                .Parse(connStr);

            // Create the blob client.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve reference to a previously created container.
            CloudBlobContainer container = blobClient.GetContainerReference("functionarchives");

            container.CreateIfNotExists(BlobContainerPublicAccessType.Container);

            // Retrieve reference to a blob named "myblob".
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(zipName);
            blockBlob.UploadFromStream(stream);
            return blockBlob.Uri.AbsoluteUri;
        }

        private MemoryStream CreateZip(string indexJs, string functionJson)
        {
            var memoryStream = new MemoryStream();
            using (ZipFile zip = new ZipFile())
            {
                zip.AddEntry(@"host.json", "{}");
                zip.AddEntry(@"TimerTriggerJS1\index.js", indexJs);
                zip.AddEntry(@"TimerTriggerJS1\function.json", functionJson);
                zip.Save(memoryStream);
            }
            memoryStream.Position = 0;
            return memoryStream;
        }
    }
}

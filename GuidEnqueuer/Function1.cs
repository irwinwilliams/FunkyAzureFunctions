using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;

namespace GuidEnqueuer
{
    public static class Function1
    {
        #region fields
        private static string connectionString = "[get from config]";
        #endregion

        public static string ConnectionString { get { return connectionString; } }

        public static string QueueName { get { return "guidqueue"; } }
        [FunctionName("Function1")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");
            var paramName = "guid";
            // parse query parameter
            string guid = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, paramName, true) == 0)
                .Value;

            if (guid == null)
            {
                // Get request body
                dynamic data = await req.Content.ReadAsAsync<object>();
                guid = data?.name;
            }
            
            if (guid != null)
            {
                var parsedGuid = Guid.Parse(guid);//deal with shenanigans :) 
                CloudStorageAccount storageAccount = CloudStorageAccount
                    .Parse(ConnectionString);
                CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
                CloudQueue queue = queueClient.GetQueueReference(QueueName);
                queue.CreateIfNotExists();
                CloudQueueMessage message = new CloudQueueMessage(parsedGuid.ToString());
                await queue.AddMessageAsync(message);
            }

            return guid == null
                ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a guid"+
                " on the query string or in the request body")
                : req.CreateResponse(HttpStatusCode.OK, "Hello " + guid);
        }
    }
}

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace SetupWaterer
{
    public static class Function1
    {
        #region field
        private static string connectionString = "[supply valid connection string]";
        #endregion

        public static string ConnectionString { get { return connectionString; } }

        [FunctionName("Function1")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            // parse query parameter
            string oldId = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "name", true) == 0)
                .Value;

            string newId = string.Empty;

            if (oldId == null)
            {
                // Get request body
                dynamic data = await req.Content.ReadAsAsync<object>();
                oldId = data?.name;
            }
            var functionCreator = new FunctionCreator();
            if (!string.IsNullOrEmpty(oldId))
            {
                functionCreator.DeleteSchedule(oldId);
            }
            else
            {
                string schedule = "0 */3 * * * *";//every 3 minutes
                Guid id = Guid.NewGuid();
                string functionUrlToInvoke = $"https://guidenqueuer.azurewebsites.net/api/Function1?code=uSeUaF8eVgbEp8awuayRtcykBvRP18ZzZCaeHy/awvX0GyRUeOCLBw==&guid={id.ToString()}";
                string fnNamePrefix = "Funky";
                string resourceGroup = "FunkyAzureFunctions";

                newId = functionCreator.CreateSchedule(id, schedule, 
                    functionUrlToInvoke, fnNamePrefix,
                    resourceGroup, ConnectionString);
            }

            return oldId == null
                ? req.CreateResponse(HttpStatusCode.OK, "Here's what you're working with: " + newId)
                : req.CreateResponse(HttpStatusCode.OK, "Goodbye, my friend. " + oldId);
        }
    }
}

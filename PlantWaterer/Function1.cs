using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace PlantWaterer
{
    public static class Function1
    {
        [FunctionName("Function1")]
        public static void Run([QueueTrigger("guidqueue",
            Connection = "GuidConnection")]string myQueueItem, TraceWriter log)
        {
            log.Info($"Watered this plant,{myQueueItem}, man! ");
        }
    }
}

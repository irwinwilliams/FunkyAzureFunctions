# FunkyAzureFunctions
Demonstration code for my Caribbean Developer Week session, "Funky Azure Functions"

# Architecture
This is a simple solution that features three Azure functions, one for enqueuing via an HTTP request to an Azure Storage Queue, another for reading from the queue and a third to make magic.
That third one, the one with all the magic, can spawn new Azure functions by using the Azure Management API Fluent SDK. It will create a nodeJs function that invokes a web site on a specified schedule.
There's a visio document that tries to show this. It might fail.

# Configuration
A service principal needs to be created for the "SetupWaterer" function.
Storage account/s need to be made for the other two, and specified in their respective function.cs files.
The url for the http function needs to be specified to the SetupWaterer function.


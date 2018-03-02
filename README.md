Description
----
This repo contains a simple simultation of air traffic control. It consists of two services implemented using .NET Core 2.0: a "controller" service, and an "airplane" service. The controller service (a singleton) exposes a web API that lets the client submit new flights and observe flight progress using HTTP server-sent events. The airplane service manages all simulated airplanes. It receives "instructions" from the controller service and simulates airplanes flying.

I use this repo to test ideas for work--do not try this on real airplanes :-)

[Setup](#setup)
----

1. Create an Azure storage account (to store world state, i.e. information about all flying airplanes)
1. Ensure that AZURE_STORAGE_CONNECTION_STRING configuration parameter is set to the storage account connection string (including the storage account key) for the atcsvc launch environment
    * The storage connection string format is 
    
        `DefaultEndpointsProtocol=https;AccountName=yourStorageAccountName;EndpointSuffix=core.windows.net;AccountKey=yourStorageAccountKey`

    * Use Secret Manager, which works for Windows/Linux/Mac, see https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets?tabs=visual-studio
        * Go to (repo root)/atcsvc and do 
            
            `dotnet user-secrets set AZURE_STORAGE_CONNECTION_STRING 'storage-connection-string'`

        * The secret ID (part of path to secrets.json file) is atc_k8s
    * You have to "refresh" the project in Visual Studio for Mac for the changes to take effect
1. The ATC service uses port 5023 (Properties/launchsettings.json)
1. To run the system, start both atcsvc project and airplanesvc project (e.g do `dotnet run` from both folders, or configure Visual Studio to start both projects upon F5)
1. To get notifications about flights in progress do

    `curl --no-buffer http://localhost:5023/api/flights`

1. To start new flight do

    `curl -H 'Content-Type: application/json' -X PUT -d '{ "DeparturePoint": {"Name": "KSEA"}, "Destination": {"Name": "KPDX"}, "CallSign": "N2130U"}' http://localhost:5023/api/flights`

    Basically, a flight goes from "DeparturePoint" to "Destination". Both are airports. You can look up the names of airports in Universe.cs source file. The "CallSign" is arbitrary identifier for the airplane.

Local container testing (using Docker Compose)
----

1. Set AZURE_STORAGE_CONNECTION_STRING as described above in the [Setup paragraph](#setup) 

    `export AZURE_STORAGE_ACCOUNT_STRING='connection-string'`

1. Do

    `docker-compose up`
    
    * On a Mac do 
    
        `docker-compose -f docker-compose.yml -f docker-compose.override.mac.yml up`

1. When done testing, do 

    `docker-compose down`

Setup for Kubernetes (AKS) deployment
----
1. Create AKS cluster
1. If you have not created it yet--create an Azure container registry (ACR)
1. Ensure that AKS service principal has access to the ACR registry 
    * (it is easy to set it through the Azure portal, go to 'Access Control' tab in the Azure container registry blade)
    * Reader permission should suffice, but it might be necessary to grant your AKS cluster a Contributor role
1. Create a namespace for the app: 
    
    `kubectl create namespace atc`

1. Create a Kubernetes configuration for the namespace:
    * View the cluster details:

        `kubectl config view`

    * Create a configuration that uses the newly created namespace. You get the cluster name and user name from the result of `config view` command
    
         `kubectl config set-context atc --namespace=atc --cluster=yourClusterName --user=clusterUserName

    * Switch to the new context:

        `kubectl config use-context atc`

1. Switch to `k8s` directory and run `deploy.sh` script from there (do `deploy.sh --help` to see the options).

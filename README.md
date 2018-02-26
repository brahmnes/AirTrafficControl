Setup
----

1. Create Azure storage account (to store world state, i.e. information about all flying airplanes)
1. Ensure that storage connection string in atcsvc/appsettings.json points to the correct storage account
1. Ensure that AZURE_STORAGE_ACCOUNT_KEY environment variable with the storage account key is set for the atcsvc launch environment
    * E.g. you can use application properties in Visual Studio for Mac, which are stored in the .user file,  excluded from Git, but not encrypted.
    * Or use Secret Manager, which works for Windows/Linux/Mac, see https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets?tabs=visual-studio
        * The secret ID (part of path to secrets.json file) is atc_k8s
    * You have to "refresh" the project in Visual Studio for Mac for the changes to take effect
1. The ATC service uses http://localhost:5023 (Properties/launchsettings.json)


Setup for Kubernetes (AKS) deployment
----
1. Create AKS cluster
1. If you have not created it yet--create an Azure container registry (ACR)
1. Ensure that AKS service principal has access to the ACR registry 
    a. (it is easy to set it through the Azure portal)
    b. Reader permission should suffice, but it might be necessary to grant AKS a Contributor role
(MORE TO COME)
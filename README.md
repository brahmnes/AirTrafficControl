Setup
----

1. Create Azure storage account (to store world state, i.e. information about all flying airplanes)
1. Ensure that storage connection string in atcsvc/appsettings.json points to the correct storage account
1. Set AZURE_STORAGE_ACCOUNT_KEY environment variable with the storage account key in the application properties in VS 
    * It goes into .user file, which is excluded from Git, but not encrypted
    * You have to "refresh" the project in Visual Studio for Mac for the changes to take effect


Setup for Kubernetes (AKS) deployment
----
1. Create AKS cluster
1. If you have not created it yet--create an Azure container registry (ACR)
1. Ensure that AKS service principal has access to the ACR registry 
    a. (it is easy to set it through the Azure portal)
    b. Reader permission should suffice, but it might be necessary to grant AKS a Contributor role
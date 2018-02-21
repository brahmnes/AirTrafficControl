Setup
----

1. Create Azure storage account (to store world state, i.e. information about all flying airplanes)
1. blah


Setup for Kubernetes (AKS) deployment
----
1. Create AKS cluster
1. If you have not created it yet--create an Azure container registry (ACR)
1. Ensure that AKS service principal has access to the ACR registry 
  * (it is easy to set it through the Azure portal)
  * Reader permission should suffice, but it might be necessary to grant AKS a Contributor role
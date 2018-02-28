#!/usr/bin/env bash

# http://redsymbol.net/articles/unofficial-bash-strict-mode/
set -euo pipefail

usage()
{
    cat <<END
deploy.sh: deploys ATC application to Kubernetes cluster using Helm service
Parameters:
  -r | --registry <container registry> 
    Specifies container registry (ACR) to use (required), e.g. myregistry.azurecr.io
  -t | --tag <docker image tag> 
    Default: current timestamp, with 1-minute resolution
  --rel <release name>
    Specify Helm release name (default: atcAppRelease)
  -b | --build-solution
    Force solution build before deployment (default: false)
  --ikey <AppInsights instrumentation key>
    Sets the Application Insights instrumentation key used for sending diagnostic data
  --skip-image-build
    Do not build images (default is to build all images)
  --skip-image-push
    Do not upload images to the container registry (just run the Kubernetes deployment portion)
    Default is to push images to container registry
  --only-clean
    Do not deploy application to Kubernetes, just clean the old deployment (default: false)
  -h | --help
    Displays this help text and exits the script

It is assumed that the Kubernetes AKS cluster has been granted access to ACR registry.
For more info see 
https://docs.microsoft.com/en-us/azure/container-registry/container-registry-auth-aks

WARNING! THE SCRIPT WILL COMPLETELY DESTROY ALL DEPLOYMENTS AND SERVICES VISIBLE
FROM THE CURRENT CONFIGURATION CONTEXT.
It is recommended that you create a separate namespace and confguration context
for application, to isolate it from other applications on the cluster.
For more information see https://kubernetes.io/docs/tasks/administer-cluster/namespaces/

END
}

image_tag=$(date '+%Y%m%d%H%M')
build_solution=''
container_registry=''
build_images='yes'
push_images='yes'
only_clean=''
helm_release_name='atcAppRelease'  # Note: cannot be the same as chart name
appinsights_ikey=''

while [[ $# -gt 0 ]]; do
  case "$1" in
    -r | --registry )
        container_registry="$2"; shift 2 ;;
    -t | --tag )
        image_tag="$2"; shift 2 ;;
    --rel )
        helm_release_name="$2"; shift 2 ;;
    -b | --build-solution )
        build_solution='yes'; shift ;;
    --ikey )
        appinsights_ikey="$2"; shift 2 ;;
    --skip-image-build )
        build_images=''; shift ;;
    --skip-image-push )
        push_images=''; shift ;;
    --only-clean )
        only_clean='yes'; shift ;;
    -h | --help )
        usage; exit 1 ;;
    *)
        echo "Unknown option $1"
        usage; exit 2 ;;
  esac
done

if [[ ! $container_registry && ! $only_clean ]]; then
    echo 'Container registry must be specified (e.g. myregistry.azurecr.io)'
    echo ''
    usage
    exit 3
fi

if [[ $build_solution ]]; then
    echo "#################### Building eShopOnContainers solution ####################"
    dotnet publish -o obj/Docker/publish ../atc-k8s.sln
fi

export TAG=$image_tag

if [[ $build_images ]]; then
    echo "#################### Building eShopOnContainers Docker images ####################"
    docker-compose -p .. -f ../docker-compose.yml build

    # Remove temporary images
    docker rmi $(docker images -qf "dangling=true")
fi

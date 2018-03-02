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
  --ikey <AppInsights instrumentation key>
    Sets the Application Insights instrumentation key used for sending diagnostic data
  -s | --storage <storage connection string>
    Specifies Azure storage connection string for the ATC service (required if creating new deployment)
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
container_registry=''
build_images='yes'
push_images='yes'
only_clean=''
helm_release_name='atcapprel'  # Note: cannot be the same as chart name
appinsights_ikey=''
storage_cstring=''

while [[ $# -gt 0 ]]; do
  case "$1" in
    -r | --registry )
        container_registry="$2"; shift 2 ;;
    -t | --tag )
        image_tag="$2"; shift 2 ;;
    --rel )
        helm_release_name="$2"; shift 2 ;;
    --ikey )
        appinsights_ikey="$2"; shift 2 ;;
    -s | --storage )
        storage_cstring="$2"; shift 2 ;;
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

if [[ ! $storage_cstring && ! $only_clean ]]; then
    echo 'Azure storage connection string must be specified'
    echo ''
    usage
    exit 4
fi

export TAG=$image_tag

if [[ $build_images ]]; then
    echo "#################### Building ATC app Docker images ####################"
    docker-compose -p .. -f ../docker-compose.yml build

    # Remove temporary images
    docker rmi $(docker images -qf "dangling=true")
fi

if [[ $push_images ]]; then
    echo "#################### Pushing images to registry ####################"
    services=(atcsvc airplanesvc)

    for service in "${services[@]}"
    do
        echo "Pushing image for service $service..."
        docker tag "atc/$service:$image_tag" "$container_registry/$service:$image_tag"
        docker push "$container_registry/$service:$image_tag"
    done
fi

echo "#################### Cleaning up old deployment ####################"
helm delete "$helm_release_name" --purge || true

if [[ $only_clean ]]; then
    exit 0
fi

echo "############ Deploying ATC application ############"
if [[ $appinsights_ikey ]]; then
    helm install atcApp --name "$helm_release_name" --wait --dep-up \
        --set "appinsights_instrumentationkey=$appinsights_ikey" \
        --set "azure_storage_connection_string=$storage_cstring" \
        --set "container_registry=$container_registry" \
        --set "image_tag= $image_tag" 
else
    helm install atcApp --name "$helm_release_name" --wait --dep-up \
        --set "azure_storage_connection_string=$storage_cstring" \
        --set "container_registry=$container_registry" \
        --set "image_tag= $image_tag" 
fi

echo "#################### Waiting for Azure to provision external IP ####################"

ip_regex='([0-9]{1,3}\.){3}[0-9]{1,3}'
while true; do
    printf "."
    frontendIp=$(kubectl get svc atcsvc -o=jsonpath="{.status.loadBalancer.ingress[0].ip}")
    if [[ $frontendIp =~ $ip_regex ]]; then
        break
    fi
    sleep 5s
done

printf "\n"
echo "ATC service is available under http://$frontendIp:5023/api/flights"

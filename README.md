 docker build -t gloogatewaytst .

 $azureContainerRegistryAddress=$(az acr show --name $azureContainerRegistryName --query "loginServer" --output tsv)
docker tag gloogatewaytst:latest "$azureContainerRegistryAddress/gloogatewaytst:1.0.1"

docker push "$azureContainerRegistryAddress/gloogatewaytst:1.0.1"



$namespace="keyvaultapp"
kubectl create namespace $namespace

kubectl apply -f .\Kubernetes\application.yaml -n $namespace
kubectl apply -f .\Kubernetes\service.yaml -n $namespace


kubectl apply -f .\Kubernetes\virtualservice.yaml -n gloo-system
kubectl get upstream -n gloo-system


========================
fro debugging that ur virtual srvice is working

1) kubectl describe virtualservice keyvaultapp-virtualservice -n gloo-system
Explains the configuration and status of your Gloo Virtual Service.

Why Use This Command?
It helps debug why Gloo Gateway is not routing traffic to your backend service.
If you're getting a 503 Service Unavailable, this command can show:

Route Issues (incorrect upstream name, wrong prefix match)
Misconfigurations (missing upstream service)
Errors in Gateway Routing



2)kubectl get upstream -n gloo-system
NAME                                               AGE
default-kubernetes-443                             93m
gloo-system-gateway-proxy-443                      93m
gloo-system-gateway-proxy-80                       93m
gloo-system-gloo-443                               92m
gloo-system-gloo-9966                              92m
gloo-system-gloo-9976                              92m
gloo-system-gloo-9977                              92m
gloo-system-gloo-9979                              92m
gloo-system-gloo-9988                              92m
keyvaultapp-keyvaultapp-service-80                 73m
kube-system-azure-wi-webhook-webhook-service-443   92m
kube-system-kube-dns-53                            92m
kube-system-metrics-server-443                     93m

then run:
kubectl describe upstream keyvaultapp-keyvaultapp-service-80 
if it has =>       State:Accepted  
it means Your upstream keyvaultapp-keyvaultapp-service-80 is now correctly discovered by Gloo and has the status "Accepted", 


3)Verify If Gloo Gateway Can Reach Your Service:
 >kubectl exec -it deploy/gateway-proxy -n gloo-system -- curl -v http://keyvaultapp-service.keyvaultapp.svc.cluster.local:80/WeatherForecast
 > kubectl exec -it deploy/gateway-proxy -n gloo-system -- wget -qO- http://keyvaultapp-service.keyvaultapp.svc.cluster.local:80/WeatherForecast
 ✅ If It Works
If the response contains JSON data from your API, your backend service is working, and the issue is with Gloo Gateway configuration.


==============================




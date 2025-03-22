﻿ 
 #### Create and tag and push image to ACR
 docker build -t gloogatewaytst:1.0.2 .

 $azureContainerRegistryAddress=$(az acr show --name $azureContainerRegistryName --query "loginServer" --output tsv)
docker tag gloogatewaytst:1.0.2 "$azureContainerRegistryAddress/gloogatewaytst:1.0.2"

az acr login --name $azureContainerRegistryName

docker push "$azureContainerRegistryAddress/gloogatewaytst:1.0.2"


#### Deploying Services and Gloo ApiGateway
```
$namespace="keyvaultapp"
kubectl create namespace $namespace

kubectl apply -f .\Kubernetes\application.yaml -n $namespace
kubectl apply -f .\Kubernetes\service.yaml -n $namespace




helm repo add gloo https://storage.googleapis.com/solo-public-helm
helm repo update
helm install gloo gloo/gloo --namespace gloo-system --create-namespace
kubectl apply -f .\Kubernetes\virtualservice.yaml -n gloo-system
kubectl get upstream -n gloo-system

get list of helm 
helm list --namespace gloo-system
```


### Debugging Part

for debugging that ur virtual srvice is working

1) Explains the configuration and status of your Gloo Virtual Service.
> kubectl describe virtualservice keyvaultapp-virtualservice -n gloo-system


Why Use This Command?
It helps debug why Gloo Gateway is not routing traffic to your backend service.
If you're getting a 503 Service Unavailable, this command can show:

Route Issues (incorrect upstream name, wrong prefix match)
Misconfigurations (missing upstream service)
Errors in Gateway Routing



2)
>kubectl get upstream -n gloo-system
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


================================================
### Deployin KeyClock
```
$keyCloakNamespace="keycloakapp"
kubectl create namespace $keyCloakNamespace
kubectl apply -f .\Kubernetes\keycloak\keycloak-vol.yaml -n keycloak
kubectl apply -f .\Kubernetes\keycloak\keycloak-deployment.yaml -n keycloak

kubectl delete -f .\Kubernetes\keycloak\keycloak-deployment.yaml -n keycloak
kubectl delete -f .\Kubernetes\keycloak\keycloak-vol.yaml -n keycloak
```

get pods:
```
kubectl get pods -n keycloak
kubectl get services -n keycloak
```
its accessable from=> http://168.62.16.116/admin 


for getting token, Configure ur postman by following:
```
Grant type=> PKCE
Callback URL=>https://oauth.pstmn.io/v1/callback
Client ID=> Postman
auth url=>http://168.62.16.116/realms/AucRealm/protocol/openid-connect/auth
token => http://168.62.16.116/realms/AucRealm/protocol/openid-connect/token
Scope => openid profile Auc.FullAccess
```

****
### FOOTNOTE INFO

if ur VirtulService can not find ur upstream run following:
```
kubectl rollout restart deployment -n keyvaultapp

```
🔹 What Happens When You Run This Command?
It does not delete the Deployment.
All pods under the Deployment are restarted one by one (rolling restart).
The restart is graceful (old pods are removed only after new ones are running).
Does not change the configuration—only forces a refresh.


CHART AND DFAULT VALUE
u can get `chart template and default value` for helm by:
```
helm template gloo gloo/gloo > gloo-templates.yaml
helm show values gloo/gloo  >> gloogvalue.yaml
```

For accesssing to api gateway using DNS name instead of IP Address
there is 2 options:
>1- Azure DNS Label Name Annotation, like emissary, set annotation for loadbalancer
> 2- Azure DNS Zone

we are going to stick tonumber 1,
i treied to install the helm and set annotation at the same time by using following, but it does not work:
>helm install gloo gloo/gloo --namespace gloo-system --create-namespace  --set gatewayProxies.gatewayProxy.service.annotations."service\.beta\.kubernetes\.io/azure-dns-label-name"="orderapigateway"


so then i used 
```
PS C:\Users\Acer> kubectl annotate service gateway-proxy -n gloo-system service.beta.kubernetes.io/azure-dns-label-name=orderapigateway --overwrite
service/gateway-proxy annotated
PS C:\Users\Acer> kubectl describe service  gateway-proxy -n  gloo-system
Name:                     gateway-proxy
Namespace:                gloo-system
Labels:                   app=gloo
                          app.kubernetes.io/managed-by=Helm
                          gateway-proxy-id=gateway-proxy
                          gloo=gateway-proxy
Annotations:              meta.helm.sh/release-name: gloo
                          meta.helm.sh/release-namespace: gloo-system
                          service.beta.kubernetes.io/azure-dns-label-name: orderapigateway
Selector:                 gateway-proxy-id=gateway-proxy,gateway-proxy=live
Type:                     LoadBalancer
IP Family Policy:         SingleStack
IP Families:              IPv4
IP:                       10.0.166.123
IPs:                      10.0.166.123
LoadBalancer Ingress:     13.64.240.109 (VIP)
Port:                     http  80/TCP
TargetPort:               8080/TCP
NodePort:                 http  30686/TCP
Endpoints:                10.244.1.81:8080
Port:                     https  443/TCP
TargetPort:               8443/TCP
NodePort:                 https  31576/TCP
Endpoints:                10.244.1.81:8443
Session Affinity:         None
External Traffic Policy:  Cluster
Internal Traffic Policy:  Cluster
Events:
  Type    Reason                Age                 From                Message
  ----    ------                ----                ----                -------
  Normal  EnsuringLoadBalancer  9s (x2 over 9m30s)  service-controller  Ensuring load balancer
```
Why Does kubectl annotate Work, But helm install --set annotations Does Not?
The reason is how Gloo's Helm chart is structured:

Gloo’s Helm chart does not natively support adding annotations to the gateway-proxy service via values.yaml.
The annotations field is missing in the Helm template that generates the Service resource for gateway-proxy.
Helm cannot inject an annotation unless the chart templates reference it.
1. Helm Only Passes Values That Are Defined in Templates
When you run:


helm install gloo gloo/gloo \
  --namespace gloo-system \
  --create-namespace \
  --set gatewayProxies.gatewayProxy.service.annotations."service\.beta\.kubernetes\.io/azure-dns-label-name"="orderapigateway"

Helm will pass the value, but if the template does not use .Values.gatewayProxies.gatewayProxy.service.annotations, it does nothing.
Gloo's Helm chart does NOT have a reference to this field in the Service template.
Helm does not add custom values unless they are referenced in the template.
2. kubectl annotate Works Because It Directly Modifies the Kubernetes Resource
When you run:


kubectl annotate service gateway-proxy -n gloo-system \
  service.beta.kubernetes.io/azure-dns-label-name=orderapigateway --overwrite

This command bypasses Helm entirely.
It directly modifies the live Kubernetes object.
Since annotations are dynamic fields, Kubernetes allows modifying them without recreating the resource.
so the ur service is accessable by:
http://orderapigateway.westUs.cloudapp.azure.com/keyvault/WeatherForecast

****



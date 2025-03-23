 
 #### Create and tag and push image to ACR
 docker build -t gloogatewaytst:1.0.3 .

 $azureContainerRegistryAddress= $(az acr show --name $azureContainerRegistryName --query "loginServer" --output tsv)
docker tag gloogatewaytst:1.0.3 "$azureContainerRegistryAddress/gloogatewaytst:1.0.3"

az acr login --name $azureContainerRegistryName

docker push "$azureContainerRegistryAddress/gloogatewaytst:1.0.3"


#### Deploying Services and Gloo ApiGateway
```
$namespace="keyvaultapp"
kubectl create namespace $namespace

kubectl apply -f .\Kubernetes\application.yaml -n $namespace
kubectl apply -f .\Kubernetes\service.yaml -n $namespace




helm repo add gloo https://storage.googleapis.com/solo-public-helm
helm repo update
helm install gloo gloo/gloo --namespace $namespace --create-namespace
kubectl apply -f .\Kubernetes\virtualservice.yaml -n $namespace
kubectl get upstream -n $namespace

get list of helm 
helm list --namespace $namespace
```
when u run following, the service that have to send requst to, is `gateway-proxy`
```
PS E:\TestProject\_Azure\GlooApiGateway\GlooApiGateway> kubectl get services -n $namespace
NAME                  TYPE           CLUSTER-IP    EXTERNAL-IP      PORT(S)                                                AGE
gateway-proxy         LoadBalancer   10.0.24.240   104.42.25.4      80:32497/TCP,443:31499/TCP                             55m
gloo                  ClusterIP      10.0.36.93    <none>           9977/TCP,9976/TCP,9988/TCP,9966/TCP,9979/TCP,443/TCP   55m
keyvaultapp-service   LoadBalancer   10.0.1.192    40.118.169.226   80:32162/TCP                                           60m
```
it means : http://104.42.25.4/keyvault/WeatherForecast/
### Debugging Part

for debugging that ur virtual srvice is working

1) Explains the configuration and status of your Gloo Virtual Service.
> kubectl describe virtualservice keyvaultapp-virtualservice -n $namespace


Why Use This Command?
It helps debug why Gloo Gateway is not routing traffic to your backend service.
If you're getting a 503 Service Unavailable, this command can show:

Route Issues (incorrect upstream name, wrong prefix match)
Misconfigurations (missing upstream service)
Errors in Gateway Routing



2)
>kubectl get upstream -n $namespace
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
 >kubectl exec -it deploy/gateway-proxy -n $namespace -- curl -v http://keyvaultapp-service.keyvaultapp.svc.cluster.local:80/WeatherForecast
 > kubectl exec -it deploy/gateway-proxy -n $namespace -- wget -qO- http://keyvaultapp-service.keyvaultapp.svc.cluster.local:80/WeatherForecast
 ✅ If It Works
If the response contains JSON data from your API, your backend service is working, and the issue is with Gloo Gateway configuration.


================================================
### Deployin KeyClock
```

kubectl apply -f .\Kubernetes\keycloak\keycloak-vol.yaml -n $namespace
kubectl apply -f .\Kubernetes\keycloak\keycloak-deployment.yaml -n $namespace

kubectl delete -f .\Kubernetes\keycloak\keycloak-deployment.yaml -n $namespace
kubectl delete -f .\Kubernetes\keycloak\keycloak-vol.yaml -n $namespace
```

get pods:
```
kubectl get pods -n $namespace
kubectl get services -n $namespace
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

#### After putting KeyCloak behind the Proxy:
http://104.42.25.4/identitymgmt/realms/master/.well-known/openid-configuration

and for fulfilling to it we did:
```
             - name: KC_HOSTNAME
              value: "104.42.25.4"
            - name: KC_HOSTNAME_PATH
              value: "/identitymgmt"
            - name: KC_HOSTNAME_STRICT
              value: "false"
            - name: KC_HOSTNAME_STRICT_HTTPS
              value: "false"
            - name: KC_HTTP_RELATIVE_PATH
              value: "/identitymgmt"
            - name: KC_PROXY_HEADERS
              value: "xforwarded"

```
the thing is, at thiss point i put following in virtualService
```
      - matchers:
          - prefix: /identitymgmt/
        routeAction:
          single:
            upstream:
              name: keycloak-upstream
              namespace: keyvaultapp
        options:
          prefixRewrite: "/identitymgmt/"
```
but im going to change `  prefixRewrite: "/identitymgmt/"` to `  prefixRewrite: "/"` for this, first we have to understand
enviroment variable which we add on keyclock deployment file:
🔹 KC_HOSTNAME_PATH

- name: KC_HOSTNAME_PATH
  value: "/identitymgmt"
📌 What it does:

Adds /identitymgmt as a base path for ALL Keycloak-generated URLs (e.g., login pages, admin console, authentication endpoints).

Ensures internal Keycloak URLs include this path when generating redirects.

📍 Example:
🔴 Without this setting, Keycloak might generate:

http://104.42.25.4/admin/ (❌ missing /identitymgmt)

✅ With this setting, Keycloak generates:

http://104.42.25.4/identitymgmt/admin/ (✔️ correct)


🔹 KC_HOSTNAME_STRICT
- name: KC_HOSTNAME_STRICT
  value: "false"
📌 What it does:

Allows requests from any hostname, even if they don’t exactly match KC_HOSTNAME or KC_HOSTNAME_PATH.

Prevents issues where Keycloak rejects requests due to mismatched URLs.

📍 Example:
🔴 If this was "true", Keycloak would reject requests if the hostname didn’t exactly match the one in its configuration.

🔹 KC_HOSTNAME_STRICT_HTTPS

- name: KC_HOSTNAME_STRICT_HTTPS
  value: "false"
📌 What it does:

Allows HTTP connections, even when Keycloak expects HTTPS.

Useful when running Keycloak behind a TLS-terminating proxy (like Gloo, which handles HTTPS externally).

📍 Example:
🔴 If "true", Keycloak would force all requests to use HTTPS and reject HTTP requests.
✅ Since your proxy (Gloo) handles HTTPS, you don't need Keycloak to enforce HTTPS, so we set it to "false".

🔹 KC_HTTP_RELATIVE_PATH

- name: KC_HTTP_RELATIVE_PATH
  value: "/identitymgmt"
📌 What it does:

Changes Keycloak’s internal root path to /identitymgmt.

Ensures that all internal Keycloak URLs use /identitymgmt/ as the base path. so in this Senario keycloak itself
expect `/identitymgmt` in the requested path.

📍 Example:
🔴 Without this setting, Keycloak would expect:

http://104.42.25.4/admin/ (❌ no /identitymgmt)

✅ With this setting, Keycloak expects:

http://104.42.25.4/identitymgmt/admin/ (✔️ correct)

🔹 KC_PROXY_HEADERS

- name: KC_PROXY_HEADERS
  value: "xforwarded"
📌 What it does:

Tells Keycloak to trust and use the X-Forwarded-* headers from Gloo Gateway.

Ensures that redirects respect the client’s original request URL.

📍 Example:
If a client accesses:


http://104.42.25.4/identitymgmt
Without KC_PROXY_HEADERS=xforwarded, Keycloak sees:(10.244.1.81 is internal IP for keycloak)

http://10.244.1.81:8080
🔴 It doesn’t know it’s behind Gloo and generates the wrong redirect.

✅ With KC_PROXY_HEADERS=xforwarded, Keycloak knows it’s behind Gloo and correctly generates:

http://104.42.25.4/identitymgmt/admin

What is the X-Forwarded-* Header?
📌 General Concept
X-Forwarded-* headers are HTTP headers used by reverse proxies (like Gloo Gateway, Nginx, or Apache) to pass information about the original client request to the backend service.

When a client (browser or API consumer) makes a request through a reverse proxy, the proxy modifies the request before forwarding it to the backend server. It adds headers to preserve details like:

The original client IP address

The original protocol (HTTP/HTTPS)

The original host/domain

The original port

These headers help backend applications (like Keycloak) generate correct responses.

🔹 Types of X-Forwarded-* Headers
Header	Description
X-Forwarded-For	The client's IP address before reaching the proxy. Useful for logging and security.
X-Forwarded-Proto	The original protocol (http or https). Helps the backend decide if the request was secure.
X-Forwarded-Host	The original host/domain the client requested. Helps Keycloak generate correct redirect URLs.
X-Forwarded-Port	The original port used by the client (80, 443, etc.). Helps ensure the correct redirect port.
📍 Example: If a client visits:


```
https://example.com/identitymgmt

```

Gloo Gateway might forward the request to Keycloak internally at:

```
http://10.244.1.81:8080/identitymgmt

```

Headers sent by Gloo to Keycloak:

```
X-Forwarded-For: 203.0.113.45
X-Forwarded-Proto: https
X-Forwarded-Host: example.com
X-Forwarded-Port: 443
```
🔹 Without these headers, Keycloak thinks it's running on http://10.244.1.81:8080, so when it redirects, it generates:


```
http://10.244.1.81:8080/identitymgmt/admin  ❌ (Wrong)

```
🔹 With KC_PROXY_HEADERS=xforwarded, Keycloak reads the headers and correctly redirects to:


```
https://example.com/identitymgmt/admin  ✅ (Correct)

```

****
### FOOTNOTE INFO

if ur VirtulService can not find ur upstream run following:
```
kubectl rollout restart deployment -n $namespace

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
>helm install gloo gloo/gloo --namespace $namespace --create-namespace  --set gatewayProxies.gatewayProxy.service.annotations."service\.beta\.kubernetes\.io/azure-dns-label-name"="orderapigateway"


so then i used 
```
PS C:\Users\Acer> kubectl annotate service gateway-proxy -n $namespace service.beta.kubernetes.io/azure-dns-label-name=orderapigateway --overwrite
service/gateway-proxy annotated
PS C:\Users\Acer> kubectl describe service  gateway-proxy -n  $namespace
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
  --namespace $namespace \
  --create-namespace \
  --set gatewayProxies.gatewayProxy.service.annotations."service\.beta\.kubernetes\.io/azure-dns-label-name"="orderapigateway"

Helm will pass the value, but if the template does not use .Values.gatewayProxies.gatewayProxy.service.annotations, it does nothing.
Gloo's Helm chart does NOT have a reference to this field in the Service template.
Helm does not add custom values unless they are referenced in the template.
2. kubectl annotate Works Because It Directly Modifies the Kubernetes Resource
When you run:


kubectl annotate service gateway-proxy -n $namespace \
  service.beta.kubernetes.io/azure-dns-label-name=orderapigateway --overwrite

This command bypasses Helm entirely.
It directly modifies the live Kubernetes object.
Since annotations are dynamic fields, Kubernetes allows modifying them without recreating the resource.
so the ur service is accessable by:
```
http://orderapigateway.westUs.cloudapp.azure.com/keyvault/WeatherForecast

http://orderapigateway.westus.cloudapp.azure.com/identitymgmt/realms/AucRealm/.well-known/openid-configuration
```
****



# Build and deploy a React app using Openshift

We will describe a simple pipeline from a repo to a working deployed app.
We'll cover here some basic principles as :

* Buildconfig strategies
* Deployment Config and Deployments
* Imagestreams
* Templates
* Jenkins and S2I
* Services and routes

1. To start our process to build and deploy the app we must create a project "devops" or any other name and to install jenkins in the created namespace. We can do that in different ways, using a chart, using operators, deploying from registry, but we'll use software catalog from openshift where it can be installed from a template provided by RedHat™. This can be done very easy in Developer view where you can add an app from catalog, in our case Jenkins ( persistent ). Wait for jenkins to be ready ant then proceed to the next step.

2. Next we'll need to bring the Jenkinsfile into the jenkins instance. This can be done in several ways: copying the pipeline into jenkins, using the svn, or the method we'll use - by applying a buildconfig for <b>Jenkinsfile</b>.  We can use cli tool <b>oc</b> or copy and paste the yaml into the window opened by pressing the plus sign in bottom right corner.

3. If you check the jenkins instance you'll see that the pipeline has been added and already started the build, but the binary build fails. This is because we need to import a second buildconfig which defines the s2i build method, in our case using a simple nginx s2i to inject the static html built from the react app hosted in this repo.

4. The builds stucks at binary build, and if we'll check the logs we can see that there is no stream for this. Because the service account that builds the image is limited, the image stream is not created, so we'll need to create our imagesream from the definition using one of the methods : cli or web ui.

5. Now if we run we'll se our build complete, and in the imagestream we'll find the latest label, as defined in the buildconfig for s2i. The next step is to deploy our application, using Helm, and after the chart has run you can find your app already exposed on URL http(s)://[route].apps.[openshift.adress] .

> There you go ! we deployed an application using a jenkins build pipeline from scratch. [Show me the easy way](cherry.md)

## Command references

create a project

```bash
oc new-project devops
```

change namespace or project

```bash
oc project devops
```

apply a manifest

```bash
oc apply -f openshift\BC-jenkins.yaml
```

install a helm chart

```bash
helm install gremlins-helm ./gremlins/ --set nameOverride=gremlins-helm
```

update a helm chart

```bash
helm install gremlins-helm ./gremlins/
```

view the installed helm charts

```bash
helm list
```

## Deploying the BlogApi backend + PostgreSQL

1. Apply the ImageStream and BuildConfig, then start a build:

   ```bash
   oc project devops
   oc apply -f openshift/blogapi-imagestream.yaml
   oc apply -f openshift/blogapi-BC-docker.yaml
   oc start-build blogapi-build --follow
   ```

2. Install PostgreSQL (set a real password; do not commit it):

   ```bash
   helm install blogapi-postgres ./helm/postgres/ \
     --set credentials.password=<choose-a-strong-password>
   ```

   A `registry.redhat.io` pull secret must already exist in the `devops` namespace or the PostgreSQL image pull will fail. If it doesn't exist yet, create it and link it to the `default` service account:

   ```bash
   oc create secret docker-registry redhat-registry-pull-secret \
     --docker-server=registry.redhat.io \
     --docker-username=<your-redhat-username> \
     --docker-password=<your-redhat-password> \
     -n devops
   oc secrets link default redhat-registry-pull-secret --for=pull -n devops
   ```

3. Install BlogApi (choose a strong API key, and use the exact same PostgreSQL password from step 2):

   ```bash
   helm install blogapi ./helm/blogapi/ \
     --set apiKey=<choose-a-strong-api-key> \
     --set db.password=<same-password-as-step-2>
   ```

4. Verify:

   ```bash
   oc get route blogapi
   curl https://<route-host>/health
   curl -H "X-API-Key: <api-key>" https://<route-host>/api/posts
   ```

### ⚠️ Important: Password consistency

PostgreSQL only reads the chart-provided password from environment variables during the first `initdb`, when the PVC is brand new. That first `helm install blogapi-postgres ... --set credentials.password=...` value becomes the real database password for the lifetime of that PVC.

On later `helm upgrade` runs, the postgres chart Secret is regenerated from the new `credentials.password` value, but the running database password stored on disk is not changed automatically. In practice, `helm/blogapi` must always be installed or upgraded with `--set db.password=<the-original-postgres-password>` so the app matches the actual password inside PostgreSQL, even if the current Secret shows something else.

If you truly need to rotate the password, change it inside the running database with `psql` / `ALTER USER ... PASSWORD` inside the running Postgres pod, or delete and recreate the PVC (which also deletes all data). Do not rely on `helm upgrade --set credentials.password=...` alone to rotate the real PostgreSQL password.

## BlogApi Jenkins pipeline (CI build)

Mirroring the `gremlins-pipeline` JenkinsPipeline BuildConfig, `blogapi-pipeline` runs `dotnet test` against the backend before triggering the actual container image build, giving a shift-left test gate ahead of the Docker-strategy build already configured in `openshift/blogapi-BC-docker.yaml`.

1. Apply the Jenkins pipeline BuildConfig (Jenkins must already be installed in the namespace, see step 1 above):

   ```bash
   oc apply -f openshift/BC-jenkins-blogapi.yaml
   ```

2. Start the pipeline:

   ```bash
   oc start-build blogapi-pipeline --follow
   ```

This checks out the repo, runs `dotnet test backend/BlogApi.sln` inside a `mcr.microsoft.com/dotnet/sdk:10.0` agent pod, and — if tests pass — triggers `blogapi-build` (the Docker-strategy BuildConfig that produces the `blogapi:latest` image consumed by the `helm/blogapi` DeploymentConfig).

api-key=dasdkjsahdksajhd
